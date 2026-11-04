using HNControl.Web.Models;
using HNControl.Web.Services;
using System.IO.Compression;
using System.Xml.Linq;

internal static class EventContractTests
{
    public static void Run(ITemplateDocxService templates, Action<bool, string> check)
    {
        var start = new DateTime(2026, 11, 4);
        check(EventContract.Validate(start, start, 100) == null, "Evento de un día admitido");
        check(EventContract.Validate(start, start.AddDays(1), 100) == null, "Evento de dos días admitido");
        check(EventContract.Validate(start, new DateTime(2026, 12, 3), 100) == null, "Un mes inclusivo admitido");
        check(EventContract.Validate(start, new DateTime(2026, 12, 4), 100) != null, "Más de un mes rechazado");
        check(EventContract.Validate(start, start.AddDays(-1), 100) != null, "Fechas invertidas rechazadas");
        check(EventContract.Validate(null, start, 100) != null && EventContract.Validate(start, null, 100) != null, "Fechas requeridas");
        check(EventContract.Validate(start, start, null) != null && EventContract.Validate(start, start, 0) != null, "Importe único requerido");
        check(EventContract.Validate(new DateTime(2027, 1, 31), new DateTime(2027, 2, 27), 100) == null, "Mes corto validado");
        var client = new Client { Name = "Evento <Prueba>", LegalRepresentative = "Representante", ClientCode = "HN-0042" };
        var contract = new ClientServiceContract { Label = "WiFi para congreso", Notes = "Cobertura: salón principal\n[META] Recurrencia=Eventual",
            ContractNumber = "HN-0042-01", BranchAddress = "Sede del evento", ContractStartDate = start, ContractEndDate = start, MonthlyAmount = 5000 };
        var doc = new ClientLegalDocument { DocumentType = ClientLegalDocumentType.Contract, ClientServiceContract = contract,
            SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract) };
        check(EventContract.FrozenContract(doc)!.BranchAddress == "Sede del evento", "Snapshot conserva sede y modalidad");
        var html = ClientCommunicationTemplates.EventWelcome(client, contract, new[] { (doc, "https://example.test/firmar") });
        check(html.Contains("Evento &lt;Prueba&gt;") && html.Contains("HN-0042") && html.Contains("04/11/2026"), "Bienvenida del evento codifica datos y muestra fechas");
        check(!html.Contains("primeros cinco días") && !html.Contains("SLA del 99.85%"), "Bienvenida eventual sin condiciones mensuales heredadas");
        contract.Notes = "[META] Recurrencia=Mensual"; // Current contract can change; signed inputs must not.
        var bytes = templates.BuildClientLegalDocx(doc, client, contract);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        var xml = XDocument.Load(stream);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var text = string.Concat(xml.Descendants(w + "t").Select(x => x.Value));
        check(text.Contains("Contrato de conectividad temporal para eventos") && text.Contains("04/11/2026"), "DOCX usa modalidad y periodo congelados");
        check(text.Contains("Sede del evento") && text.Contains("HN-0042-01") && text.Contains("5,000.00"), "DOCX rellena sede número e importe único");
        check(!text.Contains("30%") && !text.Contains("12 meses"), "DOCX eventual no hereda penalización ni plazo recurrente");
        check(!text.Contains("SIGNCLIENT") && !text.Contains("SIGNHN") && !text.Contains("EVENTOALCANCE"), "Campos del DOCX y ambas firmas reemplazados");
        Directory.CreateDirectory("artifacts/event-contract");
        File.WriteAllBytes("artifacts/event-contract/contrato-eventual-prueba.docx", bytes);
        File.WriteAllText("artifacts/event-contract/bienvenida.html", html);
    }
}
