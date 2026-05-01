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
        private const string HistoryListKey = "joblauncher:history-list";
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
            var pageParam = context.Request.GetQuery("page");
            var pageSizeParam = context.Request.GetQuery("pageSize");

            int page = int.TryParse(pageParam, out int p) ? p : 1;
            int pageSize = int.TryParse(pageSizeParam, out int ps) ? ps : 20;

            var storage = context.Storage;
            using (var connection = (JobStorageConnection)storage.GetConnection())
            {
                long total = connection.GetListCount(HistoryListKey);
                int startIndex = (int)((page - 1) * pageSize);
                int endIndex = (int)Math.Min(startIndex + pageSize - 1, total - 1);

                if (startIndex >= total)
                {
                    await WriteJson(context, new { items = new List<HistoryEntry>(), total, page, pageSize });
                    return;
                }

                // Obtener solo el rango necesario (los más recientes están al final de la lista)
                var range = connection.GetRangeFromList(HistoryListKey, startIndex, endIndex);

                var entries = new List<HistoryEntry>();
                foreach (var json in range)
                {
                    try
                    {
                        var entry = JsonSerializer.Deserialize<HistoryEntry>(json);
                        if (entry != null) entries.Add(entry);
                    }
                    catch { }
                }

                await WriteJson(context, new { items = entries, total, page, pageSize });
            }
        }

        private async Task HandleClearHistory(DashboardContext context)
        {
            var storage = context.Storage;
            using (var connection = storage.GetConnection())
            {
                using (var transaction = (JobStorageTransaction)connection.CreateWriteTransaction())
                {
                    transaction.TrimList(HistoryListKey, 0, - 1);
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