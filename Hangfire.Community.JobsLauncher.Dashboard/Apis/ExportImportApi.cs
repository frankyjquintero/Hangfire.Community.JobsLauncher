using Hangfire.Community.JobsLauncher.Dashboard.Models;
using Hangfire.Dashboard;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hangfire.Community.JobsLauncher.Dashboard.Apis
{
    public class ExportImportApi : IDashboardDispatcher
    {
        private const string TemplatesHashKey = "joblauncher:templates";

        public async Task Dispatch(DashboardContext context)
        {
            var method = context.Request.Method ?? "GET";

            if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                await HandleExport(context);
            }
            else if (method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                await HandleImport(context);
            }
            else
            {
                context.Response.StatusCode = 405;
                await WriteJson(context, new { error = "Method Not Allowed" });
            }
        }

        private async Task HandleExport(DashboardContext context)
        {
            var templateName = context.Request.GetQuery("templateName");

            if (string.IsNullOrWhiteSpace(templateName))
            {
                context.Response.StatusCode = 400;
                await WriteJson(context, new { error = "El parámetro 'templateName' es obligatorio." });
                return;
            }

            var storage = context.Storage;
            using (var connection = storage.GetConnection())
            {
                var hash = connection.GetAllEntriesFromHash(TemplatesHashKey);
                if (hash == null || !hash.ContainsKey(templateName))
                {
                    context.Response.StatusCode = 404;
                    await WriteJson(context, new { error = $"No se encontró la plantilla '{templateName}'." });
                    return;
                }

                var templateJson = hash[templateName];
                var template = JsonSerializer.Deserialize<JobTemplate>(templateJson);

                // Devolver el objeto JobTemplate
                var json = JsonSerializer.Serialize(template, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var bytes = Encoding.UTF8.GetBytes(json);
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = 200;
                await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
            }
        }

        private async Task HandleImport(DashboardContext context)
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
                    await WriteJson(context, new { error = "El JSON debe contener un objeto JobTemplate con un campo 'Name' no vacío." });
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
                var hash = connection.GetAllEntriesFromHash(TemplatesHashKey) ?? new Dictionary<string, string>();

                if (hash.ContainsKey(template.Name))
                {
                    // Conflicto: ya existe
                    context.Response.StatusCode = 409;
                    await WriteJson(context, new { conflict = true, message = $"La plantilla '{template.Name}' ya existe. ¿Desea sobrescribirla?" });
                    return;
                }

                // Guardar en el hash
                var json = JsonSerializer.Serialize(template);
                using (var transaction = connection.CreateWriteTransaction())
                {
                    transaction.SetRangeInHash(TemplatesHashKey, new[] { new KeyValuePair<string, string>(template.Name, json) });
                    transaction.Commit();
                }

                context.Response.StatusCode = 200;
                await WriteJson(context, new { success = true, message = $"Plantilla '{template.Name}' importada correctamente." });
            }
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