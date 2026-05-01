using Hangfire;
using Hangfire.Common;
using Hangfire.Community.JobLauncher.Common;
using Hangfire.Community.JobsLauncher.Dashboard;
using Hangfire.Community.JobsLauncher.Dashboard.Models;
using Hangfire.Dashboard;
using Hangfire.Server;
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
        private const string HistoryHashKey = "joblauncher:history";
        private const string AuditLogHashKey = "joblauncher:audit-log";
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

            // Preparar parámetros serializados
            string serializedParams = request.RawParametersJson
                ?? JsonSerializer.Serialize(request.Parameters ?? new Dictionary<string, string>());

            Type jobType = TypeResolver.FindType(request.ClassName);

            string jobId = null;
            string engineUsed = jobType != null ? "Direct" : "BuiltIn"; // Para historial

            try
            {
                // Para trabajos recurrentes que NO usan el motor "Direct", forzamos siempre el dispatcher
                bool forceDispatcherForRecurring = request.ExecutionMode == "Recurring"
                                   && request.RecurringEngine == "BuiltIn";

                if (jobType != null && !forceDispatcherForRecurring)
                {
                    var method = jobType.GetMethod(request.MethodName,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                    if (method == null)
                        throw new ArgumentException($"Method {request.MethodName} not found in {jobType.FullName}");

                    object[] args = ConvertParametersForDirectCall(method, serializedParams,
                        request.IncludePerformContext, request.IncludeCancellationToken);
                    LambdaExpression directLambda = CreateDirectExpression(method, args);

                    // El motor se decide según el request (para todos los modos de ejecución)
                    if (request.ExecutionMode == "Recurring")
                    {
                        jobId = LaunchRecurringJob(request, serializedParams, jobType, method, args, directLambda);
                        engineUsed = request.RecurringEngine ?? "Direct";
                    }
                    else
                    {
                        jobId = LaunchNonRecurringDirect(request, directLambda);
                        engineUsed = "Direct";
                    }
                }
                else
                {
                    // Sin tipo, usamos el dispatcher común para todo
                    if (request.ExecutionMode == "Recurring")
                    {
                        jobId = LaunchRecurringJob(request, serializedParams, null, null, null, null);
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
            switch (request.ExecutionMode)
            {
                case "FireAndForget":
                    if (lambda is Expression<Action> actionExpr)
                        return BackgroundJob.Enqueue(actionExpr);
                    else if (lambda is Expression<Func<Task>> taskExpr)
                        return BackgroundJob.Enqueue(taskExpr);
                    throw new InvalidOperationException("Invalid lambda type");

                case "Schedule":
                    var delay = TimeSpan.FromMinutes(request.DelayMinutes ?? 30);
                    if (lambda is Expression<Action> actionExpr2)
                        return BackgroundJob.Schedule(actionExpr2, delay);
                    if (lambda is Expression<Func<Task>> taskExpr2)
                        return BackgroundJob.Schedule(taskExpr2, delay);
                    throw new InvalidOperationException("Invalid lambda type");

                case "ScheduleDateTime":
                    if (lambda is Expression<Action> actionExpr3)
                        return BackgroundJob.Schedule(actionExpr3, request.ScheduledDateTime.Value);
                    if (lambda is Expression<Func<Task>> taskExpr3)
                        return BackgroundJob.Schedule(taskExpr3, request.ScheduledDateTime.Value);
                    throw new InvalidOperationException("Invalid lambda type");

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

        // Lanza con dispatcher (sin tipo conocido)
        private string LaunchWithDispatcher(LaunchRequest request, string serializedParams)
        {
            switch (request.ExecutionMode)
            {
                case "FireAndForget":
                    return BackgroundJob.Enqueue(
                        () => JobLauncherDispatcher.ExecuteJob(request.ClassName, request.MethodName, request.Queue,
                            serializedParams, request.IncludePerformContext, request.IncludeCancellationToken));

                case "Schedule":
                    if (!request.DelayMinutes.HasValue) throw new ArgumentException("DelayMinutes requerido.");
                    return BackgroundJob.Schedule(
                        () => JobLauncherDispatcher.ExecuteJob(request.ClassName, request.MethodName, request.Queue,
                            serializedParams, request.IncludePerformContext, request.IncludeCancellationToken),
                        TimeSpan.FromMinutes(request.DelayMinutes.Value));

                case "ScheduleDateTime":
                    if (!request.ScheduledDateTime.HasValue) throw new ArgumentException("ScheduledDateTime requerido.");
                    return BackgroundJob.Schedule(
                        () => JobLauncherDispatcher.ExecuteJob(request.ClassName, request.MethodName, request.Queue,
                            serializedParams, request.IncludePerformContext, request.IncludeCancellationToken),
                        request.ScheduledDateTime.Value);

                case "Continuation":
                    if (string.IsNullOrWhiteSpace(request.ParentJobId)) throw new ArgumentException("ParentJobId requerido.");
                    return BackgroundJob.ContinueJobWith(request.ParentJobId,
                        () => JobLauncherDispatcher.ExecuteJob(request.ClassName, request.MethodName, request.Queue,
                            serializedParams, request.IncludePerformContext, request.IncludeCancellationToken));

                default:
                    throw new ArgumentException($"Modo de ejecución no soportado: {request.ExecutionMode}");
            }
        }

        // Método centralizado para lanzar trabajos recurrentes, independientemente del motor y si es directo o dispatcher
        private string LaunchRecurringJob(
            LaunchRequest request,
            string serializedParams,
            Type jobType,                // null si es vía dispatcher
            MethodInfo method,           // null si dispatcher
            object[] args,               // null si dispatcher
            LambdaExpression directLambda // null si dispatcher
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
                    // Construir expresión para dispatcher
                    if (request.IncludePerformContext || request.IncludeCancellationToken)
                    {
                        // La firma del dispatcher no acepta PerformContext/CancellationToken, se ignoran
                    }
                    builtInLambda = Expression.Lambda<Action>(
                        Expression.Call(
                            typeof(JobLauncherDispatcher).GetMethod(nameof(JobLauncherDispatcher.ExecuteJob)),
                            Expression.Constant(request.ClassName),
                            Expression.Constant(request.MethodName),
                            Expression.Constant(request.Queue),
                            Expression.Constant(serializedParams),
                            Expression.Constant(request.IncludePerformContext),
                            Expression.Constant(request.IncludeCancellationToken)
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
                        RecurringJob.AddOrUpdate(recurringId, actionExpr, request.CronExpression, queue: request.Queue);
                    else if (directLambda is Expression<Func<Task>> taskExpr)
                        RecurringJob.AddOrUpdate(recurringId, taskExpr, request.CronExpression, queue: request.Queue);
                    else
                        throw new InvalidOperationException("Direct lambda type not supported.");
                    break;

                case "BuiltIn":
                    if (builtInLambda is Expression<Action> actionExpr2)
                        RecurringJob.AddOrUpdate(recurringId, actionExpr2, request.CronExpression, queue: request.Queue);
                    else if (builtInLambda is Expression<Func<Task>> taskExpr2)
                        RecurringJob.AddOrUpdate(recurringId, taskExpr2, request.CronExpression, queue: request.Queue);
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

        private static object[] ConvertParametersForDirectCall(
            MethodInfo method,
            string serializedParams,
            bool includePerformContext,
            bool includeCancellationToken)
        {
            var paramDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(serializedParams)
                ?? new Dictionary<string, JsonElement>();

            var methodParams = method.GetParameters();
            var args = new List<object>();

            foreach (var p in methodParams)
            {
                if (p.ParameterType == typeof(PerformContext) && includePerformContext)
                {
                    args.Add(null); // Hangfire lo reemplaza automáticamente
                    continue;
                }
                if (p.ParameterType == typeof(IJobCancellationToken) && includeCancellationToken)
                {
                    args.Add(JobCancellationToken.Null);
                    continue;
                }

                if (paramDict.TryGetValue(p.Name, out var element))
                {
                    args.Add(JobLauncherDispatcher.ConvertJsonElement(element, p.ParameterType));
                }
                else
                {
                    args.Add(p.DefaultValue ?? (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null));
                }
            }

            return args.ToArray();
        }

        private static LambdaExpression CreateDirectExpression(MethodInfo method, object[] args)
        {
            var parameters = method.GetParameters();

            var paramExpressions = parameters.Select((p, i) =>
            {
                if (i >= args.Length)
                    throw new InvalidOperationException("Mismatch between method parameters and arguments.");
                var arg = args[i];
                return Expression.Constant(arg, p.ParameterType);
            }).ToArray();

            if (method.IsStatic)
            {
                var call = Expression.Call(method, paramExpressions);
                if (method.ReturnType == typeof(void))
                    return Expression.Lambda<Action>(call);
                if (method.ReturnType == typeof(Task))
                    return Expression.Lambda<Func<Task>>(call);
                throw new NotSupportedException($"Return type {method.ReturnType} not supported.");
            }
            else
            {
                var instance = Expression.New(method.DeclaringType.GetConstructor(Type.EmptyTypes));
                var call = Expression.Call(instance, method, paramExpressions);
                if (method.ReturnType == typeof(void))
                    return Expression.Lambda<Action>(call);
                if (method.ReturnType == typeof(Task))
                    return Expression.Lambda<Func<Task>>(call);
                throw new NotSupportedException($"Return type {method.ReturnType} not supported.");
            }
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

            var storage = context.Storage;
            using (var connection = storage.GetConnection())
            using (var transaction = connection.CreateWriteTransaction())
            {
                var key = $"{DateTime.UtcNow.Ticks}-{jobId}";
                transaction.SetRangeInHash(HistoryHashKey, new[] { new KeyValuePair<string, string>(key, JsonSerializer.Serialize(entry)) });

                if (_options.EnableAuditLog)
                {
                    transaction.SetRangeInHash(AuditLogHashKey, new[] { new KeyValuePair<string, string>(key, JsonSerializer.Serialize(entry)) });
                }

                var allEntries = connection.GetAllEntriesFromHash(HistoryHashKey) ?? new Dictionary<string, string>();
                if (allEntries.Count > _options.HistoryMaxEntries)
                {
                    var keysToRemove = allEntries.Keys
                        .OrderBy(k => k)
                        .Take(allEntries.Count - _options.HistoryMaxEntries)
                        .ToList();
                    foreach (var k in keysToRemove)
                    {
                        transaction.RemoveHash(HistoryHashKey);
                    }
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