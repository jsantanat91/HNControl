using System.Diagnostics;
using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

/// <summary>On-demand, read-only diagnostics. No probes write files or contact external integrations.</summary>
public sealed class SystemHealthReader(ApplicationDbContext db, IConfiguration configuration,
    IWebHostEnvironment environment, ILogger<SystemHealthReader> logger)
{
    public record Check(string Name, string Description, string Status, bool? Healthy);
    public sealed class Snapshot
    {
        public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;
        public TimeSpan? Uptime { get; set; }
        public long? ProcessMemoryBytes { get; set; }
        public int AvailableProcessors { get; set; } = Environment.ProcessorCount;
        public long? DiskAvailableBytes { get; set; }
        public long? DiskTotalBytes { get; set; }
        public List<Check> Checks { get; } = new();
    }

    public async Task<Snapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var result = new Snapshot();
        try
        {
            using var process = Process.GetCurrentProcess();
            result.Uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();
            result.ProcessMemoryBytes = process.WorkingSet64;
            result.Checks.Add(new("Aplicación web", "El proceso de HN Control está atendiendo esta consulta.", "En ejecución", true));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudieron consultar métricas del proceso");
            result.Checks.Add(new("Aplicación web", "Las métricas del proceso no están disponibles en este entorno.", "Sin medición", null));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var connected = await db.Database.CanConnectAsync(timeout.Token);
            result.Checks.Add(new("Base de datos", "Verificación de conexión; no modifica tablas ni registros.",
                connected ? "Conexión disponible" : "Sin conexión", connected));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "No se pudo consultar la conexión en salud del sistema");
            result.Checks.Add(new("Base de datos", "No se pudo confirmar la conexión dentro del tiempo de consulta.", "No disponible", false));
        }

        try
        {
            // LocalFileStorage resolves relative paths against the process working directory.
            var storagePath = Path.GetFullPath(configuration["Storage:BasePath"] ?? "App_Data/uploads");
            if (Directory.Exists(storagePath))
            {
                using var entries = Directory.EnumerateFileSystemEntries(storagePath).GetEnumerator();
                _ = entries.MoveNext();
                result.Checks.Add(new("Archivos y documentos", "La carpeta configurada es accesible en lectura. No se prueba escritura.", "Lectura disponible", true));
            }
            else result.Checks.Add(new("Archivos y documentos", "La carpeta configurada no existe o no es accesible.", "No disponible", false));

            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var drive = DriveInfo.GetDrives().Where(d => d.IsReady &&
                (string.Equals(storagePath, d.RootDirectory.FullName.TrimEnd(Path.DirectorySeparatorChar), comparison)
                 || storagePath.StartsWith(Path.EndsInDirectorySeparator(d.RootDirectory.FullName)
                     ? d.RootDirectory.FullName : d.RootDirectory.FullName + Path.DirectorySeparatorChar, comparison)))
                .OrderByDescending(d => d.RootDirectory.FullName.Length).FirstOrDefault();
            if (drive != null)
            {
                result.DiskTotalBytes = drive.TotalSize;
                result.DiskAvailableBytes = drive.AvailableFreeSpace;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo consultar el almacenamiento en salud del sistema");
            if (!result.Checks.Any(x => x.Name == "Archivos y documentos"))
                result.Checks.Add(new("Archivos y documentos", "No se pudo comprobar el acceso a la carpeta configurada.", "Sin medición", null));
        }

        var templates = new[] { "Contrato_Blanco.docx", "NDA.docx", "Acta_Entrega_Material_Servicios.docx" };
        var templatesAvailable = templates.All(name => File.Exists(Path.Combine(environment.ContentRootPath, "assets", "legal-templates", name)));
        result.Checks.Add(new("Plantillas de documentos", "Presencia de las plantillas de contrato, NDA y entrega. No realiza conversiones.",
            templatesAvailable ? "3 plantillas disponibles" : "Revisar plantillas", templatesAvailable));
        return result;
    }

    public static string FormatBytes(long? bytes) => bytes is null ? "Sin medición"
        : bytes >= (1L << 30) ? $"{bytes.Value / (double)(1L << 30):N1} GB" : $"{bytes.Value / (double)(1L << 20):N0} MB";
    public static string FormatUptime(TimeSpan? value) => value is null ? "Sin medición"
        : value.Value.TotalDays >= 1 ? $"{(int)value.Value.TotalDays} d {value.Value.Hours} h"
        : $"{(int)value.Value.TotalHours} h {value.Value.Minutes} min";
}
