using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Pages.Admin.SystemPages;
using HNControl.Web.Pages.Public;
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
using DeliveryDetails = HNControl.Web.Pages.Projects.Delivery.DetailsModel;
using DeliveryEdit = HNControl.Web.Pages.Projects.Delivery.EditModel;
using static ClientCommunicationTests;

internal static class DeliveryCommunicationTests
{
    public static async Task RunAsync(ITemplateDocxService templates, Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var files = new MemoryFiles();
        var email = new RecordingEmail();
        var converter = new RecordingConverter();
        var recipients = new DocumentNotificationRecipients(db);
        var originalCopies = DocumentNotificationRecipients.Parse(SystemConfiguration.DefaultDocumentNotificationEmails);
        check((await recipients.GetAsync()).SequenceEqual(originalCopies), "Sin configuración se usan los dos correos iniciales");
        var configurationPage = new ConfigurationModel(db, files, null!, null!, null!);
        var context = new DefaultHttpContext();
        configurationPage.PageContext = new PageContext { HttpContext = context };
        configurationPage.TempData = new TempDataDictionary(context, new MemoryTempData());
        configurationPage.Input.DocumentNotificationEmails = "nuevo@example.test;otro@example.test\nNUEVO@example.test";
        await configurationPage.OnPostSaveDocumentNotificationsAsync();
        check((await recipients.GetAsync()).SequenceEqual(new[] { "nuevo@example.test", "otro@example.test" }), "Configuración permite agregar y deduplicar correos");
        configurationPage.Input.DocumentNotificationEmails = "correo inválido";
        await configurationPage.OnPostSaveDocumentNotificationsAsync();
        check(configurationPage.FlashType == "danger" && (await recipients.GetAsync()).Count == 2, "Correos inválidos no reemplazan configuración válida");
        configurationPage.Input.DocumentNotificationEmails = "otro@example.test";
        await configurationPage.OnPostSaveDocumentNotificationsAsync();
        check((await recipients.GetAsync()).SequenceEqual(new[] { "otro@example.test" }), "Configuración permite eliminar destinatarios");
        configurationPage.Input.DocumentNotificationEmails = "";
        await configurationPage.OnPostSaveDocumentNotificationsAsync();
        check((await recipients.GetAsync()).Count == 0, "Lista vacía desactiva copias sin restaurar correos eliminados");
        configurationPage.Input.DocumentNotificationEmails = "operaciones@example.test;finanzas@example.test";
        await configurationPage.OnPostSaveDocumentNotificationsAsync();

        var client = new Client { Name = "Empresa Prueba", ClientCode = "HN-0042", LegalRepresentative = "Representante & Legal", BillingEmail = "facturas@example.test", Email = "cliente@example.test" };
        var item = new ProjectDeliveryFormat
        {
            Client = client, Title = "Entrega de prueba", ReceiverName = "Receptor de prueba", ReceiverEmail = "receptor@example.test",
            PublicToken = Guid.NewGuid().ToString("N"), Status = ProjectDeliveryFormatStatus.SentForSignature,
            TokenExpiresAt = DateTime.UtcNow.AddDays(2), ServiceSummary = ProjectDeliveryPayload.Serialize(new DeliveryTemplateData { NOMBRETECNICO = "Técnico de prueba" })
        };
        db.Add(item);
        await db.SaveChangesAsync();
        var evidence = new EvidenceRenderer();
        var signing = new DeliveryDocumentSigningService(db, files, templates, converter, evidence, email, recipients);
        await signing.SavePdfAsync(item);
        var unsigned = WordText(converter.LastDocx!);
        check(unsigned.Contains("Representante & Legal") && unsigned.Contains("facturas@example.test")
            && !unsigned.Contains("RLCLIENTE") && !unsigned.Contains("CORREOFACTURA"), "Acta reemplaza representante legal y correo de facturación");
        check(unsigned.Contains("Técnico de prueba") && unsigned.Contains("Empresa Prueba"), "Acta mantiene datos de técnico y razón social");
        check(evidence.Calls == 1, "Generación conserva anexos de evidencia");

        var publicPage = new DeliveryDocumentModel(db, files, signing, NullLogger<DeliveryDocumentModel>.Instance);
        var png = new byte[256]; new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0);
        var signature = "data:image/png;base64," + Convert.ToBase64String(png);
        check(await publicPage.OnPostSignAsync(item.PublicToken, "Cliente Firmante", "firmante@example.test", signature, false) is PageResult
            && item.SignedAt == null, "Firma de acta exige aceptación explícita del cliente");
        converter.Fail = true;
        await publicPage.OnPostSignAsync(item.PublicToken, "Cliente Firmante", "firmante@example.test", signature, true);
        item = await db.ProjectDeliveryFormats.Include(x => x.Client).SingleAsync(x => x.Id == item.Id);
        check(item.SignedAt == null && email.Messages.Count == 0, "Fallo de PDF no registra firma de acta ni envía correo");
        converter.Fail = false;
        await publicPage.OnPostSignAsync(item.PublicToken, "Cliente Firmante", "firmante@example.test", signature, true);
        check(item.SignedAt.HasValue && !item.IsFullySigned && item.Status != ProjectDeliveryFormatStatus.Signed, "Firma cliente deja acta pendiente de HN");
        await signing.NotifyCompletedAsync(item.Id);
        check(email.Messages.Count == 0, "Acta sin ambas firmas no envía confirmación final");
        check(await publicPage.OnPostSignAsync(item.PublicToken, "Otro", "otro@example.test", signature, true) is RedirectToPageResult
            && item.SignedByName == "Cliente Firmante", "Firma del acta no puede sobrescribirse");
        var edit = new DeliveryEdit(db, files);
        check(await edit.OnPostRemoveEvidenceAsync(item.Id, "foto.jpg") is BadRequestObjectResult, "Acta firmada protege evidencias de eliminación");
        check(await edit.OnGetAsync(item.Id) is BadRequestObjectResult, "Acta con firma no puede editarse");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PublicLinks:BaseUrl"] = "https://example.test" }).Build();
        var details = new DeliveryDetails(db, files, email, config, signing, recipients, NullLogger<DeliveryDetails>.Instance);
        details.PageContext = new PageContext { HttpContext = context };
        details.TempData = new TempDataDictionary(context, new MemoryTempData());
        check(await details.OnPostSignProviderAsync(item.Id, true) is ForbidResult, "Acta exige administrador para la firma HN");
        var admin = new ApplicationUser { Id = "delivery-admin", UserName = "Administrador", Email = "admin@example.test" };
        db.Users.Add(admin);
        item.Client!.LegalRepresentative = "Representante cambiado";
        item.Client.BillingEmail = "otro-correo@example.test";
        await db.SaveChangesAsync();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, AppRoles.Admin), new Claim(ClaimTypes.NameIdentifier, admin.Id) }, "test"));
        check(await details.OnPostSignProviderAsync(item.Id, false) is BadRequestObjectResult, "Firma HN de acta exige aceptación explícita");
        email.Fail = true;
        await details.OnPostSignProviderAsync(item.Id, true);
        check(item.IsFullySigned && item.CompletionEmailSentAt == null && details.FlashType == "warning", "Fallo SMTP conserva ambas firmas de acta para reintento");
        var signed = WordText(converter.LastDocx!);
        check(signed.Contains("Cliente Firmante") && signed.Contains("Administrador") && !signed.Contains("PENDIENTE DE FIRMA"), "Acta final contiene ambas firmas en la plantilla Word");
        check(signed.Contains("Representante & Legal") && signed.Contains("facturas@example.test") && !signed.Contains("Representante cambiado"), "Firma HN conserva los datos originales del acta");
        Directory.CreateDirectory("artifacts/delivery-check");
        await File.WriteAllBytesAsync("artifacts/delivery-check/acta-firmada-prueba.docx", converter.LastDocx!);
        email.Fail = false;
        await details.OnPostSendConfirmationAsync(item.Id);
        check(email.Messages.Count == 1 && email.Messages[0].To == "cliente@example.test"
            && email.Messages[0].Cc.Contains("operaciones@example.test") && email.Messages[0].Cc.Contains("finanzas@example.test")
            && !email.Messages[0].Cc.Contains("jsantana@hubnet-solutions.net"), "Acta respeta destinatarios configurados y elimina copias antiguas");
        check(email.Messages[0].Attachment!.SequenceEqual(RecordingConverter.Pdf)
            && email.Messages[0].Cc.Contains("receptor@example.test") && email.Messages[0].Cc.Contains("firmante@example.test"), "Acta envía PDF final a cliente, receptor y firmante");
        await details.OnPostSignProviderAsync(item.Id, true);
        check(email.Messages.Count == 1, "Doble envío no duplica confirmación de acta");
        await details.OnPostGeneratePdfAsync(item.Id);
        check(details.FlashType == "danger" && item.IsFullySigned, "Acta final no puede regenerarse con datos distintos");

        var expired = new ProjectDeliveryFormat { ClientId = item.ClientId, PublicToken = Guid.NewGuid().ToString("N"), TokenExpiresAt = DateTime.UtcNow.AddDays(-1), Status = ProjectDeliveryFormatStatus.SentForSignature };
        db.Add(expired); await db.SaveChangesAsync();
        check(await publicPage.OnPostSignAsync(expired.PublicToken, "Nombre", "correo@example.test", signature, true) is NotFoundResult, "Liga de acta vencida no permite firmar");

        await using var postgres = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=localhost;Database=script_only;Username=test;Password=test").Options);
        var sql = postgres.GetService<IMigrator>().GenerateScript("20260508201704_AddSalesProspectNotes", "20261022000200_AddDeliverySignaturesAndNotificationRecipients");
        check(sql.Contains("ALTER TABLE \"ProjectDeliveryFormats\" ADD \"ProviderSignedAt\"") && sql.Contains("DocumentNotificationEmails") && !sql.Contains("DROP TABLE"), "Migraciones incorporan firmas y destinatarios sin borrar datos");
        await File.WriteAllTextAsync("artifacts/delivery-check/migrations.sql", sql);
    }

    private sealed class EvidenceRenderer : IProjectDeliveryPdfRenderer
    {
        public int Calls { get; private set; }
        public Task<byte[]> RenderAsync(ProjectDeliveryFormat delivery) => throw new InvalidOperationException("No se debe reemplazar la plantilla Word");
        public Task<byte[]> AppendEvidencePagesAsync(ProjectDeliveryFormat delivery, byte[] basePdf) { Calls++; return Task.FromResult(basePdf); }
    }
}
