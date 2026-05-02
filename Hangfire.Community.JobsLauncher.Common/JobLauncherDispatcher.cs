using Hangfire.Server;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Hangfire.Community.JobLauncher.Common
{
    public static class JobLauncherDispatcher
    {
        public static void ExecuteJob(
            string className,
            string methodName,
            string queue,
            string serializedParameters,
            bool includePerformContext = false,
            bool includeCancellationToken = false)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assm => assm.GetType(className))
                .FirstOrDefault(t => t != null)
                ?? throw new InvalidOperationException($"Type '{className}' not found in any loaded assembly.");

            var method = type.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            object instance = method.IsStatic ? null : Activator.CreateInstance(type);
            object[] parameters = BuildParameterArray(
                method.GetParameters(),
                serializedParameters,
                includePerformContext,
                includeCancellationToken);
            method.Invoke(instance, parameters);
        }

        private static object[] BuildParameterArray(
            ParameterInfo[] paramInfos, string json,
            bool includePerformContext, bool includeCancellationToken)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            var list = new List<object>();
            foreach (var p in paramInfos)
            {
                if (p.ParameterType == typeof(PerformContext) && includePerformContext)
                {
                    list.Add(null); // Hangfire inyecta el contexto real
                    continue;
                }
                if (p.ParameterType == typeof(IJobCancellationToken) && includeCancellationToken)
                {
                    list.Add(JobCancellationToken.Null);
                    continue;
                }
                if (dict.TryGetValue(p.Name, out var element))
                    list.Add(ConvertJsonElement(element, p.ParameterType));
                else
                    list.Add(p.DefaultValue ?? (p.ParameterType.IsValueType ?
                        Activator.CreateInstance(p.ParameterType) : null));
            }
            return list.ToArray();
        }

        public static object ConvertJsonElement(JsonElement element, Type targetType)
        {
            // Tipos primitivos (string, int, etc.)
            if (targetType == typeof(string)) return element.GetString();
            if (targetType == typeof(int)) return element.GetInt32();
            if (targetType == typeof(long)) return element.GetInt64();
            if (targetType == typeof(bool)) return element.GetBoolean();
            if (targetType == typeof(double)) return element.GetDouble();
            if (targetType == typeof(float)) return element.GetSingle();
            if (targetType == typeof(decimal)) return element.GetDecimal();
            if (targetType == typeof(DateTime)) return element.GetDateTime();
            if (targetType == typeof(DateTimeOffset)) return element.GetDateTimeOffset();
            if (targetType == typeof(Guid)) return element.GetGuid();
            if (targetType == typeof(TimeSpan)) return TimeSpan.Parse(element.GetString());

            // Nullable<T>
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                if (element.ValueKind == JsonValueKind.Null) return null;
                return ConvertJsonElement(element, Nullable.GetUnderlyingType(targetType));
            }

            // Enum
            if (targetType.IsEnum)
                return Enum.Parse(targetType, element.GetString() ?? element.GetRawText());

            // Listas, arrays y colecciones
            if (targetType.IsGenericType &&
                (targetType.GetGenericTypeDefinition() == typeof(List<>) ||
                 targetType.GetGenericTypeDefinition() == typeof(IList<>) ||
                 targetType.GetGenericTypeDefinition() == typeof(IEnumerable<>)) ||
                (targetType.IsArray))
            {
                Type elementType = targetType.IsArray
                    ? targetType.GetElementType()
                    : targetType.GetGenericArguments()[0];

                var items = new List<object>();
                foreach (var jsonItem in element.EnumerateArray())
                {
                    items.Add(ConvertJsonElement(jsonItem, elementType));
                }

                if (targetType.IsArray)
                {
                    Array array = Array.CreateInstance(elementType, items.Count);
                    for (int i = 0; i < items.Count; i++)
                        array.SetValue(items[i], i);
                    return array;
                }
                else
                {
                    // Crear List<T>
                    Type listType = typeof(List<>).MakeGenericType(elementType);
                    var list = (System.Collections.IList)Activator.CreateInstance(listType);
                    foreach (var item in items)
                        list.Add(item);
                    return list;
                }
            }

            // Diccionarios
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var keyType = targetType.GetGenericArguments()[0];
                var valueType = targetType.GetGenericArguments()[1];
                var dictObj = Activator.CreateInstance(targetType);
                var addMethod = targetType.GetMethod("Add");
                foreach (var property in element.EnumerateObject())
                {
                    var key = Convert.ChangeType(property.Name, keyType);
                    var value = ConvertJsonElement(property.Value, valueType);
                    addMethod.Invoke(dictObj, new[] { key, value });
                }
                return dictObj;
            }

            // Objetos complejos (clases)
            return JsonSerializer.Deserialize(element.GetRawText(), targetType);
        }

        /// <summary>Marcador para evitar advertencias de paquete no utilizado en workers.</summary>
        public static void EnableDynamicJobSupport() { }
    }
}
