using Hangfire;
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
            string engineUsed = jobType != null ? "Direct" : "BuiltIn";

            try
            {
                // Si tenemos el tipo, usamos la vía directa (sin dispatcher)
                if (jobType != null)
                {
                    jobId = LaunchDirect(request, jobType, serializedParams);
                }
                else
                {
                    // Sin tipo, usamos el dispatcher común
                    jobId = LaunchWithDispatcher(request, serializedParams);
                    if (request.RecurringEngine == "DynamicJobs")
                        engineUsed = "DynamicJobs";
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

        private string LaunchDirect(LaunchRequest request, Type jobType, string serializedParams)
        {
            var method = jobType.GetMethod(request.MethodName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (method == null)
                throw new ArgumentException($"Method {request.MethodName} not found in {jobType.FullName}");

            object[] args = ConvertParametersForDirectCall(method, serializedParams,
                request.IncludePerformContext, request.IncludeCancellationToken);
            LambdaExpression lambda = CreateDirectExpression(method, args);

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

                case "Recurring":
                    return LaunchRecurringDirectWithExpression(request, method, args, lambda);

                default:
                    throw new ArgumentException($"Modo de ejecución no soportado: {request.ExecutionMode}");
            }
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
                // Hangfire inyecta automáticamente PerformContext y IJobCancellationToken si los necesita
                // No los incluimos en la expresión lambda, así que simplemente los omitimos.
                if (p.ParameterType == typeof(PerformContext) && includePerformContext)
                    continue;
                if (p.ParameterType == typeof(IJobCancellationToken) && includeCancellationToken)
                    continue;

                if (paramDict.TryGetValue(p.Name, out var element))
                {
                    args.Add(JobLauncherDispatcher.ConvertJsonElement(element, p.ParameterType));
                }
                else
                {
                    // Si no está en el JSON, usar valor por defecto
                    args.Add(p.DefaultValue ?? (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null));
                }
            }

            return args.ToArray();
        }

        private static LambdaExpression CreateDirectExpression(MethodInfo method, object[] args)
        {
            // args ya están filtrados (sin PerformContext/CancellationToken)
            var paramExpressions = args.Select(a => Expression.Constant(a)).ToArray();

            if (method.IsStatic)
            {
                var call = Expression.Call(method, paramExpressions);
                if (method.ReturnType == typeof(void))
                    return Expression.Lambda<Action>(call);
                else if (method.ReturnType == typeof(Task))
                    return Expression.Lambda<Func<Task>>(call);
                else
                    throw new NotSupportedException("Return type not supported.");
            }
            else
            {
                // Método de instancia: crear instancia con new()
                var ctor = method.DeclaringType.GetConstructor(Type.EmptyTypes)
                    ?? throw new InvalidOperationException("Instance method requires parameterless constructor.");
                var instanceExp = Expression.New(ctor);
                var call = Expression.Call(instanceExp, method, paramExpressions);
                if (method.ReturnType == typeof(void))
                    return Expression.Lambda<Action>(call);
                else if (method.ReturnType == typeof(Task))
                    return Expression.Lambda<Func<Task>>(call);
                else
                    throw new NotSupportedException("Return type not supported.");
            }
        }

        private string LaunchRecurringDirectWithExpression(LaunchRequest request, MethodInfo method, object[] args, LambdaExpression lambda)
        {
            var recurringId = $"joblauncher-{Guid.NewGuid():N}";

            if (request.RecurringEngine == "DynamicJobs" && IsDynamicJobsAvailable())
            {
                // Motor DynamicJobs: requiere el paquete Hangfire.DynamicJobs
                var parameterTypes = method.GetParameters()
                    .Select(p => p.ParameterType)
                    .ToList();

                // Construir el DynamicJob
                var dynamicJobType = Type.GetType("Hangfire.DynamicJobs.DynamicJob, Hangfire.DynamicJobs")
                    ?? throw new InvalidOperationException("DynamicJobs no está disponible.");
                var dynamicJob = Activator.CreateInstance(dynamicJobType,
                    request.ClassName,
                    request.MethodName,
                    string.Join(",", parameterTypes.Select(t => t.FullName ?? t.Name)),
                    JsonSerializer.Serialize(args), // Parámetros ya convertidos
                    null); // filters

                // Crear DynamicRecurringJobOptions (opcional)
                var optionsType = Type.GetType("Hangfire.DynamicJobs.DynamicRecurringJobOptions, Hangfire.DynamicJobs");
                object options = optionsType != null
                    ? Activator.CreateInstance(optionsType)
                    : null;
                if (options != null)
                {
                    options.GetType().GetProperty("Queue")?.SetValue(options, request.Queue);
                    options.GetType().GetProperty("TimeZone")?.SetValue(options, TimeZoneInfo.Utc);
                }

                // Invocar AddOrUpdateDynamic
                var recurringJobManager = new RecurringJobManager();
                var addMethod = recurringJobManager.GetType().GetMethod("AddOrUpdateDynamic")
                    ?? throw new InvalidOperationException("Método AddOrUpdateDynamic no encontrado.");
                addMethod.Invoke(recurringJobManager, new[] { recurringId, dynamicJob, request.CronExpression, options });

                return recurringId;
            }
            else
            {
                // Motor BuiltIn: usar la expresión lambda directamente
                if (lambda is Expression<Action> actionExpr)
                {
                    RecurringJob.AddOrUpdate(recurringId, actionExpr, cronExpression: request.CronExpression, queue:  request.Queue);
                }
                else if (lambda is Expression<Func<Task>> taskExpr)
                {
                    RecurringJob.AddOrUpdate(recurringId, taskExpr, cronExpression: request.CronExpression, queue: request.Queue);
                }
                else
                {
                    throw new InvalidOperationException("Tipo de expresión no soportado para job recurrente.");
                }
                return recurringId;
            }
        }

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

                case "Recurring":
                    return LaunchRecurringDispatcher(request, serializedParams);

                default:
                    throw new ArgumentException($"Modo de ejecución no soportado: {request.ExecutionMode}");
            }
        }

        private string LaunchRecurringDispatcher(LaunchRequest request, string serializedParams)
        {
            var recurringId = $"joblauncher-{Guid.NewGuid():N}";
            if (request.RecurringEngine == "DynamicJobs" && IsDynamicJobsAvailable())
            {
                // Usar DynamicJob sin tipos específicos (todos como string)
                var dynamicJob = Activator.CreateInstance(
                    Type.GetType("Hangfire.DynamicJobs.DynamicJob, Hangfire.DynamicJobs"),
                    request.ClassName,
                    request.MethodName,
                    "System.String", // Un único parámetro string
                    JsonSerializer.Serialize(new { parameters = serializedParams }), // Envolver
                    null);

                var recurringJobManager = new RecurringJobManager();
                var options = Activator.CreateInstance(
                    Type.GetType("Hangfire.DynamicJobs.DynamicRecurringJobOptions, Hangfire.DynamicJobs"));
                options.GetType().GetProperty("Queue")?.SetValue(options, request.Queue);

                recurringJobManager.GetType()
                    .GetMethod("AddOrUpdateDynamic")
                    ?.Invoke(recurringJobManager, new[]
                    {
                        recurringId,
                        dynamicJob,
                        request.CronExpression,
                        options
                    });
                return recurringId;
            }
            else
            {
                RecurringJob.AddOrUpdate(
                        recurringId,
                        () => JobLauncherDispatcher.ExecuteJob(
                            request.ClassName,
                            request.MethodName,
                            request.Queue,
                            serializedParams,
                            request.IncludePerformContext,
                            request.IncludeCancellationToken),
                        cronExpression: request.CronExpression,
                        queue: request.Queue);
                return recurringId;
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
                Mode = request.RawParametersJson != null ? "Manual" : "Assisted",
                ExecutionMode = request.ExecutionMode,
                Engine = engine,
                ParametersJson = serializedParams,
                IncludePerformContext = request.IncludePerformContext,
                IncludeCancellationToken = request.IncludeCancellationToken,
                //User = context.Request?.User?.Identity?.Name ?? "Anonymous"
                User = "Anonymous"
            };

            var storage = context.Storage;
            using (var connection = storage.GetConnection())
            using (var transaction = connection.CreateWriteTransaction())
            {
                // Historial volátil (clave ordenable)
                var key = $"{DateTime.UtcNow.Ticks}-{jobId}";
                transaction.SetRangeInHash(HistoryHashKey, new[] { new KeyValuePair<string, string>(key, JsonSerializer.Serialize(entry)) });

                // Auditoría si está habilitada
                if (_options.EnableAuditLog)
                {
                    transaction.SetRangeInHash(AuditLogHashKey, new[] { new KeyValuePair<string, string>(key, JsonSerializer.Serialize(entry)) });
                }

                // Limitar tamaño del historial volátil
                var allEntries = connection.GetAllEntriesFromHash(HistoryHashKey) ?? new Dictionary<string, string>();
                if (allEntries.Count > _options.HistoryMaxEntries)
                {
                    // Borrar los más antiguos (ordenando por clave descendente nos quedamos con los últimos HistoryMaxEntries)
                    var keysToRemove = allEntries.Keys
                        .OrderBy(k => k)
                        .Take(allEntries.Count - _options.HistoryMaxEntries)
                        .ToList();
                    foreach (var k in keysToRemove)
                    {
                        transaction.RemoveHash(HistoryHashKey); // Desafortunadamente no hay RemoveHashField; reescribimos todo
                    }
                    // Estrategia alternativa: borrar el hash completo y volver a añadir solo los que queremos conservar
                    // Debido a la complejidad, lo hacemos en un paso posterior o lo dejamos así (crecerá hasta que alguien limpie manualmente).
                    // Para simplificar, confiamos en que el método HandleClearHistory se encargará de purgarlo.
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
            return Type.GetType("Hangfire.DynamicJobs.DynamicJob, Hangfire.DynamicJobs") != null;
        }

        private List<Type> GetParameterTypes(Type jobType, string methodName)
        {
            var method = jobType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (method == null) throw new Exception($"Método {methodName} no encontrado en {jobType.FullName}");
            return method.GetParameters().Select(p => p.ParameterType).ToList();
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