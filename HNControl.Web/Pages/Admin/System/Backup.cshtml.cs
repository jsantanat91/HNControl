using global::System.Diagnostics;
using SystemAlias = global::System;
using IO = global::System.IO;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace HNControl.Web.Pages.Admin.SystemPages;

[Authorize(Roles = AppRoles.Admin)]
public class BackupModel : PageModel
{
    private readonly IConfiguration _cfg;
    private readonly IWebHostEnvironment _env;

    public BackupModel(IConfiguration cfg, IWebHostEnvironment env)
    {
        _cfg = cfg;
        _env = env;
    }

    [TempData] public string? Error { get; set; }
    [TempData] public string? Info { get; set; }
    [BindProperty] public IFormFile? RestoreFile { get; set; }

    public void OnGet()
    {
        Info = "El respaldo usa pg_dump del servidor y genera SQL plano.";
    }

    public async Task<IActionResult> OnPostDownloadAsync()
    {
        var conn = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(conn))
        {
            Error = "No se encontró una cadena de conexión válida (DefaultConnection / Default / Postgres).";
            return Page();
        }

        var cs = new NpgsqlConnectionStringBuilder(conn);
        var pgDump = await ResolvePgDumpPathAsync();
        if (string.IsNullOrWhiteSpace(pgDump) || (!IsCommandName(pgDump) && !IO.File.Exists(pgDump)))
        {
            Error = "No se encontró pg_dump en el servidor. Instala PostgreSQL client tools o configura Database:PgDumpPath.";
            return Page();
        }

        var host = string.IsNullOrWhiteSpace(cs.Host) ? "localhost" : cs.Host;
        var port = cs.Port <= 0 ? 5432 : cs.Port;
        var user = cs.Username ?? string.Empty;
        var db = cs.Database ?? string.Empty;
        var pass = cs.Password ?? string.Empty;
        var sslMode = cs.SslMode.ToString().ToLowerInvariant();

        var userEsc = Uri.EscapeDataString(user);
        var passEsc = Uri.EscapeDataString(pass);
        var hostEsc = Uri.EscapeDataString(host);
        var dbEsc = Uri.EscapeDataString(db);
        var dbUri = $"postgresql://{userEsc}:{passEsc}@{hostEsc}:{port}/{dbEsc}?sslmode={sslMode}";

        var psi = new ProcessStartInfo
        {
            FileName = pgDump,
            Arguments = $"--dbname=\"{dbUri}\" --format=plain --no-owner --no-privileges --encoding=UTF8",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        await using var ms = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(ms);
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            Error = "Error al generar respaldo: " + (string.IsNullOrWhiteSpace(stderr) ? "pg_dump devolvió código no exitoso." : stderr.Trim());
            return Page();
        }

        var fileName = $"hncontrol_backup_{SystemAlias.DateTime.Now:yyyyMMdd_HHmmss}.sql";
        return File(ms.ToArray(), "application/sql", fileName);
    }

    private async Task<string?> ResolvePgDumpPathAsync()
    {
        var configured = (_cfg["Database:PgDumpPath"] ?? SystemAlias.Environment.GetEnvironmentVariable("PG_DUMP_PATH") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(configured) && (IO.File.Exists(configured) || IsCommandName(configured)))
            return configured;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "where" : "which",
                Arguments = "pg_dump",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = new Process { StartInfo = psi };
            p.Start();
            var output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            var first = output.Split(new[] { '\r', '\n' }, SystemAlias.StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(x => IO.File.Exists(x));
            if (!string.IsNullOrWhiteSpace(first)) return first;
        }
        catch { }

        var fromPath = FindInPath(OperatingSystem.IsWindows() ? "pg_dump.exe" : "pg_dump");
        if (!string.IsNullOrWhiteSpace(fromPath)) return fromPath;

        var candidates = new List<string>();
        if (!OperatingSystem.IsWindows())
        {
            candidates.Add("/usr/bin/pg_dump");
            candidates.Add("/usr/local/bin/pg_dump");
            candidates.Add("/usr/lib/postgresql/16/bin/pg_dump");
            candidates.Add("/usr/lib/postgresql/15/bin/pg_dump");
            candidates.Add("/usr/lib/postgresql/14/bin/pg_dump");
        }

        void AddFromBase(string? basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath) || !IO.Directory.Exists(basePath)) return;
            foreach (var dir in IO.Directory.GetDirectories(basePath, "*", IO.SearchOption.TopDirectoryOnly))
            {
                var path = IO.Path.Combine(dir, "bin", "pg_dump.exe");
                if (IO.File.Exists(path)) candidates.Add(path);
            }
        }

        AddFromBase(IO.Path.Combine(SystemAlias.Environment.GetFolderPath(SystemAlias.Environment.SpecialFolder.ProgramFiles), "PostgreSQL"));
        AddFromBase(IO.Path.Combine(SystemAlias.Environment.GetFolderPath(SystemAlias.Environment.SpecialFolder.ProgramFilesX86), "PostgreSQL"));
        AddFromBase(IO.Path.Combine("C:\\", "PostgreSQL"));
        candidates.Add(IO.Path.Combine(SystemAlias.Environment.GetFolderPath(SystemAlias.Environment.SpecialFolder.ProgramFiles), "pgAdmin 4", "runtime", "pg_dump.exe"));
        candidates.Add(IO.Path.Combine(SystemAlias.Environment.GetFolderPath(SystemAlias.Environment.SpecialFolder.ProgramFilesX86), "pgAdmin 4", "runtime", "pg_dump.exe"));

        return candidates.OrderByDescending(x => x).FirstOrDefault();
    }

    public async Task<IActionResult> OnPostRestoreAsync()
    {
        if (RestoreFile is null || RestoreFile.Length <= 0)
        {
            Error = "Selecciona un archivo .sql para restaurar.";
            return Page();
        }

        var ext = IO.Path.GetExtension(RestoreFile.FileName);
        if (!string.Equals(ext, ".sql", StringComparison.OrdinalIgnoreCase))
        {
            Error = "El archivo debe ser .sql";
            return Page();
        }

        var conn = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(conn))
        {
            Error = "No se encontró una cadena de conexión válida (DefaultConnection / Default / Postgres).";
            return Page();
        }

        var psql = await ResolvePsqlPathAsync();
        if (string.IsNullOrWhiteSpace(psql) || (!IsCommandName(psql) && !IO.File.Exists(psql)))
        {
            Error = "No se encontró psql en el servidor. Instala PostgreSQL client tools o configura Database:PsqlPath.";
            return Page();
        }

        var cs = new NpgsqlConnectionStringBuilder(conn);
        var host = string.IsNullOrWhiteSpace(cs.Host) ? "localhost" : cs.Host;
        var port = cs.Port <= 0 ? 5432 : cs.Port;
        var user = cs.Username ?? string.Empty;
        var db = cs.Database ?? string.Empty;
        var pass = cs.Password ?? string.Empty;
        var sslMode = cs.SslMode.ToString().ToLowerInvariant();

        var userEsc = Uri.EscapeDataString(user);
        var passEsc = Uri.EscapeDataString(pass);
        var hostEsc = Uri.EscapeDataString(host);
        var dbEsc = Uri.EscapeDataString(db);
        var dbUri = $"postgresql://{userEsc}:{passEsc}@{hostEsc}:{port}/{dbEsc}?sslmode={sslMode}";

        var uploads = IO.Path.Combine(_env.ContentRootPath, "App_Data", "restore");
        IO.Directory.CreateDirectory(uploads);
        var tmp = IO.Path.Combine(uploads, $"restore_{Guid.NewGuid():N}.sql");
        await using (var fs = new IO.FileStream(tmp, IO.FileMode.Create))
        {
            await RestoreFile.CopyToAsync(fs);
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = psql,
                Arguments = $"--dbname=\"{dbUri}\" -v ON_ERROR_STOP=1 -f \"{tmp}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Error = "Error al restaurar respaldo: " + (string.IsNullOrWhiteSpace(stderr) ? "psql devolvió código no exitoso." : stderr.Trim());
                return Page();
            }

            Info = "Restauración completada correctamente.";
            return RedirectToPage();
        }
        finally
        {
            try
            {
                if (IO.File.Exists(tmp)) IO.File.Delete(tmp);
            }
            catch { }
        }
    }

    private async Task<string?> ResolvePsqlPathAsync()
    {
        var configured = (_cfg["Database:PsqlPath"] ?? SystemAlias.Environment.GetEnvironmentVariable("PSQL_PATH") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(configured) && (IO.File.Exists(configured) || IsCommandName(configured)))
            return configured;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "where" : "which",
                Arguments = "psql",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = new Process { StartInfo = psi };
            p.Start();
            var output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            var first = output.Split(new[] { '\r', '\n' }, SystemAlias.StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(x => IO.File.Exists(x));
            if (!string.IsNullOrWhiteSpace(first)) return first;
        }
        catch { }

        var fromPath = FindInPath(OperatingSystem.IsWindows() ? "psql.exe" : "psql");
        if (!string.IsNullOrWhiteSpace(fromPath)) return fromPath;

        var candidates = new List<string>();
        if (!OperatingSystem.IsWindows())
        {
            candidates.Add("/usr/bin/psql");
            candidates.Add("/usr/local/bin/psql");
            candidates.Add("/usr/lib/postgresql/16/bin/psql");
            candidates.Add("/usr/lib/postgresql/15/bin/psql");
            candidates.Add("/usr/lib/postgresql/14/bin/psql");
        }

        void AddFromBase(string? basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath) || !IO.Directory.Exists(basePath)) return;
            foreach (var dir in IO.Directory.GetDirectories(basePath, "*", IO.SearchOption.TopDirectoryOnly))
            {
                var path = IO.Path.Combine(dir, "bin", "psql.exe");
                if (IO.File.Exists(path)) candidates.Add(path);
            }
        }

        AddFromBase(IO.Path.Combine(SystemAlias.Environment.GetFolderPath(SystemAlias.Environment.SpecialFolder.ProgramFiles), "PostgreSQL"));
        AddFromBase(IO.Path.Combine(SystemAlias.Environment.GetFolderPath(SystemAlias.Environment.SpecialFolder.ProgramFilesX86), "PostgreSQL"));
        AddFromBase(IO.Path.Combine("C:\\", "PostgreSQL"));
        candidates.Add(IO.Path.Combine(SystemAlias.Environment.GetFolderPath(SystemAlias.Environment.SpecialFolder.ProgramFiles), "pgAdmin 4", "runtime", "psql.exe"));
        candidates.Add(IO.Path.Combine(SystemAlias.Environment.GetFolderPath(SystemAlias.Environment.SpecialFolder.ProgramFilesX86), "pgAdmin 4", "runtime", "psql.exe"));

        return candidates.OrderByDescending(x => x).FirstOrDefault();
    }

    private static bool IsCommandName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return !value.Contains(IO.Path.DirectorySeparatorChar) && !value.Contains(IO.Path.AltDirectorySeparatorChar);
    }

    private static string? FindInPath(string fileName)
    {
        var path = SystemAlias.Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return null;
        foreach (var chunk in path.Split(IO.Path.PathSeparator, SystemAlias.StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = IO.Path.Combine(chunk.Trim(), fileName);
                if (IO.File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    private string? ResolveConnectionString()
    {
        var c1 = _cfg.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(c1)) return c1;

        var c2 = _cfg.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(c2)) return c2;

        var c3 = _cfg.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(c3)) return c3;

        var c4 = _cfg["ConnectionStrings:DefaultConnection"];
        if (!string.IsNullOrWhiteSpace(c4)) return c4;

        var c5 = _cfg["ConnectionStrings:Default"];
        if (!string.IsNullOrWhiteSpace(c5)) return c5;

        var c6 = _cfg["ConnectionStrings:Postgres"];
        if (!string.IsNullOrWhiteSpace(c6)) return c6;

        return null;
    }
}



