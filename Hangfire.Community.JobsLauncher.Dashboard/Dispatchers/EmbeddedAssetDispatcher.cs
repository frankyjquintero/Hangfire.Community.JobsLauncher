using Hangfire.Dashboard;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Hangfire.Community.JobsLauncher.Dashboard.Dispatchers
{
    internal sealed class EmbeddedAssetDispatcher : IDashboardDispatcher
    {
        private readonly Assembly _assembly;
        private readonly string _resourceName;
        private readonly string _contentType;

        public EmbeddedAssetDispatcher(Assembly assembly, string resourceName, string contentType)
        {
            _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            _resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
            _contentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        }

        public async Task Dispatch(DashboardContext context)
        {
            using (var stream = _assembly.GetManifestResourceStream(_resourceName))
            {
                if (stream == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                context.Response.ContentType = _contentType;

                // Copiado manual para mantener compatibilidad con .NET Standard 2.0
                var buffer = new byte[16 * 1024];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    await context.Response.Body.WriteAsync(buffer, 0, read);
                }
            }
        }

        public static EmbeddedAssetDispatcher Css(Assembly assembly, string resourceName)
            => new EmbeddedAssetDispatcher(assembly, resourceName, "text/css; charset=utf-8");

        public static EmbeddedAssetDispatcher JavaScript(Assembly assembly, string resourceName)
            => new EmbeddedAssetDispatcher(assembly, resourceName, "application/javascript; charset=utf-8");
    }
}
