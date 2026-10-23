using System.IO.Compression;
using System.Xml.Linq;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

var root = Path.GetFullPath(args.FirstOrDefault() ?? "HNControl.Web");
var service = new TemplateDocxService(new TestEnvironment(root));
var client = new Client { Name = "Cliente de prueba", LegalRepresentative = "Representante de prueba" };
var doc = new ClientLegalDocument
{
    DocumentType = ClientLegalDocumentType.Contract,
    TermsBody = """__TPLJSON__{"CONTRATOC":"Nombre anterior","PERIODOC":"12 meses"}"""
};
var count = 0;
var ns = XNamespace.Get("http://schemas.openxmlformats.org/wordprocessingml/2006/main");
XDocument Read(byte[] bytes)
{
    using var zip = new ZipArchive(new MemoryStream(bytes));
    using var stream = zip.GetEntry("word/document.xml")!.Open();
    return XDocument.Load(stream);
}
string Cell(XDocument xml, string label) =>
    string.Concat(xml.Descendants(ns + "tr")
        .First(r => string.Concat(r.Descendants(ns + "t").Select(t => t.Value)).StartsWith(label))
        .Elements(ns + "tc").Last().Descendants(ns + "t").Select(t => t.Value)).Trim();
void Check(bool condition, string description)
{
    if (!condition) throw new Exception("FAIL: " + description);
    count++;
    Console.WriteLine("PASS: " + description);
}
ClientServiceContract Contract(string label, string notes, ClientServiceType type = ClientServiceType.Internet) =>
    new() { Label = label, Notes = notes + "\n[META] Plazo=24", ServiceType = type };
var cases = new[]
{
    (Contract("Internet Dedicado", "[META] InternetCapacidad=10"), "Internet Dedicado 10 MB"),
    (Contract("Internet Empresarial", "[META] InternetCapacidad=100"), "Internet Empresarial 100 MB"),
    (Contract("Internet Dedicado", ""), "Internet Dedicado"),
    (Contract("Telefonía", "[META] InternetCapacidad=10", ClientServiceType.Telefonia), "Telefonía"),
    (Contract("Internet Dedicado", "[META] InternetCapacidad=Otro\n[META] InternetCapacidadOtro=2 Gbps"), "Internet Dedicado 2 Gbps"),
    (Contract("Internet Dedicado", "[META] InternetCapacidad=Otro"), "Internet Dedicado"),
    (Contract("Internet Dedicado 10 MB", "[META] InternetCapacidad=10"), "Internet Dedicado 10 MB"),
    (Contract("Internet Dedicado 100 Mbps", "[META] InternetCapacidad=20"), "Internet Dedicado 20 MB"),
    (Contract("Internet y telefonía", "[META] TiposServicio=Telefonia|Internet\n[META] InternetCapacidad=50", ClientServiceType.Telefonia), "Internet y telefonía 50 MB"),
    (Contract("Internet & datos", "[META] InternetCapacidad=Otro\n[META] InternetCapacidadOtro=2 Gbps & respaldo"), "Internet & datos 2 Gbps & respaldo")
};
foreach (var (contract, expected) in cases)
{
    var xml = Read(service.BuildClientLegalDocx(doc, client, contract));
    Check(Cell(xml, "Servicios Contratados:") == expected, expected);
    Check(Cell(xml, "Periodo de Contratación:") == "24 meses (Cancelación Anticipada: 30% de rentas devengadas.)", "Plazo y leyenda completos");
}
var noContract = Read(service.BuildClientLegalDocx(doc, client, null));
Check(Cell(noContract, "Servicios Contratados:") == "Nombre anterior", "Documento sin contrato conserva su servicio");
var indefinite = Contract("Internet Dedicado", "[META] InternetCapacidad=10");
indefinite.Notes = "[META] InternetCapacidad=10\n[META] Plazo=Indefinido";
Check(Cell(Read(service.BuildClientLegalDocx(doc, client, indefinite)), "Periodo de Contratación:")
    == "Indefinido (Cancelación Anticipada: 30% de rentas devengadas.)", "Plazo indefinido preservado");
doc.DocumentType = ClientLegalDocumentType.NDA;
var nda = Read(service.BuildClientLegalDocx(doc, client, cases[0].Item1));
Check(!nda.ToString().Contains("Cancelación Anticipada"), "NDA sin cláusula de cancelación");
doc.DocumentType = ClientLegalDocumentType.Contract;
if (args.Length > 1)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
    File.WriteAllBytes(args[1], service.BuildClientLegalDocx(doc, client, cases[0].Item1));
}
await ClientCommunicationTests.RunAsync(service, Check);
await DeliveryCommunicationTests.RunAsync(service, Check);
await WelcomeContractSelectionTests.RunAsync(Check);
await SystemHealthTests.RunAsync(root, Check);
await ModuleSuspensionTests.RunAsync(Check);
await CarrierDeletionTests.RunAsync(Check);
await EngineeringNotificationSettingsTests.RunAsync(Check);
await ServiceOrderCreationNotificationTests.RunAsync(Check);
await ServiceOrderCreationPathsTests.RunAsync(Check);
SignatureLayoutTests.Run(service, Check);
Console.WriteLine($"All {count} checks passed.");

sealed class TestEnvironment(string root) : IWebHostEnvironment
{
    public string ContentRootPath { get; set; } = root;
    public string WebRootPath { get; set; } = root;
    public string EnvironmentName { get; set; } = "Testing";
    public string ApplicationName { get; set; } = "ContractTests";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
