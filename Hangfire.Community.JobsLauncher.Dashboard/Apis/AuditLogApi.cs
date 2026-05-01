using Hangfire.Community.JobsLauncher.Dashboard.Models;
using Hangfire.Dashboard;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hangfire.Community.JobsLauncher.Dashboard.Apis
{
    /// <summary>
    /// Endpoint de solo lectura para obtener el log de auditoría (inmutable).
    /// Usa una lista para permitir paginación real y mantener los datos aunque se limpie el historial.
    /// </summary>
    public class AuditLogApi : IDashboardDispatcher
    {
        private const string AuditLogListKey = "joblauncher:audit-log-list";

        public async Task Dispatch(DashboardContext context)
        {
            // Parámetros
            var userFilter = context.Request.GetQuery("user");
            var fromParam = context.Request.GetQuery("from");
            var toParam = context.Request.GetQuery("to");
            var pageParam = context.Request.GetQuery("page");
            var pageSizeParam = context.Request.GetQuery("pageSize");

            int page = int.TryParse(pageParam, out int p) && p > 0 ? p : 1;
            int pageSize = int.TryParse(pageSizeParam, out int ps) && ps > 0 ? ps : 20;

            DateTime? fromUtc = null;
            DateTime? toUtc = null;

            if (!string.IsNullOrEmpty(fromParam) &&
                DateTime.TryParse(fromParam, null, DateTimeStyles.AdjustToUniversal, out DateTime parsedFrom))
            {
                fromUtc = parsedFrom;
            }

            if (!string.IsNullOrEmpty(toParam) &&
                DateTime.TryParse(toParam, null, DateTimeStyles.AdjustToUniversal, out DateTime parsedTo))
            {
                toUtc = parsedTo;
            }


            var storage = context.Storage;
            List<HistoryEntry> allEntries = new List<HistoryEntry>();

            using (var connection = (JobStorageConnection)storage.GetConnection())
            {
                // Obtener todos los elementos de la lista (la operación es rápida)
                var rawItems = connection.GetAllItemsFromList(AuditLogListKey);
                if (rawItems != null)
                {
                    foreach (var json in rawItems)
                    {
                        try
                        {
                            var entry = JsonSerializer.Deserialize<HistoryEntry>(json);
                            if (entry != null)
                            {
                                // Filtro por usuario (insensible a mayúsculas/minúsculas)
                                if (!string.IsNullOrEmpty(userFilter) &&
                                    !entry.User.Equals(userFilter, StringComparison.OrdinalIgnoreCase))
                                    continue;

                                // Filtro por rango de fechas (desde/hasta)
                                if (fromUtc.HasValue && entry.Timestamp < fromUtc.Value)
                                    continue;

                                if (toUtc.HasValue && entry.Timestamp > toUtc.Value)
                                    continue;

                                allEntries.Add(entry);
                            }
                        }
                        catch
                        {
                            // Ignorar entradas corruptas
                        }
                    }
                }
            }

            // Ordenar por timestamp descendente (más reciente primero)
            var filteredEntries = allEntries.OrderByDescending(e => e.Timestamp).ToList();
            int total = filteredEntries.Count;

            // Aplicar paginación
            var pagedEntries = filteredEntries
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Responder
            var response = new
            {
                items = pagedEntries,
                total,
                page,
                pageSize
            };

            var jsonResult = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var bytes = Encoding.UTF8.GetBytes(jsonResult);

            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}