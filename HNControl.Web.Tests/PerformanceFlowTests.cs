using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Pages.Admin.Performance;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

internal static class PerformanceFlowTests
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.AddRange(new EmployeeProfile { UserId="a", FullName="Ana", SalaryBase=20000 },new EmployeeProfile { UserId="b", FullName="Bruno", SalaryBase=12000 }); await db.SaveChangesAsync();
        RateModel Page() => new(db) { PageContext = new PageContext { HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider(), User = new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"admin")},"test")) } } };
        var page=Page(); await page.OnGetAsync("a","2026-11",1);
        check(page.EmployeeName=="Ana" && page.SalaryBase==20000 && !page.HasReview,"KPI: carga identidad, sueldo y estado del empleado");
        page.Input.PersonalPerformance=page.Input.Teamwork=page.Input.ParticipationInTeam=page.Input.PunctualityAttendance=page.Input.ProjectExecution=page.Input.OrderCleanliness=page.Input.TechnicalSkills=5;
        page.Input.Notes=null; await page.OnPostAsync();
        check(page.Info!=null && await db.PerformanceReviews.CountAsync()==1 && (await db.PerformanceReviews.AsNoTracking().SingleAsync()).VariablePercent==1,"KPI: siete cincos guardan 100% sin notas obligatorias");
        page=Page(); await page.OnGetAsync("a","2026-11",1);
        check(page.HasReview && page.Input.TechnicalSkills==5,"KPI: recarga la evaluación existente");
        page.Input.PersonalPerformance=page.Input.Teamwork=page.Input.ParticipationInTeam=page.Input.PunctualityAttendance=page.Input.ProjectExecution=page.Input.OrderCleanliness=page.Input.TechnicalSkills=1;await page.OnPostAsync();
        check(await db.PerformanceReviews.CountAsync()==1 && (await db.PerformanceReviews.AsNoTracking().SingleAsync()).VariablePercent==.2m,"KPI: actualizar no duplica y conserva la regla de 20% para siete unos");
        page=Page();await page.OnGetAsync("b","2026-11",1);check(!page.HasReview && page.Input.PersonalPerformance==3,"KPI: cambiar empleado no arrastra las notas del anterior");
        page=Page();await page.OnGetAsync("a","2026-11",2);check(!page.HasReview && page.Input.PersonalPerformance==3,"KPI: nueva quincena carga sus valores propios");
        page=Page();await page.OnGetAsync("a","2020-02",2);check(page.PeriodEnd.Day==29 && page.MonthItems.Any(x=>x.Value=="2020-02"),"KPI: mes histórico y febrero bisiesto conservados en el selector");
        page=Page();await page.OnGetAsync("a","2026-99",1);check(page.Error!=null,"KPI: fecha inválida de consulta no provoca error del servidor");
        page=Page();page.Input.EmployeeId="missing";page.Input.Ym="2026-11";check(await page.OnPostAsync() is NotFoundResult,"KPI: rechaza empleados inexistentes");
        page=Page();page.Input.EmployeeId="a";page.Input.Ym="2026-99";await page.OnPostAsync();check(page.Error!=null && await db.PerformanceReviews.CountAsync()==1,"KPI: periodo inválido no guarda datos");
        var dashboard=new DashboardModel(db);await dashboard.OnGetAsync(2026,11,1);
        check(dashboard.EmployeesRated==1 && dashboard.Rows.Single(x=>x.UserId=="a").TotalPay==8400 && dashboard.Rows.Single(x=>x.UserId=="b").TotalPay==4800,"KPI: estimación conserva 80% fijo y 20% proporcional, pendiente sin variable");
        await dashboard.OnGetAsync(0,99,7);check(dashboard.Month is >=1 and <=12 && dashboard.Year>=1900,"KPI: filtros fuera de rango se normalizan");
    }
}
