using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using HNControl.Web.Controllers.Mobile;
using HNControl.Web.Pages.ServiceOrders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using AdminCreate = HNControl.Web.Pages.Admin.ServiceOrders.CreateModel;
using AdminEdit = HNControl.Web.Pages.Admin.ServiceOrders.EditModel;

internal static class EquipmentCollectionTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var files = new ClientCommunicationTests.MemoryFiles();
        var client = new Client { Name = "Cliente cerrado", IsActive = false, Email = "cliente@example.test" };
        var tech = new EmployeeProfile { UserId = "collector", FullName = "Técnico", IsActive = true, ServiceOrderCommissionEnabled = true, ServiceOrderCommissionAmount = 150 };
        db.AddRange(client, tech); await db.SaveChangesAsync();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, tech.UserId), new Claim(ClaimTypes.Role, AppRoles.Employee) }, "test"));
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMvcCore().AddDataAnnotations();
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { User = user, RequestServices = provider };
        T Page<T>(T page) where T : PageModel
        {
            page.PageContext = new PageContext { HttpContext = http };
            page.TempData = new TempDataDictionary(http, new ClientCommunicationTests.MemoryTempData());
            return page;
        }
        var config = new ConfigurationBuilder().Build();
        var notices = new ServiceOrderCreationNotifications(db, new NoMail(), config, NullLogger<ServiceOrderCreationNotifications>.Instance);
        var create = Page(new AdminCreate(db, notices));
        create.Input = new() { ClientId = client.Id, Type = ServiceOrderType.RecoleccionEquipos, Description = "Av. Central 123, Centro, CDMX", AssignedUserId = tech.UserId };
        check(await create.OnPostAsync() is RedirectToPageResult, "Recolección: crea para cliente inactivo con dirección y título automático");
        var order = await db.ServiceOrders.Include(x => x.Checklist).SingleAsync();
        check(order.Type == ServiceOrderType.RecoleccionEquipos && order.Title == "Recolección de equipos" && order.DeliveryJson == "" && order.Checklist.Count == 0,
            "Recolección: nuevo tipo dentro de órdenes, sin entrega ni checklist");
        create.Input.Type = ServiceOrderType.Preventivo;
        check(await create.OnPostAsync() is PageResult && await db.ServiceOrders.CountAsync() == 1, "Recolección: no habilita clientes inactivos en otros tipos");
        create.Input.Type = ServiceOrderType.RecoleccionEquipos; create.Input.Description = " ";
        check(await create.OnPostAsync() is PageResult && !create.ModelState.IsValid, "Recolección: exige dirección al crear");

        var work = Page(new WorkModel(db, files));
        check(await work.OnPostTakeAsync(order.Id) is RedirectToPageResult && order.ClaimedByUserId == tech.UserId, "Recolección: técnico toma la orden existente");
        work = Page(new WorkModel(db, files));
        check(await work.OnPostSignAndSubmitAsync(order.Id, null) is PageResult && work.Info!.Contains("foto"), "Recolección: no finaliza sin fotos");
        var pendingApi = new ServiceOrdersController(db, files, null!) { ControllerContext = new ControllerContext { HttpContext = http } };
        check(await pendingApi.Submit(order.Id, null) is ConflictObjectResult, "Recolección: API también exige fotos antes de cerrar");
        check(await pendingApi.UploadEvidence(order.Id, new ServiceOrdersController.UploadEvidenceRequest
        { EvidenceFile = new FormFile(new MemoryStream(new byte[] { 1 }), 0, 1, "EvidenceFile", "archivo.pdf") }) is BadRequestObjectResult,
            "Recolección: API rechaza adjuntar PDF en lugar de foto");
        var pdfEvidence = new ServiceOrderEvidence { Order = order, OriginalFileName = "archivo.pdf", SizeBytes = 10 };
        db.Add(pdfEvidence); await db.SaveChangesAsync();
        check(await EquipmentCollectionOrder.ValidateClosureAsync(db, order) != null, "Recolección: PDF no sustituye evidencia fotográfica");
        bool blocked = false;
        try { await ServiceOrderCommissionService.FinalizeAsync(db, order, DateTime.UtcNow); } catch (InvalidOperationException) { blocked = true; }
        check(blocked && order.Status != ServiceOrderStatus.Finalized, "Recolección: cierre central también valida fotos");
        db.Remove(pdfEvidence); await db.SaveChangesAsync();
        work = Page(new WorkModel(db, files));
        work.EvidenceFile = new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "EvidenceFile", "archivo.pdf");
        check(await work.OnPostUploadEvidenceAsync(order.Id) is PageResult && !await db.ServiceOrderEvidences.AnyAsync(), "Recolección: carga web rechaza PDF");
        var photo = new ServiceOrderEvidence { Order = order, OriginalFileName = "recoleccion.JPG", SizeBytes = 20, StoragePath = "foto.jpg" };
        db.Add(photo); await db.SaveChangesAsync();
        check(await EquipmentCollectionOrder.ValidateClosureAsync(db, order) == null, "Recolección: acepta foto vinculada a la orden");
        check(!OrderDeliveryService.CanEdit(order, user), "Recolección: no admite formulario de entrega de instalación");
        work = Page(new WorkModel(db, files));
        check(await work.OnPostSignAndSubmitAsync(order.Id, null) is RedirectToPageResult && order.Status == ServiceOrderStatus.Finalized && order.FinalizedAt.HasValue,
            "Recolección: finaliza directamente sin firmas ni etapas");
        check(!await db.EmployeeDeductions.AnyAsync() && !await db.ServiceOrderSignatures.AnyAsync(), "Recolección: no genera bono aunque el técnico tenga comisión activa");
        check(await db.ServiceOrderAuditLogs.AnyAsync(x => x.OrderId == order.Id && x.EventType == ServiceOrderAuditEvent.Completed), "Recolección: conserva auditoría del cierre");
        check(await Page(new WorkModel(db, files)).OnPostSignAndSubmitAsync(order.Id, null) is ForbidResult, "Recolección: evita doble cierre");
        var completed = Page(new WorkModel(db, files)); await completed.OnGetAsync(order.Id);
        check(completed.IsReadOnly, "Recolección: vista finalizada oculta carga y botón de cierre");
        var edit = Page(new AdminEdit(db)); await edit.OnGetAsync(order.Id);
        edit.Input!.Type = ServiceOrderType.NuevaInstalacion;
        check(await edit.OnPostAsync() is PageResult && edit.Error!.Contains("tipo"), "Recolección: no puede convertirse en instalación con bonificación");
        check(await edit.OnPostConvertToGlobalAsync(order.Id) is BadRequestObjectResult, "Recolección: no puede convertirse a global");
        edit.NewWorkItem = new() { Title = "Actividad", Type = ServiceOrderType.Preventivo };
        check(await edit.OnPostAddWorkItemAsync(order.Id) is BadRequestObjectResult, "Recolección: no acepta actividades adicionales");
        order.Status = ServiceOrderStatus.InProgress; await db.SaveChangesAsync();
        var api = new ServiceOrdersController(db, files, null!) { ControllerContext = new ControllerContext { HttpContext = http } };
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "other"), new Claim(ClaimTypes.Role, AppRoles.Employee) }, "test"));
        check(await api.Submit(order.Id, null) is ForbidResult, "Recolección: otro técnico no puede cerrar por API");
        http.User = user;
        check(await api.Submit(order.Id, null) is OkObjectResult && order.Status == ServiceOrderStatus.Finalized, "Recolección: API cierra sin enviar firmas");
        check(!await db.EmployeeDeductions.AnyAsync() && !client.IsActive, "Recolección: API sin comisión ni reactivación del cliente");
        check(System.Net.WebUtility.HtmlDecode(ServiceOrderNotificationTemplates.Client(order, new ServiceOrderScheduleSnapshot(null, null, false))).Contains("Dirección de recolección"), "Recolección: aviso al cliente incluye dirección");
        check(!ServiceOrderNotificationTemplates.Report(order).Contains("firmas registradas"), "Recolección: correo del PDF no anuncia firmas inexistentes");
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var png = await File.ReadAllBytesAsync("HNControl.Web.Tests/Fixtures/contract-signature.png");
        var stored = await files.SaveBytesAsync(png, "demo", "recoleccion.png", "image/png");
        photo.StoragePath = stored.storagePath; photo.ContentType = "image/png"; photo.OriginalFileName = "recoleccion.png";
        await db.SaveChangesAsync();
        var pdf = await new ServiceOrderPdfRenderer(config, db, files, new TestEnvironment(Path.GetFullPath("HNControl.Web"))).RenderAsync(order);
        Directory.CreateDirectory("artifacts/equipment-collection");
        await File.WriteAllBytesAsync("artifacts/equipment-collection/recoleccion-demo.pdf", pdf);
        using var document = PdfSharpCore.Pdf.IO.PdfReader.Open(new MemoryStream(pdf), PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.Import);
        check(document.PageCount >= 1, "Recolección: PDF con dirección y evidencia se genera sin firmas");
        var ordinary = new ServiceOrder { Client = client, Type = ServiceOrderType.NuevaInstalacion, ClaimedByUserId = tech.UserId, PublicToken = Guid.NewGuid().ToString("N") };
        db.Add(ordinary); await db.SaveChangesAsync();
        await ServiceOrderCommissionService.FinalizeAsync(db, ordinary, DateTime.UtcNow);
        check(await db.EmployeeDeductions.CountAsync() == 1 && (await db.EmployeeDeductions.SingleAsync()).SourceServiceOrderId == ordinary.Id, "Recolección: otras órdenes conservan su comisión");
    }
    private sealed class NoMail : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string htmlBody, byte[]? attachmentBytes = null, string? attachmentName = null, string? attachmentContentType = null, IReadOnlyCollection<string>? ccEmails = null) => Task.CompletedTask;
    }
}
