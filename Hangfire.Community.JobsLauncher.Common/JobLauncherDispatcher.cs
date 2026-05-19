using Hangfire.Common;
using Hangfire.Community.JobsLauncher.Common;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hangfire.Community.JobLauncher.Common
{
    public static class JobLauncherDispatcher
    {
        private static readonly BackgroundJobPerformer Performer = new BackgroundJobPerformer(JobFilterProviders.Providers, JobActivator.Current);
        private static readonly ConcurrentDictionary<string, Type> Types = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<(Type Type, string Method), MethodInfo> Methods = new ConcurrentDictionary<(Type Type, string Method), MethodInfo>();

        [LauncherJobDisplayName]
        public static void ExecuteJob(string className, string methodName, string serializedParameters, PerformContext context = null)
        {
            var job = CreateJob(className, methodName, serializedParameters);

            var bgJob = new BackgroundJob(
                context.BackgroundJob.Id,
                job,
                context.BackgroundJob.CreatedAt);

            var performContext = new PerformContext(
                context.Storage,
                context.Connection,
                bgJob,
                context.CancellationToken);

            try
            {
                Performer.Perform(performContext);
            }
            catch (JobPerformanceException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        public static Job CreateJob(string className, string methodName, string serializedParameters)
        {
            var type = ResolveType(className);

            var lambda = CreateDirectExpression(type, methodName, serializedParameters);

            return CreateJobFrom(lambda);
        }

        public static Job CreateJobFrom(LambdaExpression lambda)
        {
            if (lambda is Expression<Action> actionExpr3)
                return Job.FromExpression(actionExpr3);
            else if (lambda is Expression<Func<Task>> taskExpr3)
                return Job.FromExpression(taskExpr3);
            else
                throw new InvalidOperationException("Invalid lambda type");
        }

        public static LambdaExpression CreateDirectExpression(Type jobType, string methodName, string serializedParams)
        {
            var method = Methods.GetOrAdd((jobType, methodName), x =>
                x.Type.GetMethod(x.Method, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Method '{x.Method}' not found in '{x.Type.FullName}'."));

            var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(serializedParams)
               ?? new Dictionary<string, JsonElement>();

            var parameters = method.GetParameters();

            var args = parameters.Select(p => JobLauncherDispatcher.ResolveArgument(p, json)).ToArray();            

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

        public static object ResolveArgument(ParameterInfo p, Dictionary<string, JsonElement> json)
        {
            var type = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;

            if (type == typeof(PerformContext) ||
                type == typeof(IJobCancellationToken) ||
                type == typeof(CancellationToken))
                return null;

            if (!json.TryGetValue(p.Name, out var value))
                return p.HasDefaultValue
                    ? p.DefaultValue
                    : type.IsValueType
                        ? Activator.CreateInstance(type)
                        : null;

            if (type.IsEnum)
                return Enum.Parse(type, value.GetString() ?? value.GetRawText(), true);

            if (type == typeof(TimeSpan))
                return TimeSpan.Parse(value.GetString());

            return JsonSerializer.Deserialize(value.GetRawText(), type);
        }

        public static Type ResolveType(string className) =>
            Types.GetOrAdd(className, name =>
                AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(name, false, false))
                    .FirstOrDefault(t => t != null)
                ?? throw new InvalidOperationException($"Type '{name}' not found."));
    }
}
