using System.IO.Compression;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

internal static class AnnexContractTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static async Task RunAsync(ITemplateDocxService templates, Action<bool, string> check)
    {
        var client = new Client
        {
            Name = "CLIENTE DEMO · DOCUMENTO DE PRUEBA", ClientCode = "DEMO-0001",
            LegalRepresentative = "Representante del cliente DEMO", Email = "cliente@example.test",
            FiscalAddress = "Domicilio de demostración, Ciudad de México", State = "Ciudad de México"
        };
        var contract = new ClientServiceContract
        {
            Client = client, ClientId = client.Id, ServiceType = ClientServiceType.Internet,
            Label = "Internet Dedicado", ContractNumber = "DEMO-0001-01", MonthlyAmount = 4500,
            Provider = "Operador de acceso DEMO", Branch = "Sede corporativa DEMO",
            BranchAddress = "Sitio de demostración, Ciudad de México",
            ContractStartDate = new DateTime(2026, 12, 1), ContractEndDate = new DateTime(2027, 12, 1),
            Notes = CompleteNotes()
        };
        var createdAt = new DateTime(2026, 11, 19, 18, 0, 0, DateTimeKind.Utc);
        var provider = new ContractProviderIdentity("HN DEMO · Datos de prueba", "AAA010101AAA",
            "Domicilio completo DEMO, Ciudad de México", "01000");
        var image = File.ReadAllBytes("HNControl.Web.Tests/Fixtures/contract-signature.png");
        var doc = new ClientLegalDocument
        {
            Client = client, ClientId = client.Id, ClientServiceContract = contract,
            ClientServiceContractId = contract.Id, DocumentType = ClientLegalDocumentType.Contract,
            Title = "Contrato de demostración con Anexo de Servicios", CreatedAt = createdAt,
            Status = ClientLegalDocumentStatus.Signed,
            SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract, provider: provider),
            SignedAt = createdAt.AddHours(1), SignedByName = "Firma de prueba Cliente DEMO",
            SignedByEmail = "cliente@example.test", ProviderSignedAt = createdAt.AddHours(2),
            ProviderSignedByName = "Firma de prueba HN", ProviderSignedByEmail = "hn@example.test",
            ProviderSignedByUserId = "admin-demo", ClientSignatureImage = image, ProviderSignatureImage = image
        };
        var snapshot = JsonSerializer.Deserialize<ClientSigningSnapshot>(doc.SigningSnapshotJson)!;
        check(snapshot.ServiceTemplateRevision == 4, "Contrato nuevo congela la plantilla de anexo v4");
        check(snapshot.ProviderIdentity == provider && !doc.SigningSnapshotJson.Contains("Smtp", StringComparison.OrdinalIgnoreCase),
            "Snapshot conserva solo identidad fiscal del prestador sin configuración SMTP");
        check(ClientServiceAnnex.MissingRequired(contract).Count == 0, "Contrato DEMO tiene anexo técnico completo");
        var bytes = templates.BuildClientLegalDocx(doc, client, contract);
        var xml = Read(bytes);
        var text = Text(xml);
        check(text.Contains("Anexo A de Servicios") && text.Contains("DEMO-0001-01")
            && text.Contains("Bajada: 100 Mbps") && text.Contains("Subida: 100 Mbps")
            && text.Contains("Simétrico") && text.Contains("Reventa de servicio")
            && text.Contains("Operador de acceso DEMO") && text.Contains("Puerto GE1 del router DEMO")
            && text.Contains("01/12/2026 al 01/12/2027") && text.Contains("12 meses"),
            "Anexo emitido identifica capacidades, modalidad, operador, entrega y vigencia reales");
        check(!Regex.IsMatch(text, @"\bANEXO[A-Z]{2,}\b"), "Documento v4 no deja placeholders de anexo sin resolver");
        check(!text.Contains("30%") && !text.Contains("30 %"), "Documento v4 no impone penalidad automática de 30 por ciento");
        check(Cell(xml, "RFC") == "No registrado"
            && Cell(xml, "Estado y código postal").Contains("No registrado")
            && !Cell(xml, "Estado y código postal").Contains("00000"),
            "Datos fiscales ausentes se declaran sin inventar RFC ni código postal");
        check(Cell(xml, "Título y titular declarados") == "No especificado · No especificado"
            && Cell(xml, "Registro del modelo PROFECO") == "No especificado"
            && text.Contains("no acredita por sí mismo una concesión, autorización ni registro"),
            "Contrato no presenta títulos o registros pendientes como autorizaciones concedidas");
        check(text.Contains("99.85%") && text.Contains("WhatsApp 5526014566")
            && text.Contains("soporte@hubnet-solutions.com.mx") && text.Contains("https://support.hubnet-solutions.net"),
            "Anexo integra disponibilidad y canales de soporte confirmados");
        check(text.Contains(provider.LegalName!) && text.Contains(provider.Rfc!)
            && text.Contains(provider.FiscalAddress!) && text.Contains(provider.PostalCode!),
            "Contrato identifica razón social, RFC, domicilio y código postal del prestador");
        using (var zip = new ZipArchive(new MemoryStream(bytes)))
            check(zip.GetEntry("word/media/autograph-client.png") != null
                && zip.GetEntry("word/media/autograph-hn.png") != null
                && Regex.Matches(text, @"Hash(?: de firma)?:\s*[A-F0-9]{16}").Count == 2,
                "Anexo y contrato final conservan ambas firmas autógrafas y sus hashes");
        Directory.CreateDirectory("artifacts/sla-annex");
        await File.WriteAllBytesAsync("artifacts/sla-annex/contrato-demostracion.docx", bytes);

        // Signing must use the captured provider and specifications, even after service edits.
        contract.Provider = "OPERADOR CAMBIADO POSTERIORMENTE";
        contract.BranchAddress = "DIRECCIÓN CAMBIADA POSTERIORMENTE";
        contract.Notes = "[META] InternetCapacidad=999\n[META] AnexoRed=RedPropia";
        client.Name = "CLIENTE MODIFICADO POSTERIORMENTE";
        var frozenText = Text(Read(templates.BuildClientLegalDocx(doc, client, contract)));
        check(frozenText == text && snapshot.Contract?.Provider == "Operador de acceso DEMO",
            "Cambiar proveedor, ubicación, capacidades o cliente no modifica el contrato congelado");

        doc.SigningSnapshotJson = JsonSerializer.Serialize(snapshot with { ServiceTemplateRevision = 3 });
        var historical = Text(Read(templates.BuildClientLegalDocx(doc, client, contract)));
        check(!historical.Contains("Anexo A de Servicios") && historical.Contains("30% de rentas devengadas")
            && historical.Contains("CLIENTE DEMO"),
            "Contrato histórico v3 mantiene su plantilla y condiciones sin recibir anexo v4");
        await CheckIssuanceGateAsync(templates, check);
    }

    private static string CompleteNotes() => ClientServiceAnnex.Write(
        "DEMONSTRACIÓN: datos y firmas de prueba, sin validez comercial.\n[META] Recurrencia=Mensual\n[META] Plazo=12\n[META] InternetCapacidad=100\n[META] CostoInstalacion=1500",
        new ClientServiceAnnexData
        {
            Modality = "Dedicado", NetworkMode = "Reventa", UploadCapacity = "100 Mbps", Symmetry = "Simetrico",
            InstallationDate = new DateTime(2026, 12, 1),
            AccessMedium = "Fibra óptica", DeliveryPoint = "Puerto GE1 del router DEMO, interfaz Ethernet",
            PublicIpConditions = "Una IP pública fija para esta demostración",
            Latency = "Hasta 20 ms (dato DEMO)", Jitter = "Hasta 5 ms (dato DEMO)",
            PacketLoss = "Hasta 0.1% (dato DEMO)",
            MeasurementScope = "DEMO: NOC a puerto de entrega, prueba cada 60 segundos",
            EscalationContact = "Responsable de operaciones DEMO, por los canales NOC"
        });

    private static async Task CheckIssuanceGateAsync(ITemplateDocxService templates, Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var client = new Client { Name = "Prueba de emisión", Email = "cliente@example.test" };
        var contract = new ClientServiceContract { Client = client, ServiceType = ClientServiceType.Internet,
            Label = "Internet", Notes = "[META] Plazo=12" };
        db.SystemConfigurations.Add(new SystemConfiguration
        {
            CompanyName = "HN DEMO", CompanyLegalName = "Prestador DEMO · Datos de prueba",
            CompanyRfc = "HND010101AA1", CompanyFiscalAddress = "Domicilio fiscal de demostración",
            CompanyFiscalZipCode = "01000", SmtpUser = "smtp-user-not-for-contract",
            SmtpPasswordProtected = "protected-secret-not-for-contract"
        });
        db.Add(contract);
        await db.SaveChangesAsync();
        var email = new ClientCommunicationTests.RecordingEmail();
        var files = new ClientCommunicationTests.MemoryFiles();
        var converter = new ClientCommunicationTests.RecordingConverter();
        var recipients = new DocumentNotificationRecipients(db);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["PublicLinks:BaseUrl"] = "https://example.test" }).Build();
        var signing = new ClientDocumentSigningService(db, files, templates, converter, email, recipients);
        var page = new DocumentsModel(db, null!, files, email, configuration, templates, converter, signing,
            new NoActions(), NullLogger<DocumentsModel>.Instance, recipients);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Role, AppRoles.Admin) }, "test")) };
        page.PageContext = new PageContext { HttpContext = context };
        page.TempData = new TempDataDictionary(context, new ClientCommunicationTests.MemoryTempData());
        check(await page.OnPostGenerateAsync(client.Id, ClientLegalDocumentType.Contract, contract.Id) is RedirectToPageResult
            && page.FlashType == "warning" && page.Flash!.Contains("Anexo")
            && await db.ClientLegalDocuments.CountAsync() == 0 && converter.LastDocx == null,
            "Generación incompleta se bloquea antes de crear documento o convertir PDF");
        var oldSnapshot = ClientSigningSnapshot.Capture(client, contract);
        var draft = new ClientLegalDocument { Client = client, ClientServiceContract = contract,
            PublicToken = Guid.NewGuid().ToString("N"), SigningSnapshotJson = oldSnapshot,
            PdfStoragePath = "pdf-previo-sin-modificar.pdf", Status = ClientLegalDocumentStatus.Draft };
        db.Add(draft);
        await db.SaveChangesAsync();
        check(await page.OnPostRegeneratePdfAsync(client.Id, draft.Id) is RedirectToPageResult
            && page.FlashType == "danger" && page.Flash!.Contains("Anexo")
            && converter.LastDocx == null && draft.SigningSnapshotJson == oldSnapshot
            && draft.PdfStoragePath == "pdf-previo-sin-modificar.pdf",
            "Regeneración incompleta conserva PDF y snapshot anteriores");
        contract.Notes = CompleteNotes();
        contract.Provider = "Operador DEMO";
        contract.BranchAddress = "Domicilio de entrega DEMO";
        contract.ContractStartDate = new DateTime(2026, 12, 1);
        await db.SaveChangesAsync();
        check(await page.OnPostGenerateAsync(client.Id, ClientLegalDocumentType.Contract, contract.Id) is RedirectToPageResult
            && page.FlashType == "success" && converter.LastDocx != null
            && draft.PdfStoragePath != "pdf-previo-sin-modificar.pdf"
            && JsonSerializer.Deserialize<ClientSigningSnapshot>(draft.SigningSnapshotJson!)?.ServiceTemplateRevision == 4,
            "Completar el anexo permite generar PDF y congelar revisión v4");
        var configuredProvider = await db.SystemConfigurations.SingleAsync();
        var captured = draft.SigningSnapshotJson!;
        configuredProvider.CompanyRfc = "MODIFICADO";
        configuredProvider.CompanyFiscalAddress = "Domicilio posterior a emisión";
        await db.SaveChangesAsync();
        var saved = JsonSerializer.Deserialize<ClientSigningSnapshot>(captured)!;
        var frozen = Text(Read(templates.BuildClientLegalDocx(draft, client, contract)));
        check(saved.ProviderIdentity?.Rfc == "HND010101AA1" && frozen.Contains("HND010101AA1")
            && frozen.Contains("Domicilio fiscal de demostración") && !frozen.Contains("Domicilio posterior a emisión")
            && draft.SigningSnapshotJson == captured,
            "Cambiar configuración fiscal de HN no modifica identidad del contrato emitido");
        check(!captured.Contains("Smtp", StringComparison.OrdinalIgnoreCase)
            && !captured.Contains("smtp-user-not-for-contract") && !captured.Contains("protected-secret-not-for-contract"),
            "Generación de contrato no incluye credenciales SMTP en el snapshot");
        check(email.Messages.Count == 0, "Preparar o regenerar contrato no envía correos sin solicitarlo");
    }

    private sealed class NoActions : IActionAccessService
    {
        public Task<bool> HasActionAsync(ClaimsPrincipal user, string actionKey) => Task.FromResult(false);
        public Task<HashSet<string>> GetAllowedActionsAsync(ClaimsPrincipal user) => Task.FromResult(new HashSet<string>());
    }

    private static string Cell(XDocument xml, string label)
    {
        var cell = xml.Descendants(W + "tc").First(c => c.Elements(W + "p").FirstOrDefault() is { } first
            && Text(first).Trim() == label);
        var inlineValue = string.Concat(cell.Elements(W + "p").Skip(1).Select(Text)).Trim();
        return inlineValue.Length > 0 ? inlineValue
            : string.Concat(cell.ElementsAfterSelf(W + "tc").Select(Text)).Trim();
    }
    private static string Text(XContainer xml) => string.Concat(xml.Descendants(W + "t").Select(x => x.Value));
    private static XDocument Read(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        return XDocument.Load(stream);
    }
}
