using System.Reflection;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Pages.Admin.SystemPages;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

internal static class SystemHealthTests
{
    public static async Task RunAsync(string webRoot, Action<bool, string> check)
    {
        var folder = Path.GetFullPath(Path.Combine("artifacts", "health-tests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(folder);
        var sample = Path.Combine(folder, "document.txt");
        await File.WriteAllTextAsync(sample, "Documento que la consulta debe conservar.");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Storage:BasePath"] = folder }).Build();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Clients.Add(new Client { Name = "Conservar cliente", Email = "cliente@example.test" });
        await db.SaveChangesAsync();
        var reader = new SystemHealthReader(db, configuration, new TestEnvironment(webRoot), NullLogger<SystemHealthReader>.Instance);
        var before = DateTimeOffset.UtcNow;
        var snapshot = await reader.ReadAsync();
        check(snapshot.CheckedAt >= before && snapshot.Checks.Single(c => c.Name == "Base de datos").Healthy == true,
            "Salud consulta conexión real y registra fecha de consulta");
        check(snapshot.ProcessMemoryBytes > 0 && snapshot.Uptime >= TimeSpan.Zero && snapshot.AvailableProcessors > 0,
            "Salud obtiene métricas reales del proceso");
        check(snapshot.Checks.Single(c => c.Name == "Archivos y documentos").Healthy == true
            && snapshot.Checks.Single(c => c.Name == "Plantillas de documentos").Healthy == true,
            "Salud confirma lectura y presencia de las tres plantillas");
        check(await db.Clients.CountAsync() == 1 && !db.ChangeTracker.HasChanges()
            && Directory.GetFiles(folder).Length == 1 && await File.ReadAllTextAsync(sample) == "Documento que la consulta debe conservar.",
            "Consulta de salud conserva registros y archivos sin escritura");

        var missingStorage = Path.Combine(folder, "no-existe");
        configuration["Storage:BasePath"] = missingStorage;
        var missingDb = Path.Combine(folder, "no-existe.sqlite");
        await using var unavailable = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={missingDb};Mode=ReadOnly").Options);
        var failure = await new SystemHealthReader(unavailable, configuration, new TestEnvironment(folder), NullLogger<SystemHealthReader>.Instance).ReadAsync();
        check(failure.Checks.Single(c => c.Name == "Base de datos").Healthy == false,
            "Base inaccesible se informa sin romper la consulta de salud");
        check(failure.Checks.Single(c => c.Name == "Archivos y documentos").Healthy == false
            && failure.Checks.Single(c => c.Name == "Plantillas de documentos").Healthy == false,
            "Archivos o plantillas ausentes se muestran como no disponibles");
        check(!File.Exists(missingDb) && !Directory.Exists(missingStorage),
            "Diagnóstico fallido no crea base de datos ni carpetas");

        HealthModel Page(string? role)
        {
            var claims = role is null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.Role, role) };
            return new HealthModel(reader) { PageContext = new PageContext { HttpContext = new DefaultHttpContext
                { User = new ClaimsPrincipal(new ClaimsIdentity(claims, role is null ? null : "test")) } } };
        }
        check(await Page(null).OnGetAsync() is ForbidResult && await Page(AppRoles.Employee).OnGetAsync() is ForbidResult,
            "Salud rechaza usuarios anónimos y empleados");
        check(await Page(AppRoles.Admin).OnGetAsync() is PageResult && await Page(AppRoles.SuperAdmin).OnGetAsync() is PageResult,
            "Salud permite consulta a administradores y superadministradores");
        var cache = typeof(HealthModel).GetCustomAttribute<ResponseCacheAttribute>();
        check(cache?.NoStore == true && cache.Location == ResponseCacheLocation.None,
            "Diagnóstico administrativo no se almacena en cachés compartidas");
        var assemblyVersion = typeof(ApplicationRelease).Assembly.GetName().Version;
        check(assemblyVersion?.ToString(3) == ApplicationRelease.Version && ApplicationRelease.History[0].Version == ApplicationRelease.Version,
            "Versión instalada, compilación y release notes coinciden en 2.5.1");
    }
}
