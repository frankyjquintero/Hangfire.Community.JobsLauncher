using System;
using System.Text;
using System.Threading.Tasks;
using Hangfire.Dashboard;
using System.Text.Json;

namespace Hangfire.Community.JobsLauncher.Dashboard.Apis
{
    public class CapabilitiesApi : IDashboardDispatcher
    {
        public async Task Dispatch(DashboardContext context)
        {
            bool dynamicJobsAvailable = Type.GetType(
                "Hangfire.DynamicJobs.DynamicJob, Hangfire.DynamicJobs") != null;

            var response = new { dynamicJobsAvailable };

            await WriteJson(context, response);
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