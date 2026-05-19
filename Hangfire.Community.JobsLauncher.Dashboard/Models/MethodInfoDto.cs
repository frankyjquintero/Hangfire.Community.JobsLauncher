using System;
using System.Collections.Generic;
using System.Text;

namespace Hangfire.Community.JobsLauncher.Dashboard.Models
{
    public static class ParametersNameJobs
    {
        public const string Queue = "OriginalQueue";
    }

    public class MethodInfoDto
    {
        public string MethodName { get; set; }
        public List<ParameterInfoDto> Parameters { get; set; }
    }

    public class ParameterInfoDto
    {
        public string Name { get; set; }
        /// <summary>Nombre completo del tipo, ej: "string", "int", "System.Collections.Generic.List`1[System.String]", "MyApp.Models.Customer"</summary>
        public string Type { get; set; }
        /// <summary>Indica si el tipo es complejo (clase, lista, diccionario, etc.) y requiere editor JSON.</summary>
        public bool IsComplex { get; set; }
        public List<string> EnumValues { get; set; }
    }

    public class LaunchRequest
    {
        public string Mode { get; set; } // "assisted" o "manual"
        public string ClassName { get; set; }
        public string MethodName { get; set; }
        public string Queue { get; set; } = "default";
        public string ExecutionMode { get; set; } // "FireAndForget", "Schedule", "ScheduleDateTime", "Recurring", "Continuation"
        public string CronExpression { get; set; }
        public int? DelayMinutes { get; set; }
        public DateTime? ScheduledDateTime { get; set; }  // UTC
        public string ParentJobId { get; set; }           // para Continuation
        public string RecurringEngine { get; set; }       // "BuiltIn" o "DynamicJobs"
        public Dictionary<string, string> Parameters { get; set; }
        public string RawParametersJson { get; set; }
        public bool IncludePerformContext { get; set; }
        public bool IncludeCancellationToken { get; set; }
    }

    public class LaunchResult
    {
        public bool Success { get; set; }
        public string JobId { get; set; }
        public string Link { get; set; }
        public string Error { get; set; }
    }

    public class HistoryEntry
    {
        public string JobId { get; set; }
        public DateTime Timestamp { get; set; }
        public string ClassName { get; set; }
        public string MethodName { get; set; }
        public string Queue { get; set; }
        public string Mode { get; set; }              // "Assisted" o "Manual"
        public string ExecutionMode { get; set; }
        public string Engine { get; set; }            // "Direct", "BuiltIn", "DynamicJobs"
        public string ParametersJson { get; set; }
        public string User { get; set; }
    }

    public class JobTemplate
    {
        public string Mode { get; set; }
        public string Name { get; set; }
        public string ClassName { get; set; }
        public string MethodName { get; set; }
        public string Queue { get; set; }
        public string ExecutionMode { get; set; }
        public string CronExpression { get; set; }
        public int? DelayMinutes { get; set; }
        public DateTime? ScheduledDateTime { get; set; }
        public string ParentJobId { get; set; }
        public string RecurringEngine { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
        public string RawParametersJson { get; set; }
    }
}
