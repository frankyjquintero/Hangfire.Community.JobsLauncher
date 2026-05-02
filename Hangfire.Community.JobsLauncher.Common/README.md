# Hangfire.Community.JobsLauncher.Common

Lightweight dispatcher library for [Hangfire.Community.JobsLauncher](https://github.com/frankyjquintero/Hangfire.Community.JobsLauncher). It allows Hangfire workers to execute jobs launched manually from the dashboard **without requiring business assemblies on the dashboard side**.

---

## 📦 Installation

Add the NuGet package to your worker projects (the ones that process the jobs):

\`\`\`
dotnet add package Hangfire.Community.JobsLauncher.Common
\`\`\`

---

## 🔧 Setup

In the startup code of your worker (e.g., `Program.cs` or `Startup.cs`), call the following method **before** starting the Hangfire server:

\`\`\`csharp
Hangfire.Community.JobsLauncher.Common.JobLauncherDispatcher.EnableDynamicJobSupport();
\`\`\`

This call:

- Documents that the worker supports manually launched jobs.
- Prevents "unused package" warnings.
- No other initialization is required.

> **Note:** If you are using [Hangfire.DynamicJobs](https://github.com/HangfireIO/Hangfire.DynamicJobs) as your recurring engine, make sure that package is also installed and configured in your worker.

---

## 🧱 What It Does

The library contains the `JobLauncherDispatcher` class with two essential methods:

- `ExecuteJob` – the core method that:
  1. Resolves the target type and method at runtime via reflection.
  2. Deserializes the parameters from JSON.
  3. Converts each parameter to the expected .NET type.
  4. Invokes the method with the correct arguments.

- `ConvertJsonElement` – a helper to convert individual JSON elements to complex .NET types (lists, dictionaries, objects, enums, nullables, etc.).

When the dashboard cannot resolve the business class (manual mode), it enqueues a call to `JobLauncherDispatcher.ExecuteJob` with the following serialized data:

- `ClassName`
- `MethodName`
- `Queue`
- Serialized parameters (JSON)
- Flags for `PerformContext` and `CancellationToken`

The worker picks up the job and the dispatcher invokes the real business logic.

---

## 🚦 When Is It Needed?

- ✅ You are using the **manual mode** of the Job Launcher dashboard.
- ✅ You want to keep your dashboard thin and avoid deploying business assemblies to the dashboard host.
- ✅ You run multiple workers that need to execute the same manual jobs.

- ❌ You only use the **assisted mode** (the dashboard has access to the business assembly). In that case the workers only need the business assemblies, **not** this library.

---

## 🔗 Related Packages

| Package | Purpose |
|---------|---------|
| `Hangfire.Community.JobsLauncher.Dashboard` | The dashboard UI and APIs |
| `Hangfire.Community.JobsLauncher.Common` | This one – dispatcher for workers |
| `Hangfire.DynamicJobs` (optional) | Advanced recurring engine for dynamic job types |

---

## 📄 License

This project is distributed under the MIT License. See the [LICENSE](LICENSE) file for details.