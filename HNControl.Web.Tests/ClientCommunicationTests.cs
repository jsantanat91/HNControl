using System.IO.Compression;
using System.Security.Claims;
using System.Xml.Linq;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Pages.Clients;
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

internal static class ClientCommunicationTests
{
    public static async Task RunAsync(ITemplateDocxService templates, Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var client = new Client { Name = "Empresa & Asociados <Prueba>", ClientCode = "HN-0022", Email = "cliente@example.test", BillingEmail = "facturas@example.test" };
        var contract = new ClientServiceContract { Client = client, ServiceType = ClientServiceType.Internet, Label = "Internet Dedicado", Notes = "[META] Plazo=24\n[META] InternetCapacidad=10" };
        var doc = new ClientLegalDocument { Client = client, ClientServiceContract = contract, Title = "Contrato de Internet", PublicToken = Guid.NewGuid().ToString("N"), TokenExpiresAt = DateTime.UtcNow.AddDays(10), Status = ClientLegalDocumentStatus.SentForSignature };
        db.Add(doc);
        await db.SaveChangesAsync();
        var email = new RecordingEmail();
        var files = new MemoryFiles();
        var converter = new RecordingConverter();
        var signing = new ClientDocumentSigningService(db, files, templates, converter, email, new DocumentNotificationRecipients(db));
        var body = ClientCommunicationTemplates.Welcome(client, new[] { (doc, "https://example.test/Public/ClientDocument/abc?x=1&y=2") });
        check(body.Contains("HN-0022") && body.Contains("24 meses") && body.Contains("Empresa &amp; Asociados &lt;Prueba&gt;"), "Bienvenida usa datos reales y codifica HTML");
        check(body.Contains("https://wa.me/525526014566") && body.Contains("soporte@hubnet-solutions.com.mx"), "Soporte y WhatsApp correctos");
        check(body.Contains("012 180 00123904423 6") && body.Contains("0123904423"), "Datos bancarios conservados");
        Directory.CreateDirectory("artifacts/welcome-check");
        await File.WriteAllTextAsync("artifacts/welcome-check/bienvenida.html", body);

        doc.SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract);
        doc.SignedAt = DateTime.UtcNow;
        doc.SignedByName = "Representante del cliente";
        doc.SignedByEmail = "firmante@example.test";
        await signing.SaveSignedPdfAsync(doc);
        await signing.NotifyCompletedAsync(doc.Id);
        check(email.Messages.Count == 0 && doc.Status == ClientLegalDocumentStatus.SentForSignature, "Firma cliente no envía confirmación bilateral prematura");
        check(WordText(converter.LastDocx!).Contains("PENDIENTE DE FIRMA HN SOLUTIONS"), "No se inventa firma HN a partir de la firma cliente");

        contract.Label = "Servicio cambiado después";
        contract.Notes = "[META] Plazo=48\n[META] InternetCapacidad=999";
        client.Name = "Razón social modificada después";
        await db.SaveChangesAsync();
        doc.ProviderSignedAt = DateTime.UtcNow;
        doc.ProviderSignedByName = "Administrador de prueba";
        doc.ProviderSignedByEmail = "admin@example.test";
        doc.ProviderSignedByUserId = "test-admin";
        converter.Fail = true;
        try { await signing.SaveSignedPdfAsync(doc); check(false, "Conversión debe fallar"); }
        catch (InvalidOperationException) { }
        await db.Entry(doc).ReloadAsync();
        check(doc.ProviderSignedAt == null && doc.Status != ClientLegalDocumentStatus.Signed, "Fallo PDF no guarda firma HN ni estado final");
        doc.ProviderSignedAt = DateTime.UtcNow;
        doc.ProviderSignedByName = "Administrador de prueba";
        doc.ProviderSignedByEmail = "admin@example.test";
        doc.ProviderSignedByUserId = "test-admin";
        converter.Fail = false;
        await signing.SaveSignedPdfAsync(doc);
        var signedText = WordText(converter.LastDocx!);
        check(signedText.Contains("Internet Dedicado 10 MB") && !signedText.Contains("999 MB") && signedText.Contains("24 meses"), "Segunda firma conserva las condiciones originales");
        check(signedText.Contains("Representante del cliente") && signedText.Contains("Administrador de prueba") && !signedText.Contains("PENDIENTE DE FIRMA HN"), "DOCX final contiene ambas firmas independientes");
        email.Fail = true;
        try { await signing.NotifyCompletedAsync(doc.Id); check(false, "SMTP debe fallar"); }
        catch (InvalidOperationException) { }
        await db.Entry(doc).ReloadAsync();
        check(doc.IsFullySigned && doc.CompletionEmailSentAt == null && doc.PdfStoragePath != null, "Fallo SMTP conserva firmas y deja confirmación reintentable");
        email.Fail = false;
        await signing.NotifyCompletedAsync(doc.Id);
        check(email.Messages.Count == 1 && email.Messages[0].To == client.Email
            && DocumentNotificationRecipients.Parse(SystemConfiguration.DefaultDocumentNotificationEmails).All(x => email.Messages[0].Cc.Contains(x))
            && email.Messages[0].Cc.Contains(doc.SignedByEmail!), "Confirmación al cliente, firmante y ambos administradores");
        check(email.Messages[0].Attachment!.SequenceEqual(RecordingConverter.Pdf)
            && email.Messages[0].AttachmentName!.EndsWith(".pdf") && doc.CompletionEmailSentAt.HasValue, "Correo adjunta el PDF final y registra envío");
        await signing.NotifyCompletedAsync(doc.Id);
        check(email.Messages.Count == 1, "Reintentar confirmación enviada no duplica correos");

        var publicPage = new ClientDocumentModel(db, files, signing, NullLogger<ClientDocumentModel>.Instance);
        check(await publicPage.OnPostSignAsync(doc.PublicToken, "Otro nombre", "otro@example.test", "", true) is RedirectToPageResult
            && doc.SignedByName == "Representante del cliente", "No permite sobrescribir una firma existente");
        var expired = new ClientLegalDocument { Client = client, PublicToken = Guid.NewGuid().ToString("N"), Status = ClientLegalDocumentStatus.SentForSignature, TokenExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        db.Add(expired);
        await db.SaveChangesAsync();
        check(await publicPage.OnPostSignAsync(expired.PublicToken, "Nombre", "otro@example.test", "", true) is NotFoundResult, "Enlace vencido no permite firmar");
        check(await publicPage.OnGetDownloadPdfAsync(expired.PublicToken) is NotFoundResult, "Enlace vencido no permite descargar borrador");
        db.Remove(expired);
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PublicLinks:BaseUrl"] = "https://example.test" }).Build();
        var welcome = new WelcomeModel(db, new DeniedActions(), email, configuration, NullLogger<WelcomeModel>.Instance, new DocumentNotificationRecipients(db));
        var context = new DefaultHttpContext();
        welcome.PageContext = new PageContext { HttpContext = context };
        welcome.TempData = new TempDataDictionary(context, new MemoryTempData());
        check(await welcome.OnPostAsync(client.Id) is ForbidResult, "Bienvenida exige permisos en servidor");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, AppRoles.Admin) }, "test"));
        check(await welcome.OnPostAsync(client.Id) is RedirectToPageResult && email.Messages.Count == 2, "Botón de bienvenida envía por el servicio SMTP");
        check(email.Messages[^1].Cc.SequenceEqual(DocumentNotificationRecipients.Parse(SystemConfiguration.DefaultDocumentNotificationEmails)), "Bienvenida incluye siempre ambas copias");
        client.Email = "correo-inválido";
        await db.SaveChangesAsync();
        check(await welcome.OnPostAsync(client.Id) is PageResult && email.Messages.Count == 2, "Correo inválido bloquea envío y muestra error");

        client.Email = "cliente@example.test";
        db.SystemConfigurations.Add(new SystemConfiguration { DocumentNotificationEmails = "contratos@example.test" });
        var adminUser = new ApplicationUser { Id = "admin-test", UserName = "Admin Test", Email = "admin@example.test" };
        db.Users.Add(adminUser);
        var secondDoc = new ClientLegalDocument
        {
            Client = client, Title = "Segundo contrato", SignedAt = DateTime.UtcNow,
            SignedByName = "Cliente de prueba", SignedByEmail = client.Email,
            PublicToken = Guid.NewGuid().ToString("N"), Status = ClientLegalDocumentStatus.SentForSignature
        };
        db.Add(secondDoc);
        await db.SaveChangesAsync();
        var documentsPage = new DocumentsModel(db, null!, files, email, configuration, templates, converter, signing,
            new DeniedActions(), NullLogger<DocumentsModel>.Instance, new DocumentNotificationRecipients(db));
        var adminContext = new DefaultHttpContext();
        documentsPage.PageContext = new PageContext { HttpContext = adminContext };
        documentsPage.TempData = new TempDataDictionary(adminContext, new MemoryTempData());
        check(await documentsPage.OnPostSignProviderAsync(client.Id, secondDoc.Id, true) is ForbidResult, "Solo administradores pueden firmar por HN");
        adminContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, AppRoles.Admin), new Claim(ClaimTypes.NameIdentifier, adminUser.Id) }, "test"));
        check(await documentsPage.OnPostSignProviderAsync(client.Id, secondDoc.Id, false) is BadRequestObjectResult && secondDoc.ProviderSignedAt == null, "Firma HN exige aceptación explícita");
        check(await documentsPage.OnPostSignProviderAsync(client.Id, secondDoc.Id, true) is RedirectToPageResult
            && secondDoc.ProviderSignedByUserId == adminUser.Id && secondDoc.IsFullySigned
            && email.Messages.Count == 3, "Firma HN registra identidad autenticada y envía contrato final");
        check(email.Messages[^1].Cc.Contains("contratos@example.test")
            && !email.Messages[^1].Cc.Contains("jsantana@hubnet-solutions.net"), "Contratos firmados respetan configuración de copias actualizada");
        check(await documentsPage.OnPostSignProviderAsync(client.Id, secondDoc.Id, true) is RedirectToPageResult
            && email.Messages.Count == 3, "Doble envío del formulario de firma no duplica confirmación");
        await documentsPage.OnPostRegeneratePdfAsync(client.Id, secondDoc.Id);
        check(documentsPage.FlashType == "danger" && secondDoc.IsFullySigned, "Regeneración bloqueada cuando el contrato ya tiene firmas");
        await welcome.OnPostAsync(client.Id);
        check(email.Messages.Count == 4 && email.Messages[^1].Cc.SequenceEqual(new[] { "contratos@example.test" }), "Bienvenida usa nuevos destinatarios sin conservar correos eliminados");

        await using (var postgres = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=script_only;Username=test;Password=test").Options))
        {
            var migrationSql = postgres.GetService<IMigrator>().GenerateScript("20260508201704_AddSalesProspectNotes", "20261022000100_AddClientDocumentCountersignature");
            check(migrationSql.Contains("ADD \"ProviderSignedAt\"") && migrationSql.Contains("ADD \"CompletionEmailSentAt\"")
                && !migrationSql.Contains("DROP TABLE"), "Migración PostgreSQL genera columnas sin borrar documentos");
            await File.WriteAllTextAsync("artifacts/welcome-check/migration.sql", migrationSql);
        }

        // A second context cannot overwrite a signature after another request updates the same document.
        await using var competingDb = new ApplicationDbContext(options);
        var stale = await competingDb.ClientLegalDocuments.SingleAsync(x => x.Id == doc.Id);
        doc.UpdatedAt = DateTime.UtcNow.AddSeconds(1);
        await db.SaveChangesAsync();
        stale.SignedByName = "Intento concurrente";
        try { await competingDb.SaveChangesAsync(); check(false, "Actualización concurrente debe fallar"); }
        catch (DbUpdateConcurrencyException) { check(true, "Conflicto concurrente impide sobrescribir firma"); }
    }

    internal static string WordText(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        var xml = XDocument.Load(stream);
        return string.Concat(xml.Descendants(XName.Get("t", "http://schemas.openxmlformats.org/wordprocessingml/2006/main")).Select(x => x.Value));
    }

    internal sealed class RecordingEmail : IEmailSender
    {
        public bool Fail { get; set; }
        public List<(string To, IReadOnlyCollection<string> Cc, byte[]? Attachment, string? AttachmentName)> Messages { get; } = new();
        public Task SendAsync(string toEmail, string subject, string htmlBody, byte[]? attachmentBytes = null, string? attachmentName = null, string? attachmentContentType = null, IReadOnlyCollection<string>? ccEmails = null)
        {
            if (Fail) throw new InvalidOperationException("Simulated SMTP failure");
            Messages.Add((toEmail, ccEmails ?? Array.Empty<string>(), attachmentBytes, attachmentName));
            return Task.CompletedTask;
        }
    }
    internal sealed class RecordingConverter : IOfficePdfConverter
    {
        public static readonly byte[] Pdf = "%PDF-1.7 fake converter for workflow tests"u8.ToArray();
        public byte[]? LastDocx { get; private set; }
        public bool Fail { get; set; }
        public Task<byte[]?> TryConvertDocxToPdfAsync(byte[] docxBytes, string baseFileName, CancellationToken cancellationToken = default)
        { LastDocx = docxBytes; return Task.FromResult<byte[]?>(Fail ? null : Pdf); }
    }
    internal sealed class MemoryFiles : IFileStorage
    {
        private readonly Dictionary<string, byte[]> files = new();
        public Task<(string storagePath, long sizeBytes, string contentType)> SaveBytesAsync(byte[] data, string subFolder, string fileNameWithExt, string contentType)
        { var path = subFolder + "/" + fileNameWithExt; files[path] = data; return Task.FromResult((path, (long)data.Length, contentType)); }
        public Task<(Stream stream, string contentType, string downloadName)> OpenAsync(string storagePath, string downloadName) => Task.FromResult(((Stream)new MemoryStream(files[storagePath]), "application/pdf", downloadName));
        public Task DeleteIfExistsAsync(string storagePath) { files.Remove(storagePath); return Task.CompletedTask; }
        public Task<(string storagePath, long sizeBytes)> SavePdfAsync(IFormFile file, string subFolder, string fileNameNoExt) => throw new NotSupportedException();
        public Task<(string storagePath, long sizeBytes, string contentType, string originalName)> SaveFileAsync(IFormFile file, string subFolder, string fileNameNoExt, string[] allowedExtensions, long maxBytes) => throw new NotSupportedException();
    }
    private sealed class DeniedActions : IActionAccessService
    {
        public Task<bool> HasActionAsync(ClaimsPrincipal user, string actionKey) => Task.FromResult(false);
        public Task<HashSet<string>> GetAllowedActionsAsync(ClaimsPrincipal user) => Task.FromResult(new HashSet<string>());
    }
    internal sealed class MemoryTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
