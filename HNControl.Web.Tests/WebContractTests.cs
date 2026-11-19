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

internal static class WebContractTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly DateTime DemoDate = new(2026, 11, 19, 18, 0, 0, DateTimeKind.Utc);
    private static readonly ContractProviderIdentity DemoProvider = new("PRESTADOR DEMO · DATOS FICTICIOS",
        "DEM010101AA1", "Domicilio fiscal ficticio DEMO, Ciudad de México", "01000");
    private const string InternalNote = "NOTA_INTERNA_NO_PUBLICAR_4829";
    private const string Credential = "CREDENCIAL_PRIVADA_NO_PUBLICAR_7632";

    public static async Task RunAsync(ITemplateDocxService templates, Action<bool, string> check)
    {
        var client = DemoClient();
        var contract = WebContract(client, "Check-In Web DEMO", WebServiceContract.CheckInUrl, 4500m, 500m);
        var document = SignedDocument(client, contract);
        var snapshot = JsonSerializer.Deserialize<ClientSigningSnapshot>(document.SigningSnapshotJson!)!;
        check(snapshot.WebTemplateRevision == 1 && snapshot.ProviderIdentity == DemoProvider,
            "Contrato web congela revisión propia e identidad fiscal del prestador");
        check(JsonSerializer.Deserialize<ClientSigningSnapshot>(ClientSigningSnapshot.Capture(client,
                new ClientServiceContract { ServiceType = ClientServiceType.Internet }))!.WebTemplateRevision == null
            && JsonSerializer.Deserialize<ClientSigningSnapshot>(ClientSigningSnapshot.Capture(client, contract,
                corporateServiceTemplate: false))!.WebTemplateRevision == null,
            "Captura estándar o histórica no convierte el documento en contrato web");
        check(!document.SigningSnapshotJson!.Contains(Credential),
            "Snapshot web excluye credenciales de cuenta y portal del servicio");

        var bytes = templates.BuildClientLegalDocx(document, client, contract);
        var xml = Read(bytes);
        var text = Text(xml);
        check(text.Contains("Contrato de servicios web") && text.Contains("Check-In Web DEMO")
            && text.Contains(WebServiceContract.CheckInUrl) && text.Contains("ALCANCE PÚBLICO DEMO")
            && text.Contains("25 empleados") && text.Contains("Plan de prueba DEMO"),
            "DOCX web incorpora plataforma, URL, alcance y capacidad seleccionados");
        check(text.Contains(DemoProvider.LegalName!) && text.Contains(DemoProvider.Rfc!)
            && text.Contains(DemoProvider.FiscalAddress!) && text.Contains(DemoProvider.PostalCode!),
            "DOCX web utiliza la identidad HN capturada para ese documento");
        check(!text.Contains(InternalNote) && !text.Contains(Credential)
            && !text.Contains("[META]") && !text.Contains("InternetCapacidad=999"),
            "DOCX web no imprime notas internas, metadatos ni credenciales");
        check(!Regex.IsMatch(text, @"\b(?:WEB[A-Z]{3,}|ANEXO[A-Z]{3,}|HNNOMBRELEGAL|HNREPRESENTANTE|HNRFC|HNDOMICILIO|HNCP|FIRMACLIENTE|FIRMAHN)\b"),
            "Contrato web resuelve los campos de datos, importes y firmas");
        check(!text.Contains("99.85") && !Regex.IsMatch(text, @"\b72\s*(?:h|horas)\b", RegexOptions.IgnoreCase)
            && !Regex.IsMatch(text, @"30\s*%") && !text.Contains("Ley en Materia de Telecomunicaciones")
            && !text.Contains("NOM-184") && !text.Contains("Máximo contractual: 4 horas"),
            "SaaS no hereda disponibilidad, reparación, penalidad ni régimen del contrato de Internet");
        check(text.Contains("Mensual, sin plazo forzoso") && text.Contains("Corte: día 1. Pago: día 15")
            && text.Contains("Después de la baja") && text.Contains("durante un año")
            && text.Contains("Esta conservación contractual") && text.Contains("ni sustituye los plazos específicos")
            && text.Contains("supresión u otros derechos cuando procedan"),
            "SaaS conserva mensualidad sin forzoso y custodia anual contractual sujeta a derechos de datos");
        check(RowText(xml, "Suscripción mensual").Contains("4,500.00")
            && RowText(xml, "Suscripción mensual").Contains("5,220.00")
            && RowText(xml, "Alta y configuración inicial").Contains("500.00")
            && RowText(xml, "Alta y configuración inicial").Contains("580.00"),
            "DOCX muestra mensualidad y alta con IVA de 16 por ciento calculado por separado");
        CheckSignatures(bytes, check, "Contrato web");

        var originalSnapshot = document.SigningSnapshotJson;
        client.Name = "CLIENTE CAMBIADO DESPUÉS DE FIRMAR";
        contract.MonthlyAmount = 99999;
        contract.Label = "SERVICIO CAMBIADO DESPUÉS DE FIRMAR";
        contract.Notes = WebServiceContract.Write("[META] CostoInstalacion=99999", new WebServiceContractData
        {
            PlatformName = "OTRA PLATAFORMA", ServiceUrl = WebServiceContract.GpsUrl,
            IncludedScope = "ALCANCE CAMBIADO", SupportHours = "OTRO HORARIO"
        }, true);
        check(Text(Read(templates.BuildClientLegalDocx(document, client, contract))) == text
            && document.SigningSnapshotJson == originalSnapshot,
            "Ediciones posteriores no alteran plataforma, cliente, importes, alcance ni firmas congelados");

        var minimalClient = DemoClient();
        var minimal = WebContract(minimalClient, "Plataforma sin plan ni cupo DEMO", "https://saas.example.test/", 1000, 0);
        var minimalData = WebServiceContract.Read(minimal.Notes);
        minimalData.PlanName = null;
        minimalData.LicensedQuantity = null;
        minimalData.LicenseUnit = null;
        minimal.Notes = WebServiceContract.Write(minimal.Notes, minimalData, true);
        var minimalXml = Read(templates.BuildClientLegalDocx(DraftDocument(minimalClient, minimal), minimalClient, minimal));
        check(!HasRow(minimalXml, "Plan contratado") && !HasRow(minimalXml, "Usuarios o unidades incluidos")
            && !Text(minimalXml).Contains("No especificad"),
            "Plan y capacidad opcionales ausentes no dejan filas pendientes en el contrato web");
        CheckFrozenRouting(templates, check);
        await CheckIssuanceGateAsync(templates, check);
        await CheckWelcomeAsync(check);
        WriteDemonstrations(templates, check);
    }

    private static void CheckFrozenRouting(ITemplateDocxService templates, Action<bool, string> check)
    {
        foreach (var eventual in new[] { false, true })
        {
            var client = DemoClient();
            var contract = new ClientServiceContract
            {
                Client = client, ClientId = client.Id, ServiceType = ClientServiceType.Internet,
                Label = eventual ? "Internet para evento DEMO" : "Internet Dedicado DEMO",
                ContractNumber = "DEMO-HIST-01", MonthlyAmount = 4500,
                BranchAddress = "Sede ficticia DEMO", ContractStartDate = new DateTime(2026, 11, 20),
                ContractEndDate = eventual ? new DateTime(2026, 11, 21) : null,
                Notes = eventual ? "[META] Recurrencia=Eventual" :
                    "[META] Recurrencia=Mensual\n[META] Plazo=Indefinido\n[META] AnexoModalidad=Dedicado\n[META] InternetCapacidad=100"
            };
            var doc = DraftDocument(client, contract);
            var baseline = Text(Read(templates.BuildClientLegalDocx(doc, client, contract)));
            contract.Notes = WebServiceContract.Write(contract.Notes, new WebServiceContractData
            {
                PlatformName = "CAMBIO A WEB POSTERIOR", ServiceUrl = WebServiceContract.CheckInUrl,
                IncludedScope = "Alcance posterior", SupportHours = "Horario posterior"
            }, true);
            var rendered = Text(Read(templates.BuildClientLegalDocx(doc, client, contract)));
            check(rendered == baseline && !rendered.Contains("Contrato de servicios web")
                && rendered.Contains(eventual ? "Contrato de conectividad temporal para eventos" : "99.85%"),
                $"Contrato {(eventual ? "eventual" : "Internet")} congelado conserva su formato aunque el servicio actual pase a SaaS");
        }

        var webClient = DemoClient();
        var web = WebContract(webClient, "NDA con servicio web DEMO", WebServiceContract.GpsUrl, 1000, 0);
        var nda = DraftDocument(webClient, web);
        nda.DocumentType = ClientLegalDocumentType.NDA;
        var ndaText = Text(Read(templates.BuildClientLegalDocx(nda, webClient, web)));
        check(!ndaText.Contains("Contrato de servicios web") && !ndaText.Contains("ALCANCE PÚBLICO DEMO")
            && ndaText.Contains("CONFIDENCIAL", StringComparison.OrdinalIgnoreCase),
            "NDA conserva su formato aunque el snapshot incluya la variante web");

        var historical = DraftDocument(webClient, web);
        historical.PdfStoragePath = "documento-historico.pdf";
        historical.SigningSnapshotJson = ClientSigningSnapshot.Capture(webClient, web, corporateServiceTemplate: false);
        check(!Text(Read(templates.BuildClientLegalDocx(historical, webClient, web))).Contains("Contrato de servicios web"),
            "Documento histórico sin revisión SaaS mantiene su plantilla original");
    }

    private static async Task CheckIssuanceGateAsync(ITemplateDocxService templates, Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var client = DemoClient();
        var contract = WebContract(client, "", WebServiceContract.CheckInUrl, 4500, 500);
        var settings = new SystemConfiguration
        {
            CompanyName = "Marca de prueba", CompanyLegalName = "HN CONFIG DEMO",
            CompanyRfc = "CON010101AA1", CompanyFiscalAddress = "Domicilio configurado DEMO",
            CompanyFiscalZipCode = "01000", SmtpUser = "smtp-privado-no-imprimir",
            SmtpPasswordProtected = "smtp-secreto-protegido-no-imprimir"
        };
        db.Add(contract);
        db.Add(settings);
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
            && page.FlashType == "warning" && page.Flash!.Contains("plataforma")
            && await db.ClientLegalDocuments.CountAsync() == 0 && converter.LastDocx == null,
            "Emisión SaaS incompleta se bloquea antes de crear documento o convertir PDF");

        var previous = DraftDocument(client, contract);
        previous.Title = "Título anterior inmutable ante error";
        previous.TermsBody = "Condiciones anteriores";
        previous.PdfStoragePath = "pdf-previo.pdf";
        var previousSnapshot = previous.SigningSnapshotJson;
        db.Add(previous);
        await db.SaveChangesAsync();
        check(await page.OnPostRegeneratePdfAsync(client.Id, previous.Id) is RedirectToPageResult
            && page.FlashType == "danger" && page.Flash!.Contains("plataforma")
            && previous.Title == "Título anterior inmutable ante error" && previous.TermsBody == "Condiciones anteriores"
            && previous.SigningSnapshotJson == previousSnapshot && previous.PdfStoragePath == "pdf-previo.pdf"
            && converter.LastDocx == null,
            "Regeneración SaaS incompleta conserva PDF, título, condiciones y snapshot anteriores");

        var complete = WebServiceContract.Read(contract.Notes);
        complete.PlatformName = "Check-In Web CONFIG DEMO";
        contract.Notes = WebServiceContract.Write(contract.Notes, complete, true);
        settings.CompanyRfc = "";
        await db.SaveChangesAsync();
        check(await page.OnPostGenerateAsync(client.Id, ClientLegalDocumentType.Contract, contract.Id) is RedirectToPageResult
            && page.FlashType == "warning" && page.Flash!.Contains("Configuración")
            && await db.ClientLegalDocuments.CountAsync() == 1 && previous.SigningSnapshotJson == previousSnapshot
            && previous.Title == "Título anterior inmutable ante error" && converter.LastDocx == null,
            "Emisión SaaS exige identidad fiscal HN antes de modificar un documento existente");
        check(await page.OnPostRegeneratePdfAsync(client.Id, previous.Id) is RedirectToPageResult
            && page.FlashType == "danger" && previous.SigningSnapshotJson == previousSnapshot
            && previous.PdfStoragePath == "pdf-previo.pdf" && converter.LastDocx == null,
            "Regeneración SaaS sin identidad del prestador preserva el documento anterior");

        settings.CompanyRfc = "CON010101AA1";
        await db.SaveChangesAsync();
        check(contract.BranchAddress.Length == 0 && WebServiceContract.MissingRequired(contract).Count == 0,
            "La emisión SaaS no exige domicilio técnico de instalación de Internet");
        check(await page.OnPostGenerateAsync(client.Id, ClientLegalDocumentType.Contract, contract.Id) is RedirectToPageResult
            && page.FlashType == "success" && converter.LastDocx != null,
            "Servicio web completo genera su documento sin domicilio técnico ni anexo de Internet");
        var issued = await db.ClientLegalDocuments.SingleAsync();
        var issuedSnapshot = JsonSerializer.Deserialize<ClientSigningSnapshot>(issued.SigningSnapshotJson!)!;
        var issuedText = Text(Read(converter.LastDocx!));
        check(issuedSnapshot.WebTemplateRevision == 1 && issuedSnapshot.ProviderIdentity?.Rfc == "CON010101AA1"
            && issuedText.Contains("HN CONFIG DEMO") && issuedText.Contains("Domicilio configurado DEMO")
            && issuedText.Contains("Check-In Web CONFIG DEMO"),
            "Emisión integra Configuración HN y congela la revisión SaaS");
        check(!issued.SigningSnapshotJson!.Contains("smtp-privado") && !issued.SigningSnapshotJson.Contains("smtp-secreto")
            && !issuedText.Contains("smtp-privado") && !issuedText.Contains("smtp-secreto")
            && !issuedText.Contains(InternalNote) && !issuedText.Contains(Credential),
            "La generación desde página no publica configuración SMTP, notas ni claves");
        var captured = issued.SigningSnapshotJson;
        settings.CompanyLegalName = "HN CONFIG MODIFICADA";
        settings.CompanyRfc = "RFC-MODIFICADO";
        await db.SaveChangesAsync();
        check(Text(Read(templates.BuildClientLegalDocx(issued, client, contract))) == issuedText
            && issued.SigningSnapshotJson == captured && email.Messages.Count == 0,
            "Cambiar Configuración no altera el PDF preparado y generar no envía correos");

        issued.ProviderSignedAt = DemoDate;
        issued.ProviderSignedByName = "Firmante HN DEMO";
        await db.SaveChangesAsync();
        var signedPath = issued.PdfStoragePath;
        check(await page.OnPostRegeneratePdfAsync(client.Id, issued.Id) is RedirectToPageResult
            && page.FlashType == "danger" && issued.SigningSnapshotJson == captured && issued.PdfStoragePath == signedPath,
            "La presencia de firma bloquea regenerar el contrato SaaS y conserva sus condiciones");
    }

    private static async Task CheckWelcomeAsync(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var client = DemoClient();
        var web = WebContract(client, "Plataforma Web Original DEMO", WebServiceContract.CheckInUrl, 4500, 500);
        var webData = WebServiceContract.Read(web.Notes);
        webData.IncludedScope = "Alcance congelado DEMO <em>texto del cliente</em>";
        web.Notes = WebServiceContract.Write(web.Notes, webData, true);
        var webDoc = DraftDocument(client, web);
        webDoc.PdfStoragePath = "web-demo.pdf";
        var nda = DraftDocument(client, web);
        nda.DocumentType = ClientLegalDocumentType.NDA;
        nda.PdfStoragePath = "nda-demo.pdf";
        ClientServiceContract InternetContract(bool eventual) => new()
        {
            Client = client, ClientId = client.Id, ServiceType = ClientServiceType.Internet,
            Label = eventual ? "Evento congelado DEMO" : "Internet congelado DEMO",
            ContractNumber = eventual ? "DEMO-EVENTO" : "DEMO-INTERNET", MonthlyAmount = 4500,
            ContractStartDate = new DateTime(2026, 11, 20),
            ContractEndDate = eventual ? new DateTime(2026, 11, 21) : null,
            Notes = eventual ? "[META] Recurrencia=Eventual" : "[META] Recurrencia=Mensual\n[META] Plazo=12"
        };
        var internet = InternetContract(false);
        var internetDoc = DraftDocument(client, internet);
        internetDoc.PdfStoragePath = "internet-demo.pdf";
        var eventContract = InternetContract(true);
        var eventDoc = DraftDocument(client, eventContract);
        eventDoc.PdfStoragePath = "evento-demo.pdf";
        var historical = WebContract(client, "Web heredada DEMO", WebServiceContract.GpsUrl, 2000, 0);
        var historicalDoc = DraftDocument(client, historical);
        historicalDoc.SigningSnapshotJson = ClientSigningSnapshot.Capture(client, historical, corporateServiceTemplate: false);
        historicalDoc.PdfStoragePath = "historico-demo.pdf";
        // Existing documents must remain the source of the welcome even if their live service changes variant.
        foreach (var contract in new[] { internet, eventContract })
            contract.Notes = WebServiceContract.Write(contract.Notes, webData, true);
        web.Notes = "[META] Recurrencia=Mensual\n[META] Plazo=36";
        web.ServiceType = ClientServiceType.Internet;
        web.MonthlyAmount = 99999;
        db.AddRange(webDoc, nda, internetDoc, eventDoc, historicalDoc);
        db.Add(new SystemConfiguration { DocumentNotificationEmails = "copia-demo@example.test" });
        await db.SaveChangesAsync();
        var email = new WelcomeEmail();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["PublicLinks:BaseUrl"] = "https://example.test" }).Build();
        WelcomeModel Page(Guid? selected = null)
        {
            var page = new WelcomeModel(db, new NoActions(), email, configuration, NullLogger<WelcomeModel>.Instance,
                new DocumentNotificationRecipients(db)) { ContractId = selected };
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Role, AppRoles.Admin) }, "test")) };
            page.PageContext = new PageContext { HttpContext = context };
            page.TempData = new TempDataDictionary(context, new ClientCommunicationTests.MemoryTempData());
            return page;
        }
        var unselected = Page();
        await unselected.OnGetAsync(client.Id);
        check(!unselected.CanSend && unselected.ContractId == null && unselected.HtmlBody == "",
            "Cliente con Internet, evento y SaaS debe seleccionar contrato antes de preparar bienvenida");
        var selected = Page(web.Id);
        await selected.OnGetAsync(client.Id);
        var html = selected.HtmlBody;
        check(selected.CanSend && selected.Subject == ClientCommunicationTemplates.WebWelcomeSubject
            && html.Contains("Plataforma Web Original DEMO") && html.Contains("5,220.00")
            && html.Contains("25 empleados") && html.Contains(WebServiceContract.CheckInUrl)
            && html.Contains("Alcance congelado DEMO &lt;em&gt;texto del cliente&lt;/em&gt;")
            && !html.Contains("<em>texto del cliente</em>") && !html.Contains("99,999")
            && !html.Contains(InternalNote) && !html.Contains(Credential),
            "Bienvenida SaaS usa alcance, precio, licencias y acceso del snapshot y escapa texto del cliente");
        check(html.Contains("mensual sin plazo forzoso") && html.Contains("día 1 y pago el día 15")
            && html.Contains("canal seguro") && html.Contains("durante un año") && html.Contains("exportación")
            && html.Contains("derechos de supresión") && html.Contains("plazos legales")
            && html.Contains("no mantiene activa la suscripción") && html.Contains("5526014566")
            && html.Contains("soporte@hubnet-solutions.com.mx") && html.Contains("https://support.hubnet-solutions.net")
            && !html.Contains("99.85") && !html.Contains("72 horas") && !html.Contains("cuatro horas")
            && !html.Contains("Anexo A") && !html.Contains("24/7"),
            "Bienvenida SaaS explica mensualidad, soporte pactado y custodia anual con derechos sin promesas de telecomunicaciones");
        check(html.Contains(webDoc.PublicToken) && !html.Contains(nda.PublicToken)
            && !html.Contains(internetDoc.PublicToken) && !html.Contains(eventDoc.PublicToken)
            && !html.Contains(historicalDoc.PublicToken) && webDoc.SentAt == null
            && webDoc.Status == ClientLegalDocumentStatus.Draft,
            "Vista previa SaaS contiene solo el contrato elegido y no activa ni envía documentos");
        check(await selected.OnPostAsync(client.Id) is RedirectToPageResult && email.Messages.Count == 1
            && email.Messages[0].Subject == ClientCommunicationTemplates.WebWelcomeSubject
            && email.Messages[0].Html == selected.HtmlBody && email.Messages[0].To == client.Email
            && email.Messages[0].Cc.SequenceEqual(new[] { "copia-demo@example.test" })
            && webDoc.SentAt.HasValue && webDoc.Status == ClientLegalDocumentStatus.SentForSignature
            && internetDoc.SentAt == null && eventDoc.SentAt == null && historicalDoc.SentAt == null && nda.SentAt == null,
            "Envío simulado SaaS utiliza asunto propio, cliente y copias configuradas; solo marca su documento");
        var internetPage = Page(internet.Id);
        await internetPage.OnGetAsync(client.Id);
        check(internetPage.CanSend && internetPage.Subject == ClientCommunicationTemplates.WelcomeSubject
            && internetPage.HtmlBody.Contains("99.85%") && !internetPage.HtmlBody.Contains("Sus servicios web")
            && internetPage.HtmlBody.Contains(internetDoc.PublicToken) && !internetPage.HtmlBody.Contains(webDoc.PublicToken),
            "Bienvenida de Internet conserva SLA y plantilla del snapshot aunque el servicio actual sea web");
        var eventPage = Page(eventContract.Id);
        await eventPage.OnGetAsync(client.Id);
        check(eventPage.CanSend && eventPage.Subject == ClientCommunicationTemplates.EventWelcomeSubject
            && eventPage.HtmlBody.Contains("20/11/2026 al 21/11/2026")
            && eventPage.HtmlBody.Contains(eventDoc.PublicToken) && !eventPage.HtmlBody.Contains("Sus servicios web"),
            "Bienvenida eventual conserva fechas y asunto del contrato congelado aunque el servicio actual sea web");
        var legacyPage = Page(historical.Id);
        await legacyPage.OnGetAsync(client.Id);
        check(legacyPage.CanSend && legacyPage.Subject == ClientCommunicationTemplates.WelcomeSubject
            && !legacyPage.HtmlBody.Contains("Sus servicios web") && !legacyPage.HtmlBody.Contains("99.85%")
            && legacyPage.HtmlBody.Contains(historicalDoc.PublicToken),
            "Bienvenida histórica sin revisión SaaS no adopta condiciones web ni SLA posteriores");
    }

    private static void WriteDemonstrations(ITemplateDocxService templates, Action<bool, string> check)
    {
        Directory.CreateDirectory("artifacts/web-contract");
        foreach (var gps in new[] { false, true })
        {
            var client = DemoClient();
            var contract = WebContract(client, gps ? "HN Tracking · GPS DEMO" : "Check-In Web DEMO",
                gps ? WebServiceContract.GpsUrl : WebServiceContract.CheckInUrl, gps ? 2500m : 1500m, 0);
            contract.ContractNumber = gps ? "DEMO-WEB-GPS-001" : "DEMO-WEB-CHECKIN-001";
            var data = WebServiceContract.Read(contract.Notes);
            data.PlanName = "Plan ilustrativo DEMO, sin oferta comercial";
            data.LicensedQuantity = gps ? 10 : 25;
            data.LicenseUnit = gps ? "vehículos" : "empleados";
            data.IncludedScope = gps
                ? "DEMOSTRACIÓN SIN VALIDEZ COMERCIAL. Datos, precios y firmas ficticios. Alcance ilustrativo: consulta de ubicación y reportes de diez vehículos DEMO. No incluye dispositivos, SIM, instalación, video, inmovilización ni garantía antirrobo."
                : "DEMOSTRACIÓN SIN VALIDEZ COMERCIAL. Datos, precios y firmas ficticios. Alcance ilustrativo: registro y consulta de asistencia y reportes para veinticinco empleados DEMO. No incluye biometría, checadores físicos, integraciones ni certificación laboral.";
            data.SupportHours = "Horario DEMO: lunes a viernes, 09:00 a 18:00, Ciudad de México";
            contract.Notes = WebServiceContract.Write(contract.Notes, data, true);
            var bytes = templates.BuildClientLegalDocx(SignedDocument(client, contract), client, contract);
            var text = Text(Read(bytes));
            check(text.Contains("DEMOSTRACIÓN SIN VALIDEZ COMERCIAL")
                && text.Contains(gps ? "10 vehículos" : "25 empleados")
                && text.Contains(gps ? "2,900.00" : "1,740.00"),
                $"Demo {(gps ? "GPS" : "Check-In")} identifica datos ficticios, capacidad y precio con IVA");
            CheckSignatures(bytes, check, gps ? "Demo GPS" : "Demo Check-In");
            File.WriteAllBytes($"artifacts/web-contract/contrato-web-{(gps ? "gps" : "checkin")}-demo.docx", bytes);
        }
    }

    private static Client DemoClient() => new()
    {
        Name = "CLIENTE FICTICIO DEMO · NO ES UN CONTRATO REAL", ClientCode = "DEMO-WEB-001",
        LegalRepresentative = "Representante ficticio DEMO", Email = "cliente@example.test",
        BillingEmail = "facturacion@example.test", Rfc = "DEM010101AA1",
        FiscalAddress = "Domicilio fiscal ficticio DEMO, Ciudad de México", State = "Ciudad de México", FiscalZipCode = "01000"
    };

    private static ClientServiceContract WebContract(Client client, string platform, string url, decimal amount, decimal setup) => new()
    {
        Client = client, ClientId = client.Id, ServiceType = ClientServiceType.Otro,
        Label = "Suscripción de prueba DEMO", ContractNumber = "DEMO-WEB-001-01", MonthlyAmount = amount,
        ContractStartDate = new DateTime(2026, 12, 1),
        AccountNumber = Credential, PortalUsername = Credential, PortalPasswordProtected = Credential,
        PortalUrl = "https://private-account.example.test/", Notes = WebServiceContract.Write(
            InternalNote + "\n[META] InternetCapacidad=999\n[META] CostoInstalacion="
                + setup.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new WebServiceContractData
            {
                PlatformName = platform, ServiceUrl = url, PlanName = "Plan de prueba DEMO",
                IncludedScope = "ALCANCE PÚBLICO DEMO: consulta de asistencia y reportes de prueba; excluye equipos y biometría.",
                LicensedQuantity = 25, LicenseUnit = "empleados", SupportHours = "Horario DEMO: lunes a viernes, 09:00 a 18:00"
            }, true)
    };

    private static ClientLegalDocument DraftDocument(Client client, ClientServiceContract contract) => new()
    {
        Client = client, ClientId = client.Id, ClientServiceContract = contract, ClientServiceContractId = contract.Id,
        DocumentType = ClientLegalDocumentType.Contract, Status = ClientLegalDocumentStatus.Draft,
        Title = "DEMOSTRACIÓN · DATOS FICTICIOS", CreatedAt = DemoDate,
        PublicToken = Guid.NewGuid().ToString("N"), SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract, provider: DemoProvider)
    };

    private static ClientLegalDocument SignedDocument(Client client, ClientServiceContract contract)
    {
        var doc = DraftDocument(client, contract);
        doc.Status = ClientLegalDocumentStatus.Signed;
        doc.SignedAt = DemoDate.AddHours(1);
        doc.SignedByName = "Firma ficticia cliente DEMO";
        doc.SignedByEmail = "cliente@example.test";
        doc.ProviderSignedAt = DemoDate.AddHours(2);
        doc.ProviderSignedByName = "Firma ficticia prestador DEMO";
        doc.ProviderSignedByEmail = "prestador@example.test";
        doc.ProviderSignedByUserId = "firmante-ficticio-demo";
        doc.ClientSignatureImage = File.ReadAllBytes("HNControl.Web.Tests/Fixtures/contract-signature.png");
        doc.ProviderSignatureImage = doc.ClientSignatureImage;
        return doc;
    }

    private static void CheckSignatures(byte[] bytes, Action<bool, string> check, string label)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        var xml = Read(bytes);
        check(zip.GetEntry("word/media/autograph-client.png") != null && zip.GetEntry("word/media/autograph-hn.png") != null
            && xml.Descendants(W + "drawing").Count() >= 2
            && Regex.Matches(Text(xml), @"Hash(?: de firma)?:\s*[A-F0-9]{16}").Count == 2,
            label + " integra firmas autógrafas y registro hash de ambas partes");
    }

    private sealed class WelcomeEmail : IEmailSender
    {
        public List<(string To, string Subject, string Html, IReadOnlyCollection<string> Cc)> Messages { get; } = new();
        public Task SendAsync(string toEmail, string subject, string htmlBody, byte[]? attachmentBytes = null,
            string? attachmentName = null, string? attachmentContentType = null, IReadOnlyCollection<string>? ccEmails = null)
        {
            Messages.Add((toEmail, subject, htmlBody, ccEmails ?? Array.Empty<string>()));
            return Task.CompletedTask;
        }
    }

    private sealed class NoActions : IActionAccessService
    {
        public Task<bool> HasActionAsync(ClaimsPrincipal user, string actionKey) => Task.FromResult(false);
        public Task<HashSet<string>> GetAllowedActionsAsync(ClaimsPrincipal user) => Task.FromResult(new HashSet<string>());
    }

    private static bool HasRow(XDocument xml, string label) => xml.Descendants(W + "tr")
        .Any(row => Text(row.Elements(W + "tc").FirstOrDefault() ?? new XElement(W + "tc")).Trim() == label);
    private static string RowText(XDocument xml, string label) => Text(xml.Descendants(W + "tr")
        .First(row => Text(row.Elements(W + "tc").First()).Trim() == label));
    private static string Text(XContainer xml) => string.Concat(xml.Descendants(W + "t").Select(node => node.Value));
    private static XDocument Read(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        return XDocument.Load(stream);
    }
}
