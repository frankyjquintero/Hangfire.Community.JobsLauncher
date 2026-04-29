using Hangfire.Community.JobLauncher.Common;
using Hangfire.Community.JobsLauncher.Dashboard.Apis;
using Hangfire.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Hangfire.Community.JobsLauncher.Dashboard
{
    internal static class DirectJobInvoker
    {
        public static void Invoke(string className, string methodName, string serializedParams,
            bool includePerformContext = false, bool includeCancellationToken = false)
        {
            var type = TypeResolver.FindType(className) ?? throw new Exception("Tipo no encontrado");
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            object instance = method.IsStatic ? null : Activator.CreateInstance(type);
            var paramDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(serializedParams);
            var parameters = method.GetParameters().Select(p =>
            {
                if (p.ParameterType == typeof(PerformContext) && includePerformContext) return null;
                if (p.ParameterType == typeof(IJobCancellationToken) && includeCancellationToken)
                    return JobCancellationToken.Null;
                if (paramDict.TryGetValue(p.Name, out var element))
                    return JobLauncherDispatcher.ConvertJsonElement(element, p.ParameterType);  // mismo Convert que el dispatcher
                return p.DefaultValue ?? (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null);
            }).ToArray();
            method.Invoke(instance, parameters);
        }
    }
}
