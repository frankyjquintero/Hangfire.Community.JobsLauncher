# Hangfire.Community.JobsLauncher.Dashboard

Plugin para el dashboard de [Hangfire](https://www.hangfire.io/) que permite **lanzar jobs manualmente** desde una interfaz gráfica amigable, con soporte para:

- **Modo asistido**: descubre clases, métodos y parámetros mediante reflexión si la assembly está disponible.
- **Modo manual**: sin necesidad de assemblies de negocio; escribes el tipo, método y parámetros en JSON.
- **Múltiples modos de ejecución**: Fire & Forget, Schedule (con delay o fecha exacta), Recurring (con elección de motor) y Continuation.
- **Motor de recurrentes dual**: Built‑in ligero o DynamicJobs (si está instalado).
- **Editor de parámetros dinámico**: inputs nativos para tipos simples, editor JSON para tipos complejos (listas, clases, diccionarios, nulos, enums…).
- **Plantillas reutilizables**: guarda, carga, exporta e importa configuraciones de jobs por nombre.
- **Historial de lanzamientos**: registro de los últimos jobs lanzados con opción de relanzar o clonar.
- **Validación de Cron**, vista previa del job, enlace directo al job creado, atajos de teclado (`Ctrl+Enter`) y confirmación para colas críticas.
- **Log de auditoría** opcional e independiente del historial volátil.
- **Arquitectura desacoplada**: el dashboard nunca necesita dependencias directas a tus clases de negocio cuando usas el modo manual; un dispatcher ligero (`JobLauncherDispatcher`) se encarga de ejecutar el job en el worker.

---

## 📦 Requisitos

- Hangfire 1.7 o superior
- .NET Standard 2.0 (compatible con .NET Core 2.1+ y .NET Framework 4.6.1+)
- **Librería común** `Hangfire.Community.JobsLauncher.Common` (obligatoria solo para los workers si se va a usar el modo manual)
- Bootstrap 3 y jQuery (ya incluidos en el dashboard de Hangfire)
- **[Opcional]** [Hangfire.DynamicJobs](https://github.com/HangfireIO/Hangfire.DynamicJobs) si se desea usar el motor avanzado de recurrentes

---

## 📥 Instalación

### 1. Dashboard

Agrega el paquete NuGet al proyecto donde configuras el dashboard:

\`\`\`
dotnet add package Hangfire.Community.JobsLauncher.Dashboard
\`\`\`

### 2. Workers (solo si se lanzan jobs en modo manual)

Instala la librería común en los proyectos worker que vayan a ejecutar jobs lanzados manualmente:

\`\`\`
dotnet add package Hangfire.Community.JobsLauncher.Common
\`\`\`

En el código de arranque del worker, añade esta línea para evitar advertencias de paquete no utilizado y documentar la intención:

\`\`\`csharp
Hangfire.Community.JobsLauncher.Common.JobLauncherDispatcher.EnableDynamicJobSupport();
\`\`\`

Los workers **no necesitan la librería común** si solo se usa el modo asistido (con assemblies disponibles) o si nunca se ejecutarán jobs manuales.

---

## ⚙️ Configuración

En el método donde configuras Hangfire (por ejemplo, `Startup.cs`), añade:

\`\`\`csharp
GlobalConfiguration.Configuration.UseJobLauncher(new JobLauncherOptions
{
    CriticalQueues = new List<string> { "production", "critical" },
    HistoryMaxEntries = 50,
    EnableAuditLog = true,
    InheritTheme = true
});
\`\`\`

Registra el dashboard en la aplicación:

\`\`\`csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    // ... tu configuración de autorización, etc.
});
\`\`\`

El plugin aparecerá automáticamente como una nueva pestaña **"Job Launcher"** en el menú de navegación.

---

## 🚀 Uso

### Pestaña Launch

1. **Modo Assisted** (requiere que la clase esté disponible en el AppDomain):
   - Escribe el nombre completo de la clase (`Namespace.ClassName`) y pulsa **Load Methods**.
   - Selecciona un método de la lista.
   - Completa los parámetros: se generan inputs adecuados para tipos simples (números, fechas, texto) y un editor JSON para tipos complejos.
   - Elige el modo de ejecución: Fire & Forget, Schedule, Recurring, Continuation.
   - Especifica la cola (con autocompletado de colas conocidas).
   - Opcionalmente, marca las casillas *PerformContext* o *CancellationToken*.
   - Pulsa **Launch Job**. Si la cola es crítica, se pedirá confirmación adicional.

2. **Modo Manual** (sin acceso a la assembly):
   - Introduce manualmente el nombre de la clase, el método y los parámetros en formato JSON.
   - Valida y formatea el JSON con los botones correspondientes.
   - El resto del proceso es idéntico al modo asistido.
   - Para jobs recurrentes, aparecerá un selector para elegir entre el motor **Built‑in** (ligero) o **DynamicJobs** (si está instalado).
   
### Pestaña History

- Muestra los últimos lanzamientos (hasta `HistoryMaxEntries`).
- Cada entrada ofrece:
  - **Relaunch**: carga los datos en el formulario de Launch.
  - **Clone & Launch**: lanza una copia exacta sin modificar el formulario.
- El botón **Clear history** borra el historial volátil (no afecta al log de auditoría).

### Pestaña Templates

- Guarda la configuración actual del formulario como plantilla (nombre único).
- Carga una plantilla para precargar el formulario.
- Elimina plantillas que ya no necesites.
- **Exporta** una plantilla individual como archivo JSON.
- **Importa** una plantilla desde un archivo JSON (con aviso si el nombre ya existe).

---

## 🧱 Arquitectura interna

| Proyecto | Descripción |
|----------|-------------|
| `Hangfire.Community.JobsLauncher.Dashboard` | Plugin del dashboard (APIs, página Razor) |
| `Hangfire.Community.JobsLauncher.Common` | Librería compartida con `JobLauncherDispatcher` |

### Dualidad de ejecución

- **Si la clase de negocio está disponible** en el dashboard, se usa `DirectJobInvoker` para encolar el job directamente con los tipos reales (sin dispatcher). El worker solo necesita la assembly de negocio.
- **Si la clase no está disponible**, se usa `JobLauncherDispatcher.ExecuteJob(...)`, que recibe el nombre de la clase, método y parámetros serializados en JSON. El dispatcher se encarga de la reflexión en el worker. El worker necesita la librería común.

---

## 🔒 Seguridad

- Las colas críticas se pueden configurar para que requieran confirmación explícita antes del lanzamiento.
- El dashboard hereda la autorización configurada en `DashboardOptions`.
- Opcionalmente, se puede restringir la búsqueda de tipos en el modo asistido a prefijos de ensamblado permitidos.

---

## 📄 Licencia

Este proyecto se distribuye bajo la licencia MIT. Consulta el archivo [LICENSE](LICENSE) para más detalles.

---

## 🤝 Contribuciones

Toda contribución es bienvenida. Por favor, abre un issue o un pull request en el repositorio oficial.

---
