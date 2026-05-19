using Hangfire;
using Hangfire.Common;
using Hangfire.Community.JobLauncher.Common;
using Hangfire.Community.JobsLauncher.Dashboard;
using Hangfire.Community.JobsLauncher.Dashboard.Models;
using Hangfire.Dashboard;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hangfire.Community.JobsLauncher.Dashboard.Apis
{
    public class LaunchJobApi : IDashboardDispatcher
    {
        private const string HistoryListKey = "joblauncher:history-list";
        private const string AuditLogListKey = "joblauncher:audit-log-list";
        private const string KnownQueuesSetKey = "joblauncher:known-queues";

        private readonly JobLauncherOptions _options;

        public LaunchJobApi(JobLauncherOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task Dispatch(DashboardContext context)
        {
            if (!context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 405;
                await WriteJson(context, new LaunchResult { Success = false, Error = "Method Not Allowed" });
                return;
            }

            LaunchRequest request = null;
            try
            {
                var formValues = await context.Request.GetFormValuesAsync("json");
                if (formValues == null || formValues.Count == 0)
                    throw new Exception("Falta el campo 'json' en el formulario.");

                var json = formValues[0];
                request = JsonSerializer.Deserialize<LaunchRequest>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null)
                    throw new Exception("Cuerpo vacío o mal formado.");
            }
            catch (Exception ex)
            {
                await WriteJson(context, new LaunchResult { Success = false, Error = $"Error al leer la solicitud: {ex.Message}" });
                return;
            }

            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(request.ClassName) || string.IsNullOrWhiteSpace(request.MethodName))
            {
                await WriteJson(context, new LaunchResult { Success = false, Error = "ClassName y MethodName son obligatorios." });
                return;
            }           

            try
            {
                Type jobType = JobLauncherDispatcher.ResolveType(request.ClassName);

                // Preparar parámetros serializados
                string serializedParams = request.RawParametersJson
                    ?? JsonSerializer.Serialize(request.Parameters ?? new Dictionary<string, string>());

                string jobId = null;
                string engineUsed = jobType != null ? "Direct" : "BuiltIn"; // Para historial

                // Para trabajos recurrentes que NO usan el motor "Direct", forzamos siempre el dispatcher
                bool forceDispatcherForRecurring = request.ExecutionMode == "Recurring" && request.RecurringEngine == "BuiltIn";

                // Si existe el type object quiere decir que esta dentro del appdomain de la aplicacion
                // por ende puede ejecutarse sin envoltorio, lo que permite aprovechar la resolución de dependencias y otras características de Hangfire.
                // si es recurrente tomara la definicion del job para ello, si no es recurrente se lanzara directamente con el tipo y método indicado.
                if (jobType != null && !forceDispatcherForRecurring)
                {
                    LambdaExpression directLambda = JobLauncherDispatcher.CreateDirectExpression(jobType, request.MethodName, serializedParams);

                    // El motor se decide según el request (para todos los modos de ejecución)
                    if (request.ExecutionMode == "Recurring")
                    {
                        // job recurrente conociendo el tipo se define el metodo de lanzamiento específico para recurrentes,
                        // que permite aprovechar la definición del job y otras características, y se elige el motor según el request (Direct o BuiltIn o Dynamic jobs)                        
                        jobId = LaunchRecurringJob(request, serializedParams, jobType, directLambda);
                        engineUsed = request.RecurringEngine ?? "Direct";
                    }
                    else
                    {
                        // Lanzamos un job directamente desde el ensamblado de la aplicación, sin pasar por el dispatcher,
                        // lo que permite aprovechar la resolución de dependencias y otras características de Hangfire.                        
                        jobId = LaunchNonRecurringDirect(request, directLambda);
                        engineUsed = "Direct";
                    }
                }
                else
                {
                    // Sin tipo, usamos el dispatcher común para todo
                    if (request.ExecutionMode == "Recurring")
                    {
                        jobId = LaunchRecurringJob(request, serializedParams, null, null);
                        engineUsed = request.RecurringEngine ?? "BuiltIn";
                    }
                    else
                    {
                        jobId = LaunchWithDispatcher(request, serializedParams);
                        engineUsed = "BuiltIn";
                    }
                }

                // Registrar en el historial
                SaveToHistory(context, request, serializedParams, jobId, engineUsed);

                // Actualizar el conjunto de colas conocidas
                AddToKnownQueues(context, request.Queue);

                var basePath = context.Request.PathBase?.TrimEnd('/') ?? "";
                var link = $"{basePath}/jobs/details/{jobId}";
                await WriteJson(context, new LaunchResult { Success = true, JobId = jobId, Link = link });
            }
            catch (Exception ex)
            {
                await WriteJson(context, new LaunchResult { Success = false, Error = $"Error al lanzar el job: {ex.Message}" });
            }
        }

        // Lanza trabajos no recurrentes usando expresión directa
        private string LaunchNonRecurringDirect(LaunchRequest request, LambdaExpression lambda)
        {
            var client = new BackgroundJobClient();
            var jobDirect = JobLauncherDispatcher.CreateJobFrom(lambda);

            switch (request.ExecutionMode)
            {
                case "FireAndForget":
                    return client.Create(jobDirect, new EnqueuedState(request.Queue));

                case "Schedule":
                    var delay = TimeSpan.FromMinutes(request.DelayMinutes ?? 30);
                    string jobId;
                    jobId = client.Create(jobDirect, new ScheduledState(delay));

                    // Asignar la cola mediante el parámetro del trabajo (única forma con ScheduledState)
                    SetJobQueue(jobId, request.Queue);
                    return jobId;

                case "ScheduleDateTime":
                    if (!request.ScheduledDateTime.HasValue)
                        throw new ArgumentException("ScheduledDateTime requerido.");

                    string schedJobId;
                    var scheduledTime = request.ScheduledDateTime.Value;
                    var scheduledState = new ScheduledState(scheduledTime);
                    schedJobId = client.Create(jobDirect, scheduledState);

                    SetJobQueue(schedJobId, request.Queue);
                    return schedJobId;

                case "Continuation":
                    if (lambda is Expression<Action> actionExpr4)
                        return BackgroundJob.ContinueJobWith(request.ParentJobId, actionExpr4);
                    if (lambda is Expression<Func<Task>> taskExpr4)
                        return BackgroundJob.ContinueJobWith(request.ParentJobId, taskExpr4);
                    throw new InvalidOperationException("Invalid lambda type");

                default:
                    throw new ArgumentException($"Modo de ejecución no soportado: {request.ExecutionMode}");
            }
        }

        // Método auxiliar para fijar la cola en un job ya creado
        private void SetJobQueue(string jobId, string queue)
        {
            using (var connection = JobStorage.Current.GetConnection())
            {
                connection.SetJobParameter(jobId, ParametersNameJobs.Queue, queue);
            }
        }

        // Lanza con dispatcher (sin tipo conocido)
        private string LaunchWithDispatcher(LaunchRequest request, string serializedParams)
        {
            var client = new BackgroundJobClient();

            switch (request.ExecutionMode)
            {
                case "FireAndForget":
                    return client.Create(
                        Job.FromExpression(() => JobLauncherDispatcher.ExecuteJob(
                            request.ClassName, request.MethodName,
                            serializedParams, null)),
                        new EnqueuedState(request.Queue));

                case "Schedule":
                    if (!request.DelayMinutes.HasValue) throw new ArgumentException("DelayMinutes requerido.");
                    var delay = TimeSpan.FromMinutes(request.DelayMinutes.Value);
                    var jobId = client.Create(
                        Job.FromExpression(() => JobLauncherDispatcher.ExecuteJob(
                            request.ClassName, request.MethodName,
                            serializedParams, null)),
                        new ScheduledState(delay));
                    SetJobQueue(jobId, request.Queue);
                    return jobId;

                case "ScheduleDateTime":
                    if (!request.ScheduledDateTime.HasValue) throw new ArgumentException("ScheduledDateTime requerido.");
                    var schedTime = request.ScheduledDateTime.Value;
                    var schedJobId = client.Create(
                        Job.FromExpression(() => JobLauncherDispatcher.ExecuteJob(
                            request.ClassName, request.MethodName,
                            serializedParams, null)),
                        new ScheduledState(schedTime));
                    SetJobQueue(schedJobId, request.Queue);
                    return schedJobId;

                case "Continuation":
                    if (string.IsNullOrWhiteSpace(request.ParentJobId)) throw new ArgumentException("ParentJobId requerido.");
                    // Continuación sin cola explícita (hereda del padre)
                    return BackgroundJob.ContinueJobWith(request.ParentJobId,
                        () => JobLauncherDispatcher.ExecuteJob(
                            request.ClassName, request.MethodName,
                            serializedParams, null));

                default:
                    throw new ArgumentException($"Modo de ejecución no soportado: {request.ExecutionMode}");
            }
        }


        // Método centralizado para lanzar trabajos recurrentes, independientemente del motor y si es directo o dispatcher
        private string LaunchRecurringJob(
            LaunchRequest request,
            string serializedParams,
            Type jobType,
            LambdaExpression directLambda
        )
        {
            var recurringId = $"joblauncher-{Guid.NewGuid():N}";
            var engine = request.RecurringEngine ?? (jobType != null ? "Direct" : "BuiltIn");

            // Preparar la expresión lambda adecuada según el motor "BuiltIn"
            LambdaExpression builtInLambda = null;
            if (engine == "BuiltIn" || engine == "DynamicJobs")
            {
                if (jobType != null)
                {
                    builtInLambda = directLambda; // usar expresión directa
                }
                else
                {
                    builtInLambda = Expression.Lambda<Action>(
                        Expression.Call(
                            typeof(JobLauncherDispatcher).GetMethod(nameof(JobLauncherDispatcher.ExecuteJob)),
                            Expression.Constant(request.ClassName),
                            Expression.Constant(request.MethodName),
                            Expression.Constant(serializedParams),
                            Expression.Constant(null, typeof(PerformContext))
                        )
                    );
                }
            }

            switch (engine)
            {
                case "Direct":
                    if (jobType == null)
                        throw new InvalidOperationException("Direct engine requires a resolved type.");
                    // Usar expresión directa
                    if (directLambda is Expression<Action> actionExpr)
                        RecurringJob.AddOrUpdate(recurringId, request.Queue, actionExpr, request.CronExpression);
                    else if (directLambda is Expression<Func<Task>> taskExpr)
                        RecurringJob.AddOrUpdate(recurringId, request.Queue, taskExpr, request.CronExpression);
                    else
                        throw new InvalidOperationException("Direct lambda type not supported.");
                    break;

                case "BuiltIn":
                    if (builtInLambda is Expression<Action> actionExpr2)
                        RecurringJob.AddOrUpdate(recurringId, request.Queue, actionExpr2, request.CronExpression);
                    else if (builtInLambda is Expression<Func<Task>> taskExpr2)
                        RecurringJob.AddOrUpdate(recurringId, request.Queue, taskExpr2, request.CronExpression);
                    else
                        throw new InvalidOperationException("BuiltIn lambda type not supported.");
                    break;

                case "DynamicJobs":
                    if (!IsDynamicJobsAvailable())
                        throw new InvalidOperationException("DynamicJobs engine not available.");

                    Job job = null;
                    if (jobType != null)
                    {
                        if (directLambda is Expression<Action> actionExprr)
                            job = Job.FromExpression(actionExprr);
                        else if (directLambda is Expression<Func<Task>> taskExpr)
                            job = Job.FromExpression(taskExpr);
                        else
                            throw new InvalidOperationException("Direct lambda type not supported for DynamicJobs.");
                    }
                    else
                    {
                        if (builtInLambda is Expression<Action> actionExprr)
                            job = Job.FromExpression(actionExprr);
                        else if (builtInLambda is Expression<Func<Task>> taskExpr)
                            job = Job.FromExpression(taskExpr);
                        else
                            throw new InvalidOperationException("Dispatcher lambda type not supported for DynamicJobs.");
                    }

                    // Crear DynamicRecurringJobOptions por reflexión (evita dependencia de compilación)
                    var dynamicOptionsType = Type.GetType("Hangfire.DynamicRecurringJobOptions, Hangfire.DynamicJobs");
                    var dynamicOptions = Activator.CreateInstance(dynamicOptionsType);
                    dynamicOptionsType.GetProperty("QueueName")?.SetValue(dynamicOptions, request.Queue);
                    dynamicOptionsType.GetProperty("TimeZone")?.SetValue(dynamicOptions, TimeZoneInfo.Utc);

                    // Invocar AddOrUpdateDynamic como método de extensión estático
                    var extensionsType = Type.GetType("Hangfire.DynamicJobRecurringJobManagerExtensions, Hangfire.DynamicJobs");
                    var addMethod = extensionsType.GetMethod("AddOrUpdateDynamic", new[] {
                        typeof(IRecurringJobManager),
                        typeof(string),
                        typeof(Job),
                        typeof(string),
                        dynamicOptionsType
                    });
                    var manager = new RecurringJobManager();
                    addMethod.Invoke(null, new object[] { manager, recurringId, job, request.CronExpression, dynamicOptions });
                    break;

                default:
                    throw new ArgumentException($"Unsupported Recurring Engine: {engine}");
            }

            return recurringId;
        }



        private void SaveToHistory(DashboardContext context, LaunchRequest request, string serializedParams, string jobId, string engine)
        {
            var entry = new HistoryEntry
            {
                JobId = jobId,
                Timestamp = DateTime.UtcNow,
                ClassName = request.ClassName,
                MethodName = request.MethodName,
                Queue = request.Queue,
                Mode = request.Mode,
                ExecutionMode = request.ExecutionMode,
                Engine = engine,
                ParametersJson = serializedParams,
                IncludePerformContext = request.IncludePerformContext,
                IncludeCancellationToken = request.IncludeCancellationToken,
                User = "Anonymous"
            };

            var json = JsonSerializer.Serialize(entry);

            var storage = context.Storage;
            using (var connection = (JobStorageConnection)storage.GetConnection())
            using (var transaction = connection.CreateWriteTransaction())
            {
                // Insertar al final de la lista (más reciente al final)
                transaction.InsertToList(HistoryListKey, json);

                // Limitar el tamaño: si excede HistoryMaxEntries, eliminar los más antiguos (principio)
                long count = connection.GetListCount(HistoryListKey);
                if (count > _options.HistoryMaxEntries)
                {
                    // Eliminar los primeros (count - HistoryMaxEntries) elementos
                    transaction.TrimList(HistoryListKey, 0, _options.HistoryMaxEntries - 1);
                }

                // Auditoría: misma lógica en otra lista
                if (_options.EnableAuditLog)
                {
                    transaction.InsertToList(AuditLogListKey, json);
                }

                transaction.Commit();
            }
        }

        private void AddToKnownQueues(DashboardContext context, string queue)
        {
            var storage = context.Storage;
            using (var connection = storage.GetConnection())
            using (var transaction = connection.CreateWriteTransaction())
            {
                transaction.AddToSet(KnownQueuesSetKey, queue);
                transaction.Commit();
            }
        }

        private bool IsDynamicJobsAvailable()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                    .Any(a => a.GetType("Hangfire.DynamicJob") != null);
        }

        private static async Task WriteJson(DashboardContext context, object data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var bytes = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}