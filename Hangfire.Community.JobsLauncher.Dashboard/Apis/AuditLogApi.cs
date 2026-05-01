using Hangfire.Dashboard;
using Hangfire.Community.JobsLauncher.Dashboard.Models;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hangfire.Community.JobsLauncher.Dashboard.Apis
{
    /// <summary>
    /// Endpoint de solo lectura para obtener el log de auditoría.
    /// No se borran entradas al limpiar el historial volátil.
    /// </summary>
    public class AuditLogApi : IDashboardDispatcher
    {
        private const string AuditLogHashKey = "joblauncher:audit-log";

        public async Task Dispatch(DashboardContext context)
        {
            // Obtención de parámetros mediante GetQuery
            var userFilter = context.Request.GetQuery("user");
            var fromParam = context.Request.GetQuery("from");
            var toParam = context.Request.GetQuery("to");
            var countStr = context.Request.GetQuery("count");

            int.TryParse(countStr, out int count);
            if (count <= 0) count = 100; // valor por defecto

            var storage = context.Storage;
            List<HistoryEntry> entries = new List<HistoryEntry>();

            using (var connection = storage.GetConnection())
            {
                var rawEntries = connection.GetAllEntriesFromHash(AuditLogHashKey)
                    ?? new Dictionary<string, string>();

                foreach (var kvp in rawEntries)
                {
                    try
                    {
                        var entry = JsonSerializer.Deserialize<HistoryEntry>(kvp.Value);
                        if (entry != null)
                        {
                            // Aplicar filtros básicos
                            if (!string.IsNullOrEmpty(userFilter) &&
                                !entry.User.Equals(userFilter, StringComparison.OrdinalIgnoreCase))
                                continue;

                            DateTime from = DateTime.MinValue;
                            DateTime to = DateTime.MaxValue;

                            if (!string.IsNullOrWhiteSpace(fromParam) &&
                                DateTimeOffset.TryParse(fromParam, out var fromOffset))
                            {
                                from = fromOffset.UtcDateTime;
                            }

                            if (!string.IsNullOrWhiteSpace(toParam) &&
                                DateTimeOffset.TryParse(toParam, out var toOffset))
                            {
                                to = toOffset.UtcDateTime;
                            }

                            if (entry.Timestamp < from || entry.Timestamp > to)
                                continue;

                            entries.Add(entry);
                        }
                    }
                    catch
                    {
                        // Ignorar entradas corruptas
                    }
                }
            }

            // Ordenar por timestamp descendente (más reciente primero) y limitar cantidad
            var result = entries
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList();

            // Responder con JSON
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}