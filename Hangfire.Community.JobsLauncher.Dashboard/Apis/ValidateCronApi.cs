using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Cronos;
using Hangfire.Dashboard;

namespace Hangfire.Community.JobsLauncher.Dashboard.Apis
{
    public class ValidateCronApi : IDashboardDispatcher
    {
        public async Task Dispatch(DashboardContext context)
        {
            //var expression = context.Request.GetQuery("expression");
            //if (string.IsNullOrWhiteSpace(expression))
            //{
            //    await WriteJson(context, new { success = false, error = "El parámetro 'expression' es obligatorio." });
            //    return;
            //}

            //try
            //{
            //    CronExpression cronExpression = CronExpression.Parse(expression);
            //    var now = DateTime.UtcNow;
            //    var occurrences = new List<string>();

            //    DateTime? next = now;
            //    for (int i = 0; i < 5; i++)
            //    {
            //        next = cronExpression.GetNextOccurrence(next.Value, inclusive: false);
            //        if (next.HasValue)
            //        {
            //            occurrences.Add(next.Value.ToString("O")); // formato ISO 8601
            //        }
            //        else
            //        {
            //            break;
            //        }
            //    }

            //    await WriteJson(context, new { success = true, occurrences });
            //}
            //catch (CronFormatException ex)
            //{
            //    await WriteJson(context, new { success = false, error = $"Expresión cron inválida: {ex.Message}" });
            //}
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