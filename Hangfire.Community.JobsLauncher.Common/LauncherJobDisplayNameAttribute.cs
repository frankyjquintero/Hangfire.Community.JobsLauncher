using Hangfire.Common;
using Hangfire.Dashboard;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hangfire.Community.JobsLauncher.Common
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class LauncherJobDisplayNameAttribute : JobDisplayNameAttribute
    {
        public LauncherJobDisplayNameAttribute() : base("Launcher Job") { }

        public override string Format(DashboardContext context, Job job)
        {
            if (job.Args.Count >= 2 &&
                job.Args[0] is string className &&
                job.Args[1] is string methodName)
            {
                string shortClassName = Shorten(className);
                string shortMethodName = Shorten(methodName);
                return $"Launcher: {shortClassName}.{shortMethodName}";
            }
            return base.Format(context, job);
        }

        private static string Shorten(string id) => id?.Length > 60 ? id.Substring(0, 60) : id;
    }
}
