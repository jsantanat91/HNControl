using System.IO.Compression;
using System.Xml.Linq;
using HNControl.Web.Models;
using HNControl.Web.Services;

internal static class ContractAutographTests
{
    public static void Run(ITemplateDocxService templates, Action<bool, string> check)
    {
        var image = File.ReadAllBytes("HNControl.Web.Tests/Fixtures/contract-signature.png");
        var client = new Client { Name = "Cliente de demostración", LegalRepresentative = "Firma de prueba Cliente", LegalEmail = "cliente@example.test" };
        foreach (var eventual in new[] { false, true })
        {
            var contract = new ClientServiceContract { Label = "Internet Dedicado 20 MB", MonthlyAmount = 4500, Notes = eventual ? "[META] Recurrencia=Eventual" : "", ContractStartDate = new DateTime(2026,11,9), ContractEndDate = new DateTime(2026,11,10) };
            var doc = new ClientLegalDocument { Client = client, ClientServiceContract = contract, SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract),
                SignedAt = DateTime.UtcNow, SignedByName = "Firma de prueba Cliente", SignedByEmail = "cliente@example.test",
                ProviderSignedAt = DateTime.UtcNow, ProviderSignedByName = "Firma de prueba HN", ProviderSignedByEmail = "hn@example.test",
                ClientSignatureImage = image, ProviderSignatureImage = image };
            var word = templates.BuildClientLegalDocx(doc, client, contract);
            using var zip = new ZipArchive(new MemoryStream(word));
            using var stream = zip.GetEntry("word/document.xml")!.Open();
            var xml = XDocument.Load(stream);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            check(zip.GetEntry("word/media/autograph-client.png") != null && zip.GetEntry("word/media/autograph-hn.png") != null,
                $"Contrato {(eventual ? "eventual" : "mensual")} incorpora ambas imágenes autógrafas");
            check(xml.Descendants(w + "drawing").Count() >= 2 && xml.ToString().Contains("Hash"), "Firma autógrafa y registro digital coexisten");
            Directory.CreateDirectory("artifacts/mobile-services");
            File.WriteAllBytes($"artifacts/mobile-services/contrato-{(eventual ? "evento" : "mensual")}-firmado.docx", word);
        }
    }
}
