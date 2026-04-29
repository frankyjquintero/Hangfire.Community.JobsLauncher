using Hangfire.Community.JobsLauncher.Dashboard;
using Hangfire.Community.JobsLauncher.Dashboard.Models;
using Hangfire.Dashboard;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hangfire.Community.JobsLauncher.Dashboard.Apis
{
    public class HistoryApi : IDashboardDispatcher
    {
        private const string HistoryHashKey = "joblauncher:history";
        private readonly JobLauncherOptions _options;

        public HistoryApi(JobLauncherOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task Dispatch(DashboardContext context)
        {
            var method = context.Request.Method ?? "GET";

            if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                await HandleGetHistory(context);
            }
            else if (method.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
            {
                await HandleClearHistory(context);
            }
            else
            {
                context.Response.StatusCode = 405;
                await WriteJson(context, new { error = "Method Not Allowed" });
            }
        }

        private async Task HandleGetHistory(DashboardContext context)
        {
            var storage = context.Storage;
            using (var connection = storage.GetConnection())
            {
                var allEntries = connection.GetAllEntriesFromHash(HistoryHashKey);
                var entries = new List<HistoryEntry>();

                if (allEntries != null)
                {
                    foreach (var kvp in allEntries.OrderByDescending(x => x.Key))
                    {
                        try
                        {
                            var entry = JsonSerializer.Deserialize<HistoryEntry>(kvp.Value);
                            if (entry != null)
                            {
                                entries.Add(entry);
                            }
                        }
                        catch
                        {
                            // ignora entradas corruptas
                        }
                    }
                }

                // Limitar al máximo configurado
                var result = entries.Take(_options.HistoryMaxEntries).ToList();
                await WriteJson(context, result);
            }
        }

        private async Task HandleClearHistory(DashboardContext context)
        {
            var storage = context.Storage;
            using (var connection = storage.GetConnection())
            {
                using (var transaction = connection.CreateWriteTransaction())
                {
                    transaction.RemoveHash(HistoryHashKey);
                    transaction.Commit();
                }
            }
            await WriteJson(context, new { success = true, message = "Historial volátil borrado." });
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