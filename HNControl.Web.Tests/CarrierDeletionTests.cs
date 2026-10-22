using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CarrierPage = HNControl.Web.Pages.Admin.Carriers.IndexModel;
using ServicePage = HNControl.Web.Pages.Admin.Carriers.Services.IndexModel;

internal static class CarrierDeletionTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var client = new Client { Name = "Cliente de prueba" };
        var contract = new ClientServiceContract { Client = client, Label = "Conservar contrato" };
        var carrier = new InternetCarrier { Name = "Carrier de prueba" };
        var service = new ClientCarrierService { Client = client, Carrier = carrier, ClientServiceContract = contract, ServiceLabel = "Servicio a eliminar" };
        var note = new ClientCarrierNote { Service = service, Message = "Bitácora" };
        var monitor = new MonitorTarget { Client = client, ClientCarrierService = service, Name = "Conservar historial" };
        var history = new MonitorCheck { Target = monitor, Success = true };
        var other = new ClientCarrierService { Client = client, Carrier = carrier, ServiceLabel = "Conservar otro servicio" };
        db.AddRange(note, history, other);
        await db.SaveChangesAsync();
        T Page<T>(T page, string? role = AppRoles.Admin) where T : PageModel
        {
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(role == null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.Role, role) }, role == null ? null : "test")) };
            page.PageContext = new PageContext { HttpContext = context };
            page.TempData = new TempDataDictionary(context, new ClientCommunicationTests.MemoryTempData());
            return page;
        }
        foreach (var role in new string?[] { null, AppRoles.Employee })
            check(await Page(new CarrierPage(db), role).OnPostDeleteAsync(carrier.Id) is ForbidResult
                && await Page(new ServicePage(db), role).OnPostDeleteAsync(service.Id) is ForbidResult,
                "Eliminar carriers y servicios requiere administrador");
        var protectedCarrier = Page(new CarrierPage(db));
        check(await protectedCarrier.OnPostDeleteAsync(carrier.Id) is RedirectToPageResult && protectedCarrier.FlashError != null
            && await db.InternetCarriers.CountAsync() == 1, "No elimina carrier con servicios ligados");
        var deleteService = Page(new ServicePage(db) { ClientId = client.Id, Q = "prueba" });
        var result = await deleteService.OnPostDeleteAsync(service.Id) as RedirectToPageResult;
        check(result?.RouteValues?["ClientId"]?.Equals(client.Id) == true && result.RouteValues["Q"]?.Equals("prueba") == true,
            "Eliminar servicio conserva filtros al regresar al listado");
        db.ChangeTracker.Clear();
        check(!await db.ClientCarrierServices.AnyAsync(s => s.Id == service.Id) && !await db.ClientCarrierNotes.AnyAsync(n => n.ServiceId == service.Id),
            "Servicio eliminado con su bitácora según confirmación");
        check(await db.ClientServiceContracts.AnyAsync(c => c.Id == contract.Id) && await db.Clients.AnyAsync(c => c.Id == client.Id)
            && await db.ClientCarrierServices.AnyAsync(s => s.Id == other.Id), "Eliminación conserva cliente, contrato y servicios ajenos");
        var savedMonitor = await db.MonitorTargets.FindAsync(monitor.Id);
        check(savedMonitor?.IsActive == false && savedMonitor.ClientCarrierServiceId == null && await db.MonitorChecks.AnyAsync(h => h.Id == history.Id),
            "Eliminar servicio detiene monitores y conserva su historial");
        check(await Page(new ServicePage(db)).OnPostDeleteAsync(service.Id) is NotFoundResult, "Repetir eliminación no afecta otros servicios");
        await Page(new ServicePage(db)).OnPostDeleteAsync(other.Id);
        var finalCarrier = Page(new CarrierPage(db));
        check(await finalCarrier.OnPostDeleteAsync(carrier.Id) is RedirectToPageResult && finalCarrier.FlashOk != null
            && !await db.InternetCarriers.AnyAsync(c => c.Id == carrier.Id), "Permite eliminar carrier al quedar sin servicios");
        check(await Page(new CarrierPage(db)).OnPostDeleteAsync(carrier.Id) is NotFoundResult, "Carrier inexistente no produce eliminaciones adicionales");
    }
}
