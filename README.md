# Hangfire.Community.JobsLauncher.Dashboard

A Hangfire Dashboard extension that fills the gap between scheduled automation and ad‑hoc operations. It was born from the need to **launch jobs on demand without redeploying** and to keep a permanent, searchable audit trail of those manual interventions.

---

## ✨ Highlights

| Area | What you can do |
|------|-----------------|
| **Assisted Launch** | Discover classes, methods and parameters via reflection if the business assembly is loaded in the dashboard. |
| **Manual Launch** | Type the class name, method and a JSON payload – no business dependencies required on the dashboard side. |
| **Execution Modes** | Fire & Forget, Schedule (delay or UTC date), Recurring (with Cron expression) and Continuation. |
| **Recurring Engines** | Built‑in (lightweight, uses the dispatcher) or **DynamicJobs** – selectable directly from the UI. |
| **Smart Parameter Editor** | Native inputs for simple types; JSON editor for complex types (lists, dictionaries, objects, enums, nullables). |
| **Cron Generator** | Visual builder with minute / hour / day / month / weekday tabs, preview of next executions and human‑readable description. |
| **Reusable Templates** | Save any job configuration as a named template, load it later, or export/import as JSON files. |
| **History with Pagination** | Browse recent launches with full pagination, relaunch, clone‑and‑launch or save as template. |
| **Immutable Audit Log** | Optional, read‑only log of every launch, independent of the volatile history. Support for filtering by user, date range and pagination. |
| **Critical Queues** | Warns the user when targeting a production queue and asks for explicit confirmation. |
| **Live Preview & Shortcuts** | See a summary of the job before launching and use `Ctrl+Enter` to fire. |
| **Decoupled Execution** | The dashboard never references your business classes directly; a lightweight `JobLauncherDispatcher` is enqueued and resolves the real type at runtime on the worker. |

---

## 🔧 Recent Enhancements

- **Scalable storage**: history and audit log are stored in Hangfire’s native **List** data structure, permitting efficient trimming and pagination without ever loading the entire collection when using a supported storage (SQL Server, Redis, etc.).
- **Pagination** on both history and audit tabs, with configurable page size and prev/next navigation.
- **Audit filters**: search by user, UTC date range and page size; dates are parsed once for performance.
- **Recurring engine per launch**: in assisted mode you can choose Direct, Built‑In or DynamicJobs; in manual mode Built‑In or DynamicJobs. The backend automatically builds the correct expression or dynamic job.
- **Queue awareness**: all non‑recurring jobs are now enqueued to the chosen queue using the `BackgroundJobClient`.
- **Multiple bug fixes** and robustness improvements across the launcher, history and audit APIs.

## 📸 Screenshots

### Launch Tab (Assisted & Manual)

<img width="1919" height="887" alt="image" src="https://github.com/user-attachments/assets/144a8dc8-f9c8-4ef9-89b5-10fb3e170b46" />

<img width="1916" height="870" alt="image" src="https://github.com/user-attachments/assets/687a2a8c-95c2-4694-adbd-2bf73713503e" />
<img width="1884" height="954" alt="image" src="https://github.com/user-attachments/assets/04eff762-df6d-4f1e-a6e7-4a72b1457cdf" />

<img width="1887" height="933" alt="image" src="https://github.com/user-attachments/assets/1224016f-2500-47b2-87a1-4c38346071a1" />
<img width="1824" height="946" alt="image" src="https://github.com/user-attachments/assets/6a30df13-4290-4e31-8f9a-d479afd898ea" />
<img width="1890" height="947" alt="image" src="https://github.com/user-attachments/assets/d7d24092-ce80-40e8-8cc8-5516e3859f76" />
<img width="1881" height="946" alt="image" src="https://github.com/user-attachments/assets/66f44bd0-cf09-4328-893d-c783bfb57d20" />
<img width="948" height="537" alt="image" src="https://github.com/user-attachments/assets/79e50b1f-ba9d-42eb-9edd-3f5b144b0a3a" />

<img width="1045" height="755" alt="image" src="https://github.com/user-attachments/assets/d0b70445-14b2-480b-97f1-c0984dafe55e" />

<img width="1878" height="944" alt="image" src="https://github.com/user-attachments/assets/41868bf2-86e6-425e-9b15-68e85796ed6d" />

<img width="1886" height="944" alt="image" src="https://github.com/user-attachments/assets/42eac170-fc06-49f8-af23-9ec74d84fe9b" />


### History & Templates

<img width="1894" height="938" alt="image" src="https://github.com/user-attachments/assets/3683620d-6349-4197-b1dc-caaf6200a5c3" />

<img width="1872" height="929" alt="image" src="https://github.com/user-attachments/assets/75bf6ad2-7645-4f73-9df7-6c0ab0bab3ab" />

## 📦 Requirements

- Hangfire 1.8.0 or later
- .NET Standard 2.0 (compatible with .NET Core 2.1+ and .NET Framework 4.6.1+)
- **Common library** `Hangfire.Community.JobsLauncher.Common` (required only on workers if manual mode is going to be used)
- Bootstrap 3 and jQuery (already included in the Hangfire dashboard)
- **[Optional]** [Hangfire.DynamicJobs](https://github.com/HangfireIO/Hangfire.DynamicJobs) if you want to use the advanced recurring engine
- 
---
## 📥 Installation

### 1. Dashboard

Add the NuGet package to the project where you configure the dashboard:

```
dotnet add package Hangfire.Community.JobsLauncher.Dashboard
```

### 2. Workers (only if you launch jobs in manual mode)

Install the common library in the worker projects that will execute manually launched jobs:

```
dotnet add package Hangfire.Community.JobsLauncher.Common
```

During worker startup, add this line to avoid "unused package" warnings and clearly document the intent:

```csharp
Hangfire.Community.JobsLauncher.Common.JobLauncherDispatcher.EnableDynamicJobSupport();
```

Workers **do not need the common library** if you only use assisted mode (with assemblies available) or never run manual jobs.

---

## ⚙️ Configuration

In the method where you set up Hangfire (e.g., `Startup.cs`), add:

```csharp
GlobalConfiguration.Configuration.UseJobLauncher(new JobLauncherOptions
{
    CriticalQueues = new List<string> { "production", "critical" },
    HistoryMaxEntries = 50,
    EnableAuditLog = true,
    InheritTheme = true
});
```


## Setup for ASP.NET Core

```csharp
using Hangfire;
using Hangfire.Community.JobsLauncher.Dashboard;

namespace Application
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHangfire(configuration =>
            {
                configuration
                    .UseMemoryStorage()          // or your storage
                    .UseJobLauncher();          // Add the Jobs launcher page
            });

            services.AddHangfireServer();
        }
    }
}
```

## Setup for ASP.NET (.NET Framework)

```csharp
using Hangfire;
using Hangfire.Community.JobsLauncher.Dashboard;

namespace Application
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            GlobalConfiguration.Configuration
                .UseJobLauncher();             // Add the Jobs launcher page

            app.UseHangfireDashboard();
        }
    }
}

```


Register the dashboard in your application:

```csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    // ... your authorization configuration, etc.
});
```

The plugin automatically appears as a new **"Job Launcher"** tab in the navigation menu.

---

## 🚀 Usage

### Launch Tab

1. **Assisted mode** (requires the class to be available in the AppDomain):
   - Enter the full class name (`Namespace.ClassName`) and click **Load Methods**.
   - Pick a method from the list.
   - Fill in the parameters: suitable inputs are generated for simple types (numbers, dates, text), and a JSON editor for complex types.
   - Choose the execution mode: Fire & Forget, Schedule, Recurring, Continuation.
   - Specify the queue (with autocomplete from known queues).
   - Optionally, check *PerformContext* or *CancellationToken*.
   - Click **Launch Job**. If the queue is critical, an additional confirmation will be required.

2. **Manual mode** (without access to the assembly):
   - Manually enter the class name, method, and a JSON payload.
   - Validate and format the JSON using the provided buttons.
   - The rest of the process is identical to assisted mode.
   - For recurring jobs, a dropdown lets you choose between the **Built‑in** engine (lightweight) and **DynamicJobs** (if installed).

### History Tab

- Shows the last launches (up to `HistoryMaxEntries`).
- Each entry offers:
  - **Relaunch**: loads the job parameters into the Launch form.
  - **Clone & Launch**: creates and enqueues an identical copy without modifying the form.
- The **Clear history** button deletes the volatile history (it does not affect the audit log).

### Templates Tab

- Save the current form configuration as a named template.
- Load a template to prefill the form.
- Delete templates you no longer need.
- **Export** a single template as a JSON file.
- **Import** a template from a JSON file (prompting if the name already exists).

---

## 🧱 Internal Architecture

| Project | Description |
|---------|-------------|
| `Hangfire.Community.JobsLauncher.Dashboard` | Dashboard plugin (APIs, Razor page) |
| `Hangfire.Community.JobsLauncher.Common` | Shared library containing `JobLauncherDispatcher` |

### Execution duality

- **If the business class is available** in the dashboard, a `DirectJobInvoker` enqueues the job directly with real types (no dispatcher involved). The worker only needs the business assembly.
- **If the class is not available**, `JobLauncherDispatcher.ExecuteJob(...)` is used, receiving the class name, method, and serialized parameters as JSON. The dispatcher performs the reflection on the worker. The worker requires the common library.

---

## 🔒 Security

- Critical queues can be configured to require explicit confirmation before a job is launched.
- The dashboard inherits the authorization configured in `DashboardOptions`.
- Optionally, assisted mode type discovery can be restricted to allowed assembly prefixes.

---

## 📄 License

This project is distributed under the MIT License. See the [LICENSE](LICENSE) file for details.

---

## 🤝 Contributing

All contributions are welcome. Please open an issue or pull request in the official repository.
