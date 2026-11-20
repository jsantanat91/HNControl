using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Pages.Clients;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using static ClientCommunicationTests;

internal static class ClientContactRoutingTests
{
    public static async Task RunAsync(ITemplateDocxService templates, Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var client = new Client { Name = "EMPRESA DEMO", ClientCode = "HN-TEST", Email = "general@example.test", BillingEmail = "billing@example.test", IsActive = true };
        var other = new Client { Name = "OTRO CLIENTE" };
        var signer = new ClientContact { Client = client, Name = "Ana Firmante", Email = "ana@example.test", IsPrimary = true };
        var finance = new ClientContact { Client = client, Name = "Finanzas", Email = "finance@example.test" };
        var duplicate = new ClientContact { Client = client, Name = "Finanzas duplicada", Email = "FINANCE@example.test" };
        var noEmail = new ClientContact { Client = client, Name = "Sin correo" };
        var foreign = new ClientContact { Client = other, Name = "Otro firmante", Email = "otro@example.test" };
        db.AddRange(client, other, signer, finance, duplicate, noEmail, foreign,
            new SystemConfiguration { DocumentNotificationEmails = "admin@example.test;finance@example.test;ANA@example.test",
                EngineeringNotificationEmails = "ingenieria@example.test;finance@example.test;ANA@example.test",
                CompanyLegalName = "PRESTADOR DEMO", CompanyRfc = "DEM010101AA1", CompanyFiscalAddress = "Domicilio demo", CompanyFiscalZipCode = "01000" });
        await db.SaveChangesAsync();
        ClientContactSelection Choose(Guid? main, params Guid[] copies) => new() { Configured = true, PrimaryContactId = main, CopyContactIds = copies.ToList() };
        var selected = Choose(signer.Id, signer.Id, finance.Id, duplicate.Id, finance.Id);
        var saved = await ClientContactRouting.CaptureAsync(db, client.Id, selected);
        var route = ClientContactRouting.Resolve(saved, client);
        check(route.Primary.Email == "ana@example.test" && route.Primary.Name == signer.Name && route.Copies.Count == 1,
            "Contactos: principal y CC propios, sin repetir principal ni correos duplicados");
        check(ClientContactRouting.Copies(route, new[] { "ADMIN@example.test", "finance@example.test", "ANA@example.test", "admin@example.test" })
            .SequenceEqual(new[] { "finance@example.test", "ADMIN@example.test" }), "Contactos: combina CC de cliente e internas sin duplicar To ni direcciones");
        foreach (var invalid in new[] { Choose(foreign.Id), Choose(signer.Id, foreign.Id), Choose(Guid.NewGuid()), Choose(noEmail.Id), Choose(signer.Id, noEmail.Id) })
        {
            try { await ClientContactRouting.CaptureAsync(db, client.Id, invalid); check(false, "Selección inválida debe fallar"); }
            catch (ArgumentException) { check(true, "Contactos: rechaza otro cliente, eliminados y correo vacío"); }
        }
        check(ClientContactRouting.Resolve(null, client).Primary.Email == "general@example.test",
            "Contactos: documentos históricos conservan el destinatario de ficha");
        try { ClientContactRouting.Resolve(saved, other); check(false, "Snapshot de otro cliente debe fallar"); }
        catch (InvalidOperationException) { check(true, "Contactos: un snapshot no puede reutilizarse para otro cliente"); }

        var service = new ClientServiceContract { Client = client, Label = "Servicio DEMO", ServiceType = ClientServiceType.Otro,
            NotificationContactsJson = saved, MonthlyAmount = 1000, ContractNumber = "HN-TEST-01" };
        db.Add(service); await db.SaveChangesAsync();
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PublicLinks:BaseUrl"] = "https://example.test" }).Build();
        var mail = new RecordingEmail(); var files = new MemoryFiles(); var converter = new RecordingConverter();
        var recipients = new DocumentNotificationRecipients(db);
        var signing = new ClientDocumentSigningService(db, files, templates, converter, mail, recipients);
        var services = new ServiceCollection().AddLogging();
        services.AddMvc();
        using var provider = services.BuildServiceProvider();
        T Context<T>(T page) where T : PageModel
        {
            var http = new DefaultHttpContext { RequestServices = provider, User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.Role, AppRoles.Admin), new Claim(ClaimTypes.NameIdentifier, "admin-demo") }, "test")) };
            page.PageContext = new PageContext { HttpContext = http };
            page.TempData = new TempDataDictionary(http, new MemoryTempData());
            return page;
        }
        DocumentsModel Documents() => Context(new DocumentsModel(db, null!, files, mail, cfg, templates, converter, signing,
            new NoActions(), NullLogger<DocumentsModel>.Instance, recipients));
        foreach (var kind in new[] { ClientLegalDocumentType.Contract, ClientLegalDocumentType.NDA })
        {
            var page = Documents();
            await page.OnPostGenerateAsync(client.Id, kind, service.Id);
            var doc = await db.ClientLegalDocuments.SingleAsync(d => d.ClientServiceContractId == service.Id && d.DocumentType == kind);
            check(page.FlashType == "success" && doc.NotificationContactsJson == saved,
                $"Contactos: {kind} hereda principal y copias desde el servicio contratado");
        }
        var contract = await db.ClientLegalDocuments.SingleAsync(d => d.DocumentType == ClientLegalDocumentType.Contract);
        var nda = await db.ClientLegalDocuments.SingleAsync(d => d.DocumentType == ClientLegalDocumentType.NDA);
        var send = Documents();
        await send.OnPostSendForSignatureAsync(client.Id, contract.Id, null);
        check(mail.Messages[^1].To == "ana@example.test" && mail.Messages[^1].Cc.SequenceEqual(new[] { "finance@example.test", "admin@example.test" }),
            "Contactos: invitación de contrato envía al principal con CC del cliente e internas");
        check(mail.Bodies[^1].Contains("Ana Firmante"), "Contactos: invitación se dirige al nombre del firmante designado");
        var welcome = Context(new WelcomeModel(db, new NoActions(), mail, cfg, NullLogger<WelcomeModel>.Instance, recipients) { ContractId = service.Id });
        await welcome.OnPostAsync(client.Id);
        check(mail.Messages[^1].To == "ana@example.test" && welcome.InternalCopies.SequenceEqual(new[] { "finance@example.test", "admin@example.test" }),
            "Contactos: bienvenida usa los destinatarios del documento elegido");
        var publicPage = Context(new HNControl.Web.Pages.Public.ClientDocumentModel(db, files, signing,
            NullLogger<HNControl.Web.Pages.Public.ClientDocumentModel>.Instance));
        await publicPage.OnPostSignAsync(contract.PublicToken, "Otro", "otro@example.test", "", true);
        check(contract.SignedAt == null && publicPage.Error!.Contains("contacto principal"),
            "Contactos: el formulario de firma rechaza cambiar la identidad designada");

        var oldToken = contract.PublicToken;
        var change = Documents(); change.Contacts = Choose(finance.Id, signer.Id);
        await change.OnPostSaveContactsAsync(client.Id, contract.Id);
        check(contract.PublicToken != oldToken && contract.Status == ClientLegalDocumentStatus.Draft && contract.TokenExpiresAt == null && contract.SentAt == null,
            "Contactos: cambiar destinatarios revoca el enlace enviado y requiere nueva invitación");
        var before = contract.NotificationContactsJson;
        var invalidPage = Documents(); invalidPage.Contacts = Choose(foreign.Id);
        await invalidPage.OnPostSaveContactsAsync(client.Id, contract.Id);
        check(contract.NotificationContactsJson == before && invalidPage.FlashType == "warning", "Contactos: edición inválida no modifica destinatarios");
        var malformed = Documents(); malformed.ModelState.AddModelError("Contacts.PrimaryContactId", "Invalid GUID");
        check(await malformed.OnPostSaveContactsAsync(client.Id, contract.Id) is BadRequestObjectResult, "Contactos: binding inválido no cambia el principal a la ficha");

        var draft = new ClientLegalDocument { Client = client, Title = "Borrador compartido", PublicToken = "enlace-bienvenida",
            NotificationContactsJson = saved, PdfStoragePath = contract.PdfStoragePath };
        db.Add(draft); await db.SaveChangesAsync();
        var draftEdit = Documents(); draftEdit.Contacts = Choose(finance.Id);
        await draftEdit.OnPostSaveContactsAsync(client.Id, draft.Id);
        check(draft.PublicToken != "enlace-bienvenida" && draft.SentAt == null,
            "Contactos: revoca también enlaces compartidos por bienvenida, aunque no tengan SentAt");
        draft.ProviderSignedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        var resend = Documents(); await resend.OnPostSendForSignatureAsync(client.Id, draft.Id, null);
        check(mail.Messages[^1].To == "finance@example.test" && resend.FlashType == "success",
            "Contactos: permite invitar al cliente si HN firmó primero sin modificar contactos");

        signer.Email = "direccion-nueva@example.test"; client.Email = "otro-general@example.test";
        await db.SaveChangesAsync();
        var unchanged = await ClientContactRouting.CaptureAsync(db, client.Id, ClientContactRouting.Selection(saved), saved);
        check(unchanged == saved && ClientContactRouting.Read(unchanged)!.Primary.Email == "ana@example.test",
            "Contactos: editar la agenda no redirige silenciosamente documentos guardados");
        nda.SignedAt = DateTime.UtcNow; nda.SignedByName = "Ana Firmante"; nda.SignedByEmail = "ana@example.test";
        await signing.SaveSignedPdfAsync(nda); await signing.NotifyCompletedAsync(nda.Id);
        check(mail.Messages[^1].To == "ana@example.test" && mail.Messages[^1].Cc.SequenceEqual(new[] { "finance@example.test", "admin@example.test" })
            && mail.Messages[^1].Attachment != null, "Contactos: confirmación final adjunta PDF y conserva principal/CC originales");
        var count = mail.Messages.Count;
        var locked = Documents(); locked.Contacts = Choose(finance.Id);
        await locked.OnPostSaveContactsAsync(client.Id, nda.Id);
        check(nda.NotificationContactsJson == saved && locked.FlashType == "warning", "Contactos: documentos firmados no cambian contactos");
        await signing.NotifyCompletedAsync(nda.Id);
        check(mail.Messages.Count == count, "Contactos: reintento de confirmación final no duplica correo");

        var notices = new ServiceOrderCreationNotifications(db, mail, cfg, NullLogger<ServiceOrderCreationNotifications>.Instance);
        var order = new ServiceOrder { Client = client, ClientServiceContractId = service.Id, Title = "Orden demo", PdfStoragePath = "initial.pdf" };
        db.Add(order); notices.QueueCreated(order); await db.SaveChangesAsync();
        check(order.NotificationContactsJson == saved, "Contactos: órdenes de otros flujos heredan contactos mediante la cola de creación");
        await notices.NotifyCreatedAsync(order.Id);
        check(mail.Messages[^1].To == "ana@example.test" && mail.Messages[^1].Cc.SequenceEqual(new[] { "finance@example.test" }),
            "Contactos: aviso al cliente de nueva orden respeta principal y CC; Ingeniería recibe su aviso separado");
        var report = Context(new HNControl.Web.Pages.Admin.ServiceOrders.DetailsModel(db, new Pdf(), files, mail, cfg, notices));
        await report.OnPostSendEmailAsync(order.Id);
        check(mail.Messages[^1].To == "ana@example.test" && mail.Messages[^1].Cc.SequenceEqual(new[] { "finance@example.test", "ingenieria@example.test" })
            && mail.Messages[^1].Attachment != null, "Contactos: PDF de orden usa principal, CC cliente e Ingeniería sin duplicados");
        var create = Context(new HNControl.Web.Pages.Admin.ServiceOrders.CreateModel(db, notices));
        create.Input = new() { ClientId = client.Id, Title = "Orden con selección propia", Contacts = Choose(finance.Id),
            StartDate = DateTime.Today, ExpectedEndDate = DateTime.Today, DeliveryKind = "service" };
        await create.OnPostAsync();
        var newOrder = await db.ServiceOrders.SingleAsync(o => o.Title == create.Input.Title);
        check(ClientContactRouting.Read(newOrder.NotificationContactsJson)!.Primary.Email == "finance@example.test",
            "Contactos: alta real de orden guarda selección propia antes del aviso");
        db.ServiceOrderSignatures.Add(new() { OrderId = newOrder.Id, Role = SignatureRole.Client, SignedByName = "Finanzas", StoragePath = "firma.png" });
        await db.SaveChangesAsync();
        var edit = Context(new HNControl.Web.Pages.Admin.ServiceOrders.EditModel(db));
        await edit.OnGetAsync(newOrder.Id); edit.Input!.Contacts = Choose(signer.Id);
        await edit.OnPostAsync();
        check(edit.Error!.Contains("firmas") && ClientContactRouting.Read(newOrder.NotificationContactsJson)!.Primary.Email == "finance@example.test",
            "Contactos: orden con firmas bloquea cambios de destinatarios");
        var finalize = Context(new HNControl.Web.Pages.Admin.ServiceOrders.EditModel(db));
        await finalize.OnGetAsync(newOrder.Id);
        // The delivery-free fixture permits finalizing without additional delivery requirements.
        newOrder.DeliveryJson = "";
        await db.SaveChangesAsync();
        finalize.Input!.Status = ServiceOrderStatus.Finalized;
        await finalize.OnPostAsync();
        check(finalize.Error == null && newOrder.Status == ServiceOrderStatus.Finalized
            && ClientContactRouting.Read(newOrder.NotificationContactsJson)!.Primary.Email == "finance@example.test",
            "Contactos: finalizar una orden firmada conserva contactos y reutiliza la transacción sin anidarla");
        await using var pg = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=localhost;Database=script;Username=test;Password=test").Options);
        var sql = pg.GetService<IMigrator>().GenerateScript("20261109232629_AddServiceOrderCommissions", "20261120000100_AddDocumentContacts");
        check(sql.Contains("ALTER TABLE \"ServiceOrders\" ADD \"NotificationContactsJson\"")
            && sql.Contains("ALTER TABLE \"ClientLegalDocuments\" ADD \"NotificationContactsJson\"")
            && sql.Contains("ALTER TABLE \"ClientServiceContracts\" ADD \"NotificationContactsJson\"") && !sql.Contains("DROP TABLE"),
            "Contactos: migración PostgreSQL agrega tres columnas opcionales sin borrar históricos");
    }
    private sealed class NoActions : IActionAccessService
    {
        public Task<bool> HasActionAsync(ClaimsPrincipal user, string actionKey) => Task.FromResult(false);
        public Task<HashSet<string>> GetAllowedActionsAsync(ClaimsPrincipal user) => Task.FromResult(new HashSet<string>());
    }
    private sealed class Pdf : IServiceOrderPdfRenderer
    {
        public Task<byte[]> RenderAsync(ServiceOrder order) => Task.FromResult("%PDF-test"u8.ToArray());
    }
}
