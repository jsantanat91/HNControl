using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using HNControl.Web.Models;
using HNControl.Web.Services;

internal static class ServiceSlaTests
{
    public static void Run(TemplateDocxService templates, Action<bool, string> check)
    {
        var client = new Client { Name = "Cliente SLA de prueba", Email = "cliente@example.test" };
        var contract = new ClientServiceContract { ServiceType = ClientServiceType.Internet,
            Label = "Internet Dedicado 20 MB", Notes = "[META] Plazo=12", MonthlyAmount = 4500 };
        var doc = new ClientLegalDocument { DocumentType = ClientLegalDocumentType.Contract,
            SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract) };
        var snapshot = JsonSerializer.Deserialize<ClientSigningSnapshot>(doc.SigningSnapshotJson)!;
        check(snapshot.ServiceTemplateRevision == 4 && snapshot.EventTemplateRevision == 2,
            "Nuevos contratos recurrentes congelan anexo v4; los eventuales mantienen v2");

        string WordText()
        {
            using var zip = new ZipArchive(new MemoryStream(templates.BuildClientLegalDocx(doc, client, contract)));
            using var stream = zip.GetEntry("word/document.xml")!.Open();
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            return string.Join("\n", XDocument.Load(stream).Descendants(w + "p")
                .Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value))));
        }
        string Welcome(params ClientLegalDocument[] docs) => ClientCommunicationTemplates.Welcome(client,
            docs.Select(d => (d, "https://example.test/firmar")));

        var text = WordText();
        check(text.Contains("México") && !text.Contains("MÃ©xico"),
            "Nuevo contrato conserva acentos correctos al usar el estado predeterminado");
        check(text.Contains("disponibilidad mínima mensual del 99.85%")
            && text.Contains("punto de entrega al cliente") && text.Contains("64 minutos y 48 segundos"),
            "SLA dedicado mensual 99.85 con medición y ejemplo coherentes");
        check(text.Contains("Máximo contractual: 4 horas transcurridas, 24/7") && text.Contains("Objetivo: 15 minutos")
            && text.Contains("Cada hora") && text.Contains("Hasta 4 horas transcurridas")
            && text.Contains("no es un plazo máximo garantizado"),
            "Dedicado distingue máximo de respuesta humana y objetivo de recuperación");
        check(text.Contains("No tiene un porcentaje de disponibilidad garantizado")
            && text.Contains("Entre 24 y 48 horas transcurridas") && !text.Contains("89.5%"),
            "Best Effort tiene sus propios objetivos sin promesa genérica de disponibilidad");
        check(text.Contains("sin exigir una interrupción mínima de 72 horas")
            && text.Contains("20% de la facturación mensual")
            && !text.Contains("en un tiempo que no exceda 72 horas"),
            "Elimina espera de 72 horas y conserva crédito contractual mínimo con derechos legales");
        check(text.Contains("WhatsApp 5526014566") && text.Contains("soporte@hubnet-solutions.com.mx")
            && text.Contains("https://support.hubnet-solutions.net")
            && !text.Contains("soporte@hubnet-solutions.net") && !text.Contains("hubnet-olutions"),
            "Carátula y SLA usan los canales de soporte confirmados sin correos anteriores");
        check(text.Contains("lo que ocurra antes") && text.Contains("no reiniciará el cómputo")
            && text.Contains("no constituyen por sí solas una exclusión"),
            "Cómputo desde detección o reporte y exclusiones justificadas sin exención genérica del carrier");

        var currentWelcome = Welcome(doc);
        check(currentWelcome.Contains("99.85%") && currentWelcome.Contains("Best Effort")
            && currentWelcome.Contains("objetivo operativo") && currentWelcome.Contains("canal preferente"),
            "Bienvenida nueva resume SLA por modalidad y facilita abrir ticket");
        Directory.CreateDirectory("artifacts/sla-contract");
        File.WriteAllText("artifacts/sla-contract/bienvenida.html", currentWelcome);
        File.WriteAllBytes("artifacts/sla-contract/contrato-pendiente.docx", templates.BuildClientLegalDocx(doc, client, contract));

        doc.SigningSnapshotJson = JsonSerializer.Serialize(snapshot with { ServiceTemplateRevision = 2 });
        var historic = WordText();
        check(historic.Contains("en un tiempo que no exceda 72 horas")
            && historic.Contains("72 (setenta y dos) horas consecutivas") && !historic.Contains("64 minutos y 48 segundos"),
            "Reemitir un contrato v2 conserva íntegramente sus condiciones originales");
        check(!Welcome(doc).Contains("99.85%") && !Welcome(doc).Contains("15 minutos"),
            "Reenviar bienvenida histórica no promete el SLA de una nueva plantilla");
        var current = new ClientLegalDocument { SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract) };
        check(!Welcome(doc, current).Contains("99.85%"),
            "Bienvenida con contratos de distintas revisiones remite a sus propias condiciones");
        doc.SigningSnapshotJson = null;
        doc.Status = ClientLegalDocumentStatus.SentForSignature;
        doc.PdfStoragePath = "historico.pdf";
        check(WordText().Contains("en un tiempo que no exceda 72 horas"),
            "Documento emitido sin snapshot conserva la plantilla histórica");
        doc.Status = ClientLegalDocumentStatus.Draft;
        doc.PdfStoragePath = null;
        check(WordText().Contains("disponibilidad mínima mensual del 99.85%"),
            "Nuevo borrador sin snapshot usa SLA actual antes de congelarse");

        contract.Notes = "[META] Recurrencia=Eventual";
        contract.ContractStartDate = new DateTime(2026, 11, 19);
        contract.ContractEndDate = new DateTime(2026, 11, 20);
        doc.SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract);
        check(!WordText().Contains("99.85%") && !Welcome(doc).Contains("99.85%"),
            "Un contrato eventual no recibe SLA mensual aunque su snapshot contenga la revisión recurrente");
    }
}
