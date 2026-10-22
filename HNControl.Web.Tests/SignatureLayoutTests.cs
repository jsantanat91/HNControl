using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HNControl.Web.Models;
using HNControl.Web.Services;

internal static class SignatureLayoutTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static void Run(ITemplateDocxService templates, Action<bool, string> check)
    {
        var client = new Client
        {
            Name = "HUBNET INFRAESTRUCTURE TECHNOLOGY SOLUTIONS",
            LegalRepresentative = "Jorge Alberto Santana Torres",
            BillingEmail = "facturacion@example.test",
            FiscalAddress = "Domicilio de prueba, Tecámac, Estado de México"
        };
        var time = new DateTime(2026, 10, 22, 21, 21, 0, DateTimeKind.Utc);
        var nda = new ClientLegalDocument { Id = Guid.NewGuid(), DocumentType = ClientLegalDocumentType.NDA };
        var delivery = new ProjectDeliveryFormat
        {
            Id = Guid.NewGuid(), Title = "Entrega de Internet dedicado", DeliveryDate = time,
            ServiceSummary = """__DELIVERYJSON__{"Services":[{"Servicio":"Internet dedicado 10 MB","Modalidad":"Mensual","Plazo":"24"}]}"""
        };
        Directory.CreateDirectory("artifacts/signature-layout");
        var pendingNda = templates.BuildClientLegalDocx(nda, client, null);
        var pendingDelivery = templates.BuildDeliveryDocx(delivery, client, null);
        Validate(pendingNda, false, "NDA pendiente", check);
        Validate(pendingDelivery, false, "Acta pendiente", check);
        check(Text(Read(pendingDelivery)).Contains("Cliente: Jorge Alberto Santana Torres"),
            "Acta conserva al representante aunque su nombre coincida con el ejemplo de la plantilla antigua");
        File.WriteAllBytes("artifacts/signature-layout/nda-pendiente.docx", pendingNda);
        File.WriteAllBytes("artifacts/signature-layout/acta-pendiente.docx", pendingDelivery);

        nda.SignedByName = delivery.SignedByName = "Jorge Alberto Santana Torres";
        nda.SignedByEmail = delivery.SignedByEmail = "jorge.santana@example.test";
        nda.SignedAt = delivery.SignedAt = time;
        nda.ProviderSignedByName = delivery.ProviderSignedByName = "Jorge Alberto Santana Torres";
        nda.ProviderSignedByEmail = delivery.ProviderSignedByEmail = "jsantana@example.test";
        nda.ProviderSignedByUserId = delivery.ProviderSignedByUserId = "admin-test";
        nda.ProviderSignedAt = delivery.ProviderSignedAt = time.AddMinutes(1);
        var signedNda = templates.BuildClientLegalDocx(nda, client, null);
        var signedDelivery = templates.BuildDeliveryDocx(delivery, client, null);
        Validate(signedNda, true, "NDA firmado", check);
        Validate(signedDelivery, true, "Acta firmada", check);
        File.WriteAllBytes("artifacts/signature-layout/nda-firmado.docx", signedNda);
        File.WriteAllBytes("artifacts/signature-layout/acta-firmada.docx", signedDelivery);
        var originalHashes = Hashes(signedDelivery);
        check(originalHashes.SequenceEqual(Hashes(templates.BuildDeliveryDocx(delivery, client, null))), "Hash del acta estable al regenerar el mismo contenido de firma");
        delivery.ProviderSignedByEmail = "otra-persona@example.test";
        var changedHashes = Hashes(templates.BuildDeliveryDocx(delivery, client, null));
        check(originalHashes[0] != changedHashes[0] && originalHashes[1] == changedHashes[1], "Cambiar firmante HN cambia solo su hash de acta");
        delivery.Id = Guid.NewGuid();
        check(!changedHashes.Intersect(Hashes(templates.BuildDeliveryDocx(delivery, client, null))).Any(), "Hash del acta depende del documento firmado");
        nda.ProviderSignedAt = null;
        var partial = templates.BuildClientLegalDocx(nda, client, null);
        check(Hashes(partial).Length == 1 && Text(Read(partial)).Contains("PENDIENTE DE FIRMA"), "NDA con una firma solo muestra el hash de quien firmó");

        nda.ProviderSignedAt = time.AddMinutes(1);
        client.Name = "Servicios Integrales de Telecomunicaciones e Infraestructura Empresarial del Centro de México, S.A. de C.V.";
        client.LegalRepresentative = "María Fernanda de los Ángeles Hernández Rodríguez y Fernández del Valle";
        nda.SignedByName = delivery.SignedByName = client.LegalRepresentative;
        nda.SignedByEmail = delivery.SignedByEmail = "administracion.y.finanzas.corporativas@example.test";
        nda.ProviderSignedByName = delivery.ProviderSignedByName = "José Alejandro de la Torre y Martínez de los Santos";
        File.WriteAllBytes("artifacts/signature-layout/nda-nombres-largos.docx", templates.BuildClientLegalDocx(nda, client, null));
        File.WriteAllBytes("artifacts/signature-layout/acta-nombres-largos.docx", templates.BuildDeliveryDocx(delivery, client, null));
        delivery.ServiceSummary = "__DELIVERYJSON__" + System.Text.Json.JsonSerializer.Serialize(new
        {
            Services = Enumerable.Range(1, 5).Select(i => new
            {
                Servicio = $"Servicio {i}: enlace dedicado con monitoreo, instalación, configuración de equipos, validación de conectividad y puesta en operación para las oficinas corporativas del cliente.",
                Modalidad = "Mensual", Plazo = "24"
            }),
            Equipment = Enumerable.Range(1, 5).Select(i => new
            {
                Equipo = $"Equipo {i}: enrutador empresarial con accesorios, fuente de alimentación y elementos de montaje para instalación en gabinete de comunicaciones.",
                Cantidad = "1"
            })
        });
        File.WriteAllBytes("artifacts/signature-layout/acta-servicios-largos.docx", templates.BuildDeliveryDocx(delivery, client, null));
    }

    private static void Validate(byte[] bytes, bool signed, string label, Action<bool, string> check)
    {
        var xml = Read(bytes);
        var row = xml.Descendants(W + "tr").Single(r => Text(r).Contains("Hash de firma:"));
        check(row.Elements(W + "tc").Count() == 2
            && row.Element(W + "trPr")?.Element(W + "cantSplit") != null,
            label + ": ambas firmas en una fila que no se divide entre páginas");
        check(!row.Descendants(W + "trHeight").Any() && !row.Descendants(W + "framePr").Any()
            && !row.Descendants(W + "spacing").Any(x => (string?)x.Attribute(W + "lineRule") == "exact"),
            label + ": sin alturas fijas que recorten nombres o hashes");
        check(!Regex.IsMatch(Text(xml), @"SIGN(?:CLIENT|HN)(?:NAME|EMAIL|DATE|HASH|STATUS)"), label + ": todos los campos de firma reemplazados");
        var hashes = Hashes(bytes);
        check(signed ? hashes.Length == 2 && hashes.Distinct().Count() == 2 : hashes.Length == 0,
            label + ": hash visible e independiente por firmante, solo después de firmar");
    }

    private static string[] Hashes(byte[] bytes) => Regex.Matches(Text(Read(bytes)), @"Hash de firma: ([A-F0-9]{16})")
        .Select(m => m.Groups[1].Value).ToArray();
    private static string Text(XContainer xml) => string.Concat(xml.Descendants(W + "t").Select(x => x.Value));
    private static XDocument Read(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        return XDocument.Load(stream);
    }
}
