using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HNControl.Web.Models;
using HNControl.Web.Services;

internal static class EventDeliveryDesignTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static void Run(ITemplateDocxService templates, Action<bool, string> check)
    {
        const string output = "artifacts/event-delivery-design";
        Directory.CreateDirectory(output);
        var time = new DateTime(2026, 11, 8, 16, 0, 0, DateTimeKind.Utc);
        var client = new Client
        {
            Name = "Conectividad de Demostración, S.A. de C.V.", LegalRepresentative = "Andrea García Martínez",
            BillingEmail = "contratos@example.test", Rfc = "XAXX010101000", State = "Ciudad de México",
            FiscalZipCode = "06000", FiscalAddress = "Avenida de la Reforma 123, colonia Centro"
        };
        var contract = new ClientServiceContract
        {
            Label = "Internet para evento", ServiceType = ClientServiceType.Internet, ContractNumber = "HN-DEMO-02",
            ContractStartDate = time.Date, ContractEndDate = time.Date.AddDays(1), MonthlyAmount = 4500,
            BranchAddress = "Salón de convenciones Centro, Ciudad de México",
            Notes = "Cobertura WiFi para el salón principal y registro de asistentes. Montaje y pruebas previos al inicio del evento.\n[META] Recurrencia=Eventual\n[META] InternetCapacidad=20\n[META] CostoInstalacion=1500"
        };
        var document = new ClientLegalDocument
        {
            Id = Guid.Parse("1452926a-3b77-4389-8c59-f2c4c9293945"), CreatedAt = time,
            SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract)
        };
        var delivery = new ProjectDeliveryFormat
        {
            Id = Guid.Parse("15cb3d17-5130-4dc7-9333-cb64b11cef2c"), Title = "Entrega de conectividad para evento",
            DeliveryDate = time.Date, DeliveryLocation = contract.BranchAddress, ReceiverName = client.LegalRepresentative,
            SigningSnapshotJson = DeliverySigningSnapshot.Capture(client, null), ServiceSummary = ProjectDeliveryPayload.Serialize(new DeliveryTemplateData
            {
                NOMBREPROYECTO = "Internet para evento", NOMBRETECNICO = "José López Hernández", IPPUBLICA = "198.51.100.20", SEGMENTOLAN = "192.168.10.0/24",
                Services = [new() { Servicio = "Internet dedicado 20 MB", Modalidad = "Eventual", Plazo = "2 días" },
                            new() { Servicio = "WiFi para el salón principal", Modalidad = "Temporal", Plazo = "2 días" }],
                Equipment = [new() { Equipo = "Router de acceso", Cantidad = "1" }, new() { Equipo = "Punto de acceso WiFi", Cantidad = "3" }]
            })
        };
        var eventBytes = templates.BuildClientLegalDocx(document, client, contract);
        var deliveryBytes = templates.BuildDeliveryDocx(delivery, client, null);
        File.WriteAllBytes(Path.Combine(output, "Evento-pendiente.docx"), eventBytes);
        File.WriteAllBytes(Path.Combine(output, "Entrega-pendiente.docx"), deliveryBytes);
        check(Text(eventBytes).Contains("Internet para evento 20 MB") && Text(eventBytes).Contains("4,500.00") && !Text(eventBytes).Contains("30%"),
            "Nuevo evento conserva capacidad importe y condiciones eventuales");
        check(Text(eventBytes).Contains("Fecha de elaboración: 08/11/2026"), "Fecha de elaboración eventual permanece en la fecha original del documento");
        check(Text(deliveryBytes).Contains("Canal preferente de atención del NOC") && Text(deliveryBytes).Contains("5526014566")
            && Text(deliveryBytes).Contains("soporte@hubnet-solutions.com.mx") && Text(deliveryBytes).Contains("https://support.hubnet-solutions.net"),
            "Nueva acta destaca WhatsApp e incluye correo y portal confirmados");
        check(Text(deliveryBytes).Contains("Temporal") && Text(deliveryBytes).Contains("Eventual"), "Acta conserva la modalidad independiente de cada servicio");
        check(Read(deliveryBytes).Descendants(W + "tbl").Single(t => string.Concat(t.Descendants(W + "t").Select(x => x.Value)).StartsWith("PartidaDescripciónModalidad"))
            .Elements(W + "tr").Count() == 3, "Acta muestra únicamente servicios registrados sin filas vacías marcadas a satisfacción");
        CheckSignatureLayout(eventBytes, false, "Evento", check);
        CheckSignatureLayout(deliveryBytes, false, "Entrega", check);

        document.SignedAt = delivery.SignedAt = time;
        document.SignedByName = delivery.SignedByName = client.LegalRepresentative;
        document.SignedByEmail = delivery.SignedByEmail = "contratos@example.test";
        document.ProviderSignedAt = delivery.ProviderSignedAt = time.AddMinutes(5);
        document.ProviderSignedByName = delivery.ProviderSignedByName = "José López Hernández";
        document.ProviderSignedByEmail = delivery.ProviderSignedByEmail = "ingenieria@example.test";
        document.ProviderSignedByUserId = delivery.ProviderSignedByUserId = "usuario-demostracion";
        var signedEvent = templates.BuildClientLegalDocx(document, client, contract);
        var signedDelivery = templates.BuildDeliveryDocx(delivery, client, null);
        File.WriteAllBytes(Path.Combine(output, "Evento-firmas-demostracion.docx"), signedEvent);
        File.WriteAllBytes(Path.Combine(output, "Entrega-firmas-demostracion.docx"), signedDelivery);
        CheckSignatureLayout(signedEvent, true, "Evento firmado", check);
        CheckSignatureLayout(signedDelivery, true, "Entrega firmada", check);
        client.LegalRepresentative = "Representante cambiado después de emitir";
        check(Text(templates.BuildClientLegalDocx(document, client, contract)).Contains("Andrea García Martínez")
            && !Text(templates.BuildDeliveryDocx(delivery, client, null)).Contains(client.LegalRepresentative), "Nuevas plantillas mantienen los datos congelados al firmar");
        document.SigningSnapshotJson = JsonSerializer.Serialize(new ClientSigningSnapshot(client, contract)
            { UsesCorporateServiceTemplate = true, ServiceTemplateRevision = 2 });
        delivery.SigningSnapshotJson = JsonSerializer.Serialize(new DeliverySigningSnapshot(client, null));
        check(!Text(templates.BuildClientLegalDocx(document, client, contract)).Contains("Registro electrónico"), "Snapshot eventual anterior sigue usando su plantilla original");
        check(!Text(templates.BuildDeliveryDocx(delivery, client, null)).Contains("Canal preferente"), "Snapshot de acta anterior sigue usando su plantilla original");
        document.SigningSnapshotJson = null;
        delivery.SigningSnapshotJson = null;
        document.PdfStoragePath = delivery.PdfStoragePath = "documento-preexistente.pdf";
        check(!Text(templates.BuildClientLegalDocx(document, client, contract)).Contains("Registro electrónico")
            && !Text(templates.BuildDeliveryDocx(delivery, client, null)).Contains("Canal preferente"), "PDFs anteriores sin snapshot no cambian de plantilla al agregar firma");

        client.Name = "Servicios Integrales de Telecomunicaciones e Infraestructura Empresarial del Centro de México, S.A. de C.V.";
        client.LegalRepresentative = document.SignedByName = delivery.SignedByName = "María Fernanda de los Ángeles Hernández Rodríguez y Fernández del Valle";
        client.FiscalAddress = "Avenida de los Insurgentes Sur número 1234, piso 14, edificio de oficinas corporativas y centro de operaciones, Ciudad de México";
        client.BillingEmail = document.SignedByEmail = delivery.SignedByEmail = "administracion.y.finanzas.corporativas@example.test";
        document.ProviderSignedByName = delivery.ProviderSignedByName = "José Alejandro de la Torre y Martínez de los Santos";
        document.SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract);
        delivery.SigningSnapshotJson = DeliverySigningSnapshot.Capture(client, null);
        delivery.ReceiverName = client.LegalRepresentative;
        var data = ProjectDeliveryPayload.Parse(delivery.ServiceSummary);
        data.NOMBRETECNICO = delivery.ProviderSignedByName;
        data.Services = Enumerable.Range(1, 5).Select(i => new DeliveryServiceRow
        {
            Servicio = $"Servicio {i}: enlace dedicado con instalación, configuración de equipos, validación de conectividad y puesta en operación para las oficinas corporativas del cliente.", Modalidad = "Mensual", Plazo = "24"
        }).ToList();
        data.Equipment = Enumerable.Range(1, 5).Select(i => new DeliveryEquipmentRow
        {
            Equipo = $"Equipo {i}: enrutador empresarial con accesorios, fuente de alimentación y elementos de montaje para instalación en gabinete de comunicaciones.", Cantidad = "1"
        }).ToList();
        delivery.ServiceSummary = ProjectDeliveryPayload.Serialize(data);
        File.WriteAllBytes(Path.Combine(output, "Evento-nombres-largos.docx"), templates.BuildClientLegalDocx(document, client, contract));
        File.WriteAllBytes(Path.Combine(output, "Entrega-contenido-largo.docx"), templates.BuildDeliveryDocx(delivery, client, null));
    }

    private static void CheckSignatureLayout(byte[] bytes, bool signed, string label, Action<bool, string> check)
    {
        var xml = Read(bytes);
        var row = xml.Descendants(W + "tr").Single(r => string.Concat(r.Descendants(W + "t").Select(x => x.Value)).Contains("Hash de firma:"));
        check(row.Elements(W + "tc").Count() == 2 && row.Element(W + "trPr")?.Element(W + "cantSplit") != null,
            label + ": ambas firmas permanecen juntas");
        check(row.Descendants(W + "spacing").Count(s => (string?)s.Attribute(W + "lineRule") == "atLeast" && (int?)s.Attribute(W + "line") >= 2000) == 2,
            label + ": ambas partes tienen espacio manuscrito amplio sin altura fija");
        check(!row.Descendants(W + "trHeight").Any() && !row.Descendants(W + "spacing").Any(s => (string?)s.Attribute(W + "lineRule") == "exact"),
            label + ": texto de firma flexible sin recortes");
        var text = Text(bytes);
        var hashes = Regex.Matches(text, @"Hash de firma: [A-F0-9]{16}");
        check(hashes.Count == (signed ? 2 : 0) && !Regex.IsMatch(text, @"SIGN(?:CLIENT|HN)(?:NAME|EMAIL|DATE|HASH|STATUS)"),
            label + ": hash únicamente cuando existe firma real y marcadores sustituidos");
    }

    private static string Text(byte[] bytes) => string.Concat(Read(bytes).Descendants(W + "t").Select(x => x.Value));
    private static XDocument Read(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        return XDocument.Load(stream);
    }
}
