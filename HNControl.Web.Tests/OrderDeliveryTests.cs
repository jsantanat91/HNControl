using System.Security.Claims;
using HNControl.Shared;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using HNControl.Web.Controllers.Mobile;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QuestPDF.Fluent;

internal static class OrderDeliveryTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var empty = OrderDeliveryData.Read("");
        check(empty.Kind == "service" && empty.Validate(true) == null, "Entrega: órdenes antiguas conservan servicio sin requerir hardware");
        var data = new OrderDeliveryData { Kind = "both", HasRouter = true, RouterBrand = "MikroTik", RouterModel = "hEX", RouterSerial = "HN-RTR-001" };
        check(data.Validate(true) == null && data.Equipment.Single().Serial == "HN-RTR-001", "Entrega: marca y serie del router integradas a equipos");
        data.RouterSerial = "";
        check(data.Validate() != null, "Entrega: exige serie cuando se registra router");
        data.RouterSerial = "HN-RTR-001";
        data.Items.Add(new() { Name = "Switch", Serial = "HN-RTR-001" });
        check(data.Validate() != null, "Entrega: detecta series repetidas incluyendo router");
        data.Items[0].Serial = "SW-01"; data.Items[0].Quantity = 2;
        check(data.Validate() != null, "Entrega: un equipo por serie");
        data.Items[0].Quantity = 1;
        check(OrderDeliveryData.Read(data.Serialize()).Items[0].Serial == "SW-01", "Entrega: conserva router y equipo en serialización");
        check(new OrderDeliveryData { Kind = "hardware" }.Validate() == null && new OrderDeliveryData { Kind = "hardware" }.Validate(true) != null,
            "Entrega: permite preparar borrador pero exige equipo al firmar hardware");

        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options); await db.Database.EnsureCreatedAsync();
        var user = new ApplicationUser { Id = "delivery-tech", UserName = "delivery-tech" };
        var order = new ServiceOrder { Client = new Client { Name = "Cliente QA" }, Title = "Entrega QA", ClaimedByUserId = user.Id, PdfStoragePath = "old.pdf", PdfGeneratedAt = DateTime.UtcNow };
        db.Users.Add(user); db.EmployeeProfiles.Add(new EmployeeProfile { UserId = user.Id, FullName = "Técnico QA" }); db.Add(order); await db.SaveChangesAsync();
        ClaimsPrincipal Actor(string role, string id) => new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, id) }, "test"));
        var tech = Actor(AppRoles.Employee, user.Id);
        check(await OrderDeliveryService.SaveAsync(db, order.Id, Actor(AppRoles.Employee, "other"), data, "") != null, "Entrega: otro técnico no puede editar");
        check(await OrderDeliveryService.SaveAsync(db, order.Id, Actor(AppRoles.Seller, user.Id), data, "") != null, "Entrega: roles no operativos no pueden editar");
        check(await OrderDeliveryService.SaveAsync(db, order.Id, tech, data, "") == null, "Entrega: técnico asignado guarda datos");
        db.ChangeTracker.Clear(); order = await db.ServiceOrders.SingleAsync();
        check(order.PdfStoragePath == null && order.PdfGeneratedAt == null && OrderDeliveryData.Read(order.DeliveryJson).RouterBrand == "MikroTik", "Entrega: invalida PDF antiguo y conserva marca capturada");
        var before = OrderDeliveryService.Version(order);
        check(await OrderDeliveryService.SaveAsync(db, order.Id, tech, data, "") != null, "Entrega: rechaza sobrescritura desde una versión antigua");
        data.RouterSerial = "HN-RTR-002";
        check(await OrderDeliveryService.SaveAsync(db, order.Id, tech, data, order.DeliveryJson) == null, "Entrega: permite actualización desde versión vigente");
        db.ChangeTracker.Clear(); order = await db.ServiceOrders.SingleAsync();
        check(OrderDeliveryService.Version(order) != before, "Entrega: cambio de serie cambia versión que se firma");
        db.ServiceOrderSignatures.Add(new() { OrderId = order.Id, Role = SignatureRole.Technician, SignedByName = "QA", StoragePath = "signature.png" }); await db.SaveChangesAsync();
        check(await OrderDeliveryService.SaveAsync(db, order.Id, Actor(AppRoles.Admin, "admin"), data, order.DeliveryJson) != null, "Entrega: incluso administrador debe respetar firmas existentes");
        db.ServiceOrderSignatures.RemoveRange(db.ServiceOrderSignatures); order.Status = ServiceOrderStatus.Finalized; await db.SaveChangesAsync();
        check(await OrderDeliveryService.SaveAsync(db, order.Id, tech, data, order.DeliveryJson) != null, "Entrega: bloqueo del servidor para orden finalizada");
        var api = new ServiceOrdersController(db, new LocalFileStorage(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:BasePath"] = Path.GetFullPath("artifacts/unified-delivery-signatures") }).Build()), null!);
        api.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Actor(AppRoles.Seller, "outsider") } };
        check(await api.Delivery(order.Id) is ForbidResult, "Entrega: API impide consulta de roles ajenos");
        api.HttpContext.User = tech;
        check(await api.Delivery(order.Id) is OkObjectResult, "Entrega: API permite consulta de entregas finalizadas");
        order.Status = ServiceOrderStatus.InProgress; order.CurrentArea = ServiceOrderWorkflowArea.CierreTecnico; await db.SaveChangesAsync();
        check(await api.Submit(order.Id, new ServiceOrdersController.SubmitOrderRequest { DeliveryVersion = "stale" }) is ConflictObjectResult,
            "Entrega: rechaza firma móvil de una versión de equipos diferente");
        var png = Document.Create(d => d.Page(p => { p.Size(250, 65); p.Content().Text("Firma autógrafa de prueba").FontSize(21).Italic(); })).GenerateImages().Single();
        var signature = "data:image/png;base64," + Convert.ToBase64String(png);
        var result = await api.Submit(order.Id, new ServiceOrdersController.SubmitOrderRequest
        { DeliveryVersion = OrderDeliveryService.Version(order), TechSigDataUrl = signature, ClientSigDataUrl = signature });
        check(result is OkObjectResult && await db.ServiceOrderSignatures.CountAsync(x => x.OrderId == order.Id) == 2
            && order.Status == ServiceOrderStatus.InReview, "Entrega: una sola captura de ambas firmas envía servicio y equipos a revisión");
        check(await OrderDeliveryService.SaveAsync(db, order.Id, tech, data, order.DeliveryJson) != null,
            "Entrega: datos quedan bloqueados después de firmar desde la APK");
    }
}
