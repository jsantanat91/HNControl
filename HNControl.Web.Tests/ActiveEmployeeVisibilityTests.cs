using ClosedXML.Excel;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kpi = HNControl.Web.Pages.Admin.Performance;

internal static class ActiveEmployeeVisibilityTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AggregateContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(14);
        var active = new EmployeeProfile { UserId = "active", FullName = "Persona activa", SalaryBase = 20000, IsActive = true };
        var inactive = new EmployeeProfile { UserId = "inactive", FullName = "Persona de baja", SalaryBase = 90000, IsActive = false };
        db.AddRange(active, inactive);
        db.AddRange(new ApplicationUser { Id = "inactive", UserName = "baja", Email = "baja@example.test" },
            new IdentityRole { Id = "employee-role", Name = AppRoles.Employee });
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = "inactive", RoleId = "employee-role" });
        await db.SaveChangesAsync();
        using var services = new ServiceCollection().BuildServiceProvider();
        T Page<T>(T page) where T : PageModel
        {
            page.PageContext = new PageContext { HttpContext = new DefaultHttpContext { RequestServices = services } };
            return page;
        }
        var rate = Page(new Kpi.RateModel(db));
        await rate.OnGetAsync(active.UserId, "2026-11", 1);
        rate.Input.PersonalPerformance = rate.Input.Teamwork = rate.Input.ParticipationInTeam = rate.Input.PunctualityAttendance =
            rate.Input.ProjectExecution = rate.Input.OrderCleanliness = rate.Input.TechnicalSkills = 5;
        await rate.OnPostAsync();
        // A real historical review stays stored after an employee is terminated.
        active.IsActive = false;
        await db.SaveChangesAsync();
        var kpi = new Kpi.DashboardModel(db);
        await kpi.OnGetAsync(2026, 11, 1);
        check(kpi.Rows.Count == 0 && kpi.EmployeesTotal == 0 && kpi.TotalPayrollPay == 0 && kpi.LabelsJson == "[]",
            "Bajas: KPI excluye empleados, importes y gráficas incluso con evaluaciones históricas");
        rate = Page(new Kpi.RateModel(db));
        check(await rate.OnGetAsync(active.UserId, "2026-11", 1) is NotFoundResult && !rate.EmployeeItems.Any(),
            "Bajas: no se ofrecen empleados inactivos para calificar");
        rate.Input.EmployeeId = active.UserId; rate.Input.Ym = "2026-11"; rate.Input.Half = 1;
        check(await rate.OnPostAsync() is NotFoundResult, "Bajas: POST directo no permite nuevas evaluaciones");
        var legacy = Page(new Kpi.EditModel(db));
        check(await legacy.OnGetAsync(active.UserId, start) is NotFoundResult, "Bajas: editor anterior tampoco permite calificarlas");
        legacy.Input.UserId = active.UserId; legacy.Input.PeriodStart = start; legacy.Input.PeriodEnd = end;
        check(await legacy.OnPostAsync() is NotFoundResult, "Bajas: POST del editor anterior bloqueado");

        async Task<string[]> ExportedEmployees()
        {
            var file = (FileContentResult)await new HNControl.Web.Pages.Performance.ExportPayrollModel(db).OnGetAsync(start, end);
            using var workbook = new XLWorkbook(new MemoryStream(file.FileContents));
            return workbook.Worksheet(1).RowsUsed().Skip(1).Select(r => r.Cell(1).GetString()).ToArray();
        }
        check((await ExportedEmployees()).Length == 0, "Bajas: Excel vacío no reincorpora usuarios por el fallback de roles");
        active.IsActive = true;
        await db.SaveChangesAsync();
        check((await ExportedEmployees()).SequenceEqual(new[] { active.FullName }), "Bajas: Excel conserva solo la nómina de activos");
        await kpi.OnGetAsync(2026, 11, 1);
        check(kpi.Rows.Count == 1 && kpi.EmployeesTotal == 1 && kpi.TotalPayrollPay == 10000,
            "Bajas: reactivar recupera la evaluación conservada y el cálculo original");

        var panel = Page(new HNControl.Web.Pages.Admin.DashboardModel(db, null!, null!, null!, null!, null!,
            new ModuleAvailability(new ConfigurationBuilder().Build()))) ;
        panel.PayrollYear = 2026; panel.PayrollMonth = 11; panel.PayrollHalf = 1;
        await panel.OnGetAsync();
        check(panel.Kpi.Employees == 1 && panel.PayrollRows.Count == 1 && panel.PayrollRows[0].UserId == active.UserId,
            "Panel: conteo y resumen de nómina muestran únicamente empleados activos");
        active.IsActive = false;
        await db.SaveChangesAsync();
        await panel.OnGetAsync();
        check(panel.Top.Count == 0 && panel.TopVarLabelsJson == "[]" && panel.PayrollRows.Count == 0 && panel.Kpi.Employees == 0,
            "Panel: una baja desaparece de tarjetas y gráfica aun teniendo evaluación");
        await panel.OnPostMarkPaidAsync(active.UserId, 2026, 11, 1);
        check(panel.FlashError != null && !await db.PayrollReceiptDispatches.AnyAsync(),
            "Panel: POST directo de pago para una baja no crea recibo ni intenta notificar");
        check(await db.PerformanceReviews.CountAsync() == 1 && await db.EmployeeProfiles.CountAsync() == 2,
            "Bajas: se conserva el historial sin eliminar perfiles ni evaluaciones");
    }

    // SQLite cannot aggregate decimals natively; production uses PostgreSQL numeric.
    private sealed class AggregateContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            foreach (var entity in builder.Model.GetEntityTypes())
                foreach (var property in entity.GetProperties())
                    if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                        property.SetValueConverter(new ValueConverter<decimal, double>(v => (double)v, v => (decimal)v));
        }
    }
}
