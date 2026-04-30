using Hangfire.Community.JobsLauncher.Dashboard.Models;
using Hangfire.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hangfire.Community.JobsLauncher.Dashboard.Apis
{
    public static class TypeResolver
    {
        /// <summary>
        /// Busca un tipo en todos los ensamblados cargados.
        /// </summary>
        public static Type FindType(string typeName)
        {
            // Primero intenta la búsqueda estándar
            var type = Type.GetType(typeName);
            if (type != null) return type;

            // Luego busca en todos los ensamblados del dominio
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(asm => asm.GetType(typeName))
                .FirstOrDefault(t => t != null);
        }
    }

    public class GetMethodsApi : IDashboardDispatcher
    {
        // Tipos considerados "simples" para generar inputs nativos en la UI
        private static readonly HashSet<Type> SimpleTypes = new HashSet<Type>
        {
            typeof(string),
            typeof(int),
            typeof(long),
            typeof(short),
            typeof(byte),
            typeof(bool),
            typeof(double),
            typeof(float),
            typeof(decimal),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(Guid),
            typeof(TimeSpan)
        };

        public async Task Dispatch(DashboardContext context)
        {
            var className = context.Request.GetQuery("className");
            if (string.IsNullOrWhiteSpace(className))
            {
                await WriteJson(context, new { success = false, error = "className es obligatorio." });
                return;
            }

            try
            {
                Type type = TypeResolver.FindType(className);
                if (type == null)
                    throw new Exception("Tipo no encontrado en ningún ensamblado.");

                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(m => m.ReturnType == typeof(void) || m.ReturnType == typeof(Task) || m.ReturnType.IsSubclassOf(typeof(Task)))
                    .Where(m => !m.IsSpecialName)
                    .Select(m => new MethodInfoDto
                    {
                        MethodName = m.Name,
                        Parameters = m.GetParameters().Select(p => new ParameterInfoDto
                        {
                            Name = p.Name,
                            Type = GetTypeName(p.ParameterType),
                            IsComplex = IsComplexType(p.ParameterType)
                        }).ToList()
                    }).ToList();

                await WriteJson(context, new { success = true, methods });
            }
            catch (Exception)
            {
                await WriteJson(context, new { success = false, error = "Assembly no disponible. Usa el modo manual." });
            }
        }

        private static string GetTypeName(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return GetTypeName(Nullable.GetUnderlyingType(type)) + "?";
            }
            if (type.IsGenericType)
            {
                // Ej: List<string> -> "System.Collections.Generic.List<System.String>"
                string genericName = type.Name.Split('`')[0];
                string args = string.Join(", ", type.GetGenericArguments().Select(GetTypeName));
                return $"{type.Namespace}.{genericName}<{args}>";
            }
            if (type == typeof(void)) return "void";
            if (type == typeof(Task)) return "Task";
            return type.FullName ?? type.Name;
        }

        private static bool IsComplexType(Type type)
        {
            // Nullable<T> se considera simple si el subyacente es simple
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return IsComplexType(Nullable.GetUnderlyingType(type));

            // Enumeraciones se consideran simples (manejamos con string)
            if (type.IsEnum) return false;

            // Si está en la lista de tipos simples, es simple
            if (SimpleTypes.Contains(type)) return false;

            // Si es Task o derivados (no debería llegar aquí un parámetro, pero por si acaso)
            if (typeof(Task).IsAssignableFrom(type)) return false;

            // Cualquier otro tipo se considera complejo (clases, listas, diccionarios, etc.)
            return true;
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