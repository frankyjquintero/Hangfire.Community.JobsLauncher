using Hangfire.Community.JobLauncher.Dashboard.Pages;
using Hangfire.Community.JobsLauncher.Dashboard.Apis;
using Hangfire.Community.JobsLauncher.Dashboard.Dispatchers;
using Hangfire.Community.JobsLauncher.Dashboard.Filters;
using Hangfire.Dashboard;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Hangfire.Common;
using Hangfire.Dashboard;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Hangfire.Community.JobsLauncher.Dashboard
{
    public class JobLauncherOptions
    {
        /// <summary>Colas que requieren confirmación explícita antes de lanzar.</summary>
        public List<string> CriticalQueues { get; set; } = new List<string> { "production" };

        /// <summary>Máximo de entradas en el historial volátil.</summary>
        public int HistoryMaxEntries { get; set; } = 50;

        /// <summary>Habilitar log de auditoría independiente (no se borra con "Clear history").</summary>
        public bool EnableAuditLog { get; set; } = false;

        /// <summary>Heredar automáticamente el tema visual del dashboard.</summary>
        public bool InheritTheme { get; set; } = true;
    }

    public static class JobLauncherExtensions
    {
        private const string AssetPrefix = "Hangfire.Community.JobsLauncher.Dashboard.Pages";

        public static IGlobalConfiguration UseJobLauncher(
            this IGlobalConfiguration config,
            JobLauncherOptions options = null)
        {
            var opts = options ?? new JobLauncherOptions();

            var asm = typeof(JobLauncherExtensions).GetTypeInfo().Assembly;

            DashboardRoutes.AddJavaScript(asm, $"{AssetPrefix}.Js.Launcher.State.js");
            DashboardRoutes.AddJavaScript(asm, $"{AssetPrefix}.Js.Launcher.Utils.js");
            DashboardRoutes.AddJavaScript(asm, $"{AssetPrefix}.Js.Launcher.Api.js");
            DashboardRoutes.AddJavaScript(asm, $"{AssetPrefix}.Js.Launcher.Params.js");
            DashboardRoutes.AddJavaScript(asm, $"{AssetPrefix}.Js.Launcher.Cron.js");
            DashboardRoutes.AddJavaScript(asm, $"{AssetPrefix}.Js.Launcher.History.js");
            DashboardRoutes.AddJavaScript(asm, $"{AssetPrefix}.Js.Launcher.Templates.js");
            DashboardRoutes.AddJavaScript(asm, $"{AssetPrefix}.Js.Launcher.Audit.js");
            DashboardRoutes.AddJavaScript(asm, $"{AssetPrefix}.Js.Launcher.Init.js");

            DashboardRoutes.AddStylesheet(asm, $"{AssetPrefix}.Css.Launcher.css");

            // APIs
            DashboardRoutes.Routes.Add("/joblauncher/api/capabilities", new CapabilitiesApi(opts));
            DashboardRoutes.Routes.Add("/joblauncher/api/methods", new GetMethodsApi());
            DashboardRoutes.Routes.Add("/joblauncher/api/launch", new LaunchJobApi(opts));
            DashboardRoutes.Routes.Add("/joblauncher/api/validate-cron", new ValidateCronApi());
            DashboardRoutes.Routes.Add("/joblauncher/api/history", new HistoryApi(opts));
            DashboardRoutes.Routes.Add("/joblauncher/api/templates", new TemplatesApi());
            DashboardRoutes.Routes.Add("/joblauncher/api/queues", new QueuesApi());
            DashboardRoutes.Routes.Add("/joblauncher/api/export-import", new ExportImportApi());
            if(opts.EnableAuditLog)
            {
                DashboardRoutes.Routes.Add("/joblauncher/api/audit-log", new AuditLogApi());
            }

            // Página principal
            DashboardRoutes.Routes.AddRazorPage("/joblauncher", x => new JobLauncherPage());

            // Menú de navegación
            NavigationMenu.Items.Add(page => new MenuItem("Job Launcher", page.Url.To("/joblauncher"))
            {
                Active = page.RequestPath.StartsWith("/joblauncher")
            });

            // Filters
            GlobalJobFilters.Filters.Add(new QueueStateFilter());

            return config;
        }
    }
}
