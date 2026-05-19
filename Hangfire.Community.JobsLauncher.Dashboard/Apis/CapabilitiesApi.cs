using Hangfire.Community.JobsLauncher.Dashboard;
using Hangfire.Dashboard;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hangfire.Community.JobsLauncher.Dashboard.Apis
{
    public class CapabilitiesApi : IDashboardDispatcher
    {
        private readonly JobLauncherOptions _options;

        public CapabilitiesApi(JobLauncherOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task Dispatch(DashboardContext context)
        {
            bool dynamicJobsAvailable = AppDomain.CurrentDomain.GetAssemblies()
                    .Any(a => a.GetType("Hangfire.DynamicJob") != null);

            var response = new
            {
                dynamicJobsAvailable,
                auditLogEnabled = _options.EnableAuditLog,
                criticalQueues = _options.CriticalQueues?.ToArray() ?? new string[0]
            };

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