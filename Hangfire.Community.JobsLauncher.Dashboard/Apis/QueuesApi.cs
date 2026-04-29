using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Hangfire.Dashboard;
using Hangfire.Storage;

namespace Hangfire.Community.JobsLauncher.Dashboard.Apis
{
    public class QueuesApi : IDashboardDispatcher
    {
        private const string KnownQueuesSetKey = "joblauncher:known-queues";

        public async Task Dispatch(DashboardContext context)
        {
            var storage = context.Storage;
            var queues = Array.Empty<string>();

            try
            {
                using (var connection = storage.GetConnection())
                {
                    var items = connection.GetAllItemsFromSet(KnownQueuesSetKey);
                    if (items != null)
                    {
                        queues = items.OrderBy(q => q).ToArray();
                    }
                }
            }
            catch
            {
                // Si el set no existe o hay cualquier error, devolvemos un array vacío.
                // La UI simplemente no mostrará sugerencias.
            }

            await WriteJson(context, new { queues });
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