using Hangfire;
using Hangfire.Server;
using Hangfire.States;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Hangfire.APMJobs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly IBackgroundJobClient _backgroundJobClient;

        public JobsController(IBackgroundJobClient backgroundJobClient)
        {
            _backgroundJobClient = backgroundJobClient;
        }

        /// <summary>
        /// Dispara un job fire-and-forget simple en la cola especificada.
        /// </summary>
        [HttpPost("fire-and-forget")]
        public IActionResult FireAndForget(string queue = "default")
        {
            _backgroundJobClient.Enqueue(queue, () => Console.WriteLine($"Job fire-and-forget ejecutado a las {DateTime.Now} en la cola {queue}"));
            return Ok("Job fire-and-forget encolado");
        }

        /// <summary>
        /// Dispara los 17 jobs de simulación en una sola llamada.
        /// </summary>
        [HttpPost("simulate-all")]
        public IActionResult SimulateAll()
        {
            var jobs = new List<string>();
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.SendWelcomeEmail()));
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.ProcessOrderPayment()));
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.GenerateMonthlyReport()));
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.CleanupExpiredTokens()));
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.ImportCustomerData()));
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.SyncProductCatalog()));
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.CreateBackup()));
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.ValidateEmailAddresses()));
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.UpdateExchangeRates()));
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.PurgeOldLogs()));
            // Los siguientes fallarán a propósito
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.FailingJob_ProcessRefund()));
            //jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.FailingJob_ChargeCreditCard()));
            //jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.FailingJob_UpdateInventory()));
            // Jobs con algo de demora para aparecer como "Processing"
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.SlowJob_GenerateInvoice()));
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.SlowJob_ResizeImages()));
            // Más jobs exitosos
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.NotifyUsersAboutDowntime()));
            jobs.Add(_backgroundJobClient.Enqueue(() => SimulatedTasks.RebuildSearchIndex()));

            return Ok(new { message = "17 jobs encolados", jobIds = jobs });
        }

        /// <summary>
        /// Programa un job recurrente (cada minuto).
        /// </summary>
        [HttpPost("recurrente")]
        public IActionResult Recurrente()
        {
            RecurringJob.AddOrUpdate("job-recurrente", () => Console.WriteLine($"Job recurrente ejecutado a las {DateTime.Now}"), Cron.Minutely);
            return Ok("Job recurrente programado (cada minuto)");
        }
    }

    public static class SimulatedTasks
    {
        private static readonly Random _random = new Random();

        public static void SendWelcomeEmail()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            Console.WriteLine($"[{DateTime.Now}] Welcome email sent.");
        }
        public static void ProcessOrderPayment()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            Console.WriteLine($"[{DateTime.Now}] Order payment processed.");
        }
        public static void GenerateMonthlyReport()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            Console.WriteLine($"[{DateTime.Now}] Monthly report generated.");
        }
        public static void CleanupExpiredTokens()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            Console.WriteLine($"[{DateTime.Now}] Expired tokens cleaned up.");
        }
        public static void ImportCustomerData()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            Console.WriteLine($"[{DateTime.Now}] Customer data imported.");
        }
        public static void SyncProductCatalog()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            Console.WriteLine($"[{DateTime.Now}] Product catalog synced.");
        }
        public static void CreateBackup()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            Console.WriteLine($"[{DateTime.Now}] Backup created successfully.");
        }
        public static void ValidateEmailAddresses()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            Console.WriteLine($"[{DateTime.Now}] Email addresses validated.");
        }
        public static void UpdateExchangeRates()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            Console.WriteLine($"[{DateTime.Now}] Exchange rates updated.");
        }
        public static void PurgeOldLogs()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            Console.WriteLine($"[{DateTime.Now}] Old logs purged.");
        }

        // Jobs que fallan
        public static void FailingJob_ProcessRefund()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            Console.WriteLine($"[{DateTime.Now}] ProcessRefund.");
        }
        public static void FailingJob_ChargeCreditCard()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            throw new Exception("Credit card charge declined.");
        }
        public static void FailingJob_UpdateInventory()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            throw new InvalidOperationException("Inventory update failed: insufficient stock.");
        }

        // Jobs lentos (mantienen sus tiempos fijos)
        public static void SlowJob_GenerateInvoice()
        {
            Thread.Sleep(3000);
            Console.WriteLine($"[{DateTime.Now}] Invoice generated after delay.");
        }
        public static void SlowJob_ResizeImages()
        {
            Thread.Sleep(5000);
            Console.WriteLine($"[{DateTime.Now}] Images resized after delay.");
        }

        // Más jobs exitosos
        public static void NotifyUsersAboutDowntime()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            Console.WriteLine($"[{DateTime.Now}] Users notified about planned downtime.");
        }
        public static void RebuildSearchIndex()
        {
            Thread.Sleep(_random.Next(1000, 5000));
            Console.WriteLine($"[{DateTime.Now}] Search index rebuilt.");
        }

        /// <summary>
        /// Parámetros: tipos primitivos (string, int, bool, double)
        /// JSON ejemplo: {"customerName":"John","age":30,"isPremium":true,"balance":1200.5}
        /// </summary>
        public static void ProcessCustomer(string customerName, int age, bool isPremium, double balance)
        {
            Thread.Sleep(1000);
            Console.WriteLine($"[{DateTime.Now}] Processing customer {customerName}, age {age}, premium: {isPremium}, balance: {balance:C}");
        }

        /// <summary>
        /// Parámetros: DateTime + tipos anulables (int?, DateTime?)
        /// JSON ejemplo: {"startDate":"2025-03-15T00:00:00","endDate":null,"maxRetries":null}
        /// </summary>
        public static void GenerateReport(DateTime startDate, DateTime? endDate, int? maxRetries)
        {
            Thread.Sleep(1000);
            var end = endDate?.ToString("d") ?? "today";
            var retries = maxRetries?.ToString() ?? "unlimited";
            Console.WriteLine($"[{DateTime.Now}] Report from {startDate:d} to {end}, max retries: {retries}");
        }

        /// <summary>
        /// Parámetros: Enum + DateTime + Guid
        /// JSON ejemplo: {"priority":"High","notifyAt":"2025-04-01T09:30:00","trackingId":"a1b2c3d4-..."}
        /// </summary>
        public enum JobPriority { Low, Medium, High, Critical }

        public static void ScheduleWork(JobPriority priority, DateTime notifyAt, Guid trackingId)
        {
            Thread.Sleep(1000);
            Console.WriteLine($"[{DateTime.Now}] Scheduled work - Priority: {priority}, Notify at: {notifyAt:g}, TrackingId: {trackingId}");
        }

        /// <summary>
        /// Parámetros: Lista de strings, Dictionary, TimeSpan
        /// JSON ejemplo: 
        /// {
        ///   "emailAddresses": ["a@b.com","c@d.com"],
        ///   "metadata": {"key1":"value1","key2":"value2"},
        ///   "timeout": "00:05:00"
        /// }
        /// </summary>
        public static void SendBulkEmails(List<string> emailAddresses, Dictionary<string, string> metadata, TimeSpan timeout)
        {
            Thread.Sleep(1000);
            Console.WriteLine($"[{DateTime.Now}] Sending emails to {emailAddresses.Count} recipients, timeout {timeout}. Metadata keys: {string.Join(",", metadata.Keys)}");
        }

        /// <summary>
        /// Parámetros: clase compleja + lista de enteros
        /// JSON ejemplo: 
        /// {
        ///   "config": {"Server":"smtp.example.com","Port":587,"UseSsl":true},
        ///   "attachmentIds": [101, 102, 103]
        /// }
        /// </summary>
        public class EmailConfig
        {
            public string Server { get; set; } = string.Empty;
            public int Port { get; set; }
            public bool UseSsl { get; set; }
        }

        public static void SendEmailWithAttachments(EmailConfig config, List<int> attachmentIds)
        {
            Thread.Sleep(1000);
            Console.WriteLine($"[{DateTime.Now}] Email via {config.Server}:{config.Port} (SSL:{config.UseSsl}), attachments: {attachmentIds.Count}");
        }

        /// <summary>
        /// Parámetros: array de objetos complejos + enum anulable
        /// JSON ejemplo:
        /// {
        ///   "logEntries": [{"Message":"Started","Timestamp":"2025-01-01T00:00:00"},{"Message":"Done","Timestamp":"2025-01-01T01:00:00"}],
        ///   "severity": "Medium"
        /// }
        /// </summary>
        public class LogEntry
        {
            public string Message { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }

        public static void BatchLogWrites(LogEntry[] logEntries, JobPriority? severity)
        {
            Thread.Sleep(1000);
            string sev = severity?.ToString() ?? "not specified";
            Console.WriteLine($"[{DateTime.Now}] Writing {logEntries.Length} log entries, severity: {sev}");
        }

        /// <summary>
        /// Método que acepta PerformContext y IJobCancellationToken (para probar las opciones avanzadas)
        /// </summary>
        public static void LongRunningTaskWithCancellation(PerformContext context, IJobCancellationToken token)
        {
            for (int i = 0; i < 60; i++)
            {
                token.ThrowIfCancellationRequested();
                Thread.Sleep(1000);
            }
            Console.WriteLine($"[{DateTime.Now}] Long task completed.");
        }
    }
}