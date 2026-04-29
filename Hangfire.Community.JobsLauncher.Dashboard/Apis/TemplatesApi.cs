using Hangfire.Community.JobsLauncher.Dashboard.Models;
using Hangfire.Dashboard;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hangfire.Community.JobsLauncher.Dashboard.Apis
{
    public class TemplatesApi : IDashboardDispatcher
    {
        private const string TemplatesHashKey = "joblauncher:templates";

        public async Task Dispatch(DashboardContext context)
        {
            var method = context.Request.Method ?? "GET";

            if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                await HandleGet(context);
            }
            else if (method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                await HandlePost(context);
            }
            else if (method.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
            {
                await HandleDelete(context);
            }
            else
            {
                context.Response.StatusCode = 405;
                await WriteJson(context, new { error = "Method Not Allowed" });
            }
        }

        private async Task HandleGet(DashboardContext context)
        {
            var templateName = context.Request.GetQuery("name");

            var storage = context.Storage;
            using (var connection = storage.GetConnection())
            {
                var allEntries = connection.GetAllEntriesFromHash(TemplatesHashKey) ?? new Dictionary<string, string>();

                if (!string.IsNullOrWhiteSpace(templateName))
                {
                    // Devolver una plantilla concreta
                    if (allEntries.TryGetValue(templateName, out var json))
                    {
                        var template = JsonSerializer.Deserialize<JobTemplate>(json);
                        await WriteJson(context, template);
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                        await WriteJson(context, new { error = $"Plantilla '{templateName}' no encontrada." });
                    }
                }
                else
                {
                    // Devolver todas las plantillas como array
                    var templates = allEntries
                        .Select(kvp =>
                        {
                            try
                            {
                                return JsonSerializer.Deserialize<JobTemplate>(kvp.Value);
                            }
                            catch
                            {
                                return null;
                            }
                        })
                        .Where(t => t != null)
                        .ToList();

                    await WriteJson(context, templates);
                }
            }
        }

        private async Task HandlePost(DashboardContext context)
        {
            JobTemplate template = null;
            try
            {
                var formValues = await context.Request.GetFormValuesAsync("json");
                if (formValues == null || formValues.Count == 0) { /* error */ }
                var body = formValues[0];
                template = JsonSerializer.Deserialize<JobTemplate>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (template == null || string.IsNullOrWhiteSpace(template.Name))
                {
                    context.Response.StatusCode = 400;
                    await WriteJson(context, new { error = "El objeto JobTemplate debe tener un campo 'Name' no vacío." });
                    return;
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = $"Error al leer el JSON: {ex.Message}" });
                return;
            }

            var storage = context.Storage;
            using (var connection = storage.GetConnection())
            {
                // Guardar o sobrescribir la plantilla
                var json = JsonSerializer.Serialize(template);
                using (var transaction = connection.CreateWriteTransaction())
                {
                    transaction.SetRangeInHash(TemplatesHashKey, new[] { new KeyValuePair<string, string>(template.Name, json) });
                    transaction.Commit();
                }
            }

            await WriteJson(context, new { success = true, message = $"Plantilla '{template.Name}' guardada correctamente." });
        }

        private async Task HandleDelete(DashboardContext context)
        {
            var templateName = context.Request.GetQuery("name");
            if (string.IsNullOrWhiteSpace(templateName))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "El parámetro 'name' es obligatorio." });
                return;
            }

            var storage = context.Storage;
            using (var connection = storage.GetConnection())
            {
                // Hangfire no permite borrar un campo individual de un hash,
                // así que leemos todas las entradas, quitamos la deseada y reescribimos.
                var allEntries = connection.GetAllEntriesFromHash(TemplatesHashKey) ?? new Dictionary<string, string>();
                if (!allEntries.ContainsKey(templateName))
                {
                    context.Response.StatusCode = 404;
                    await WriteJson(context, new { error = $"Plantilla '{templateName}' no encontrada." });
                    return;
                }

                allEntries.Remove(templateName);

                using (var transaction = connection.CreateWriteTransaction())
                {
                    // Reescribimos todo el hash sin la entrada eliminada.
                    // Primero borramos el hash completo y luego añadimos las entradas restantes.
                    transaction.RemoveHash(TemplatesHashKey);
                    if (allEntries.Count > 0)
                    {
                        transaction.SetRangeInHash(TemplatesHashKey, allEntries.ToArray());
                    }
                    transaction.Commit();
                }
            }

            await WriteJson(context, new { success = true, message = $"Plantilla '{templateName}' eliminada." });
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