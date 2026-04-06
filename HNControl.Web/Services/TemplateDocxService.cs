using System.Globalization;
using System.IO.Compression;
using System.Text;
using HNControl.Web.Models;

namespace HNControl.Web.Services;

public class TemplateDocxService : ITemplateDocxService
{
    private readonly IWebHostEnvironment _env;

    public TemplateDocxService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public byte[] BuildClientLegalDocx(ClientLegalDocument document, Client client, ClientServiceContract? contract)
    {
        var templateName = document.DocumentType == ClientLegalDocumentType.NDA
            ? "NDA.docx"
            : "Contrato_Blanco.docx";

        var bytes = LoadTemplateBytes(templateName);
        var replacements = document.DocumentType == ClientLegalDocumentType.NDA
            ? BuildNdaReplacements(document, client)
            : BuildContractReplacements(document, client, contract);

        return ReplaceDocumentXml(bytes, replacements);
    }

    public byte[] BuildDeliveryDocx(ProjectDeliveryFormat delivery, Client client, Project? project)
    {
        var bytes = LoadTemplateBytes("Acta_Entrega_Material_Servicios.docx");
        var replacements = BuildDeliveryReplacements(delivery, client, project);
        return ReplaceDocumentXml(bytes, replacements);
    }

    private byte[] LoadTemplateBytes(string fileName)
    {
        var path = Path.Combine(_env.ContentRootPath, "assets", "legal-templates", fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"No se encontró la plantilla {fileName} en assets/legal-templates.");
        return File.ReadAllBytes(path);
    }

    private static byte[] ReplaceDocumentXml(byte[] docxBytes, IReadOnlyDictionary<string, string> replacements)
    {
        using var input = new MemoryStream(docxBytes);
        using var output = new MemoryStream();

        using (var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var outArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in archive.Entries)
            {
                var outEntry = outArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var inStream = entry.Open();
                using var outStream = outEntry.Open();

                if (string.Equals(entry.FullName, "word/document.xml", StringComparison.OrdinalIgnoreCase))
                {
                    using var sr = new StreamReader(inStream, Encoding.UTF8, true);
                    var xml = sr.ReadToEnd();
                    foreach (var kv in replacements)
                    {
                        if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                        xml = xml.Replace(kv.Key, kv.Value, StringComparison.Ordinal);
                    }
                    using var sw = new StreamWriter(outStream, new UTF8Encoding(false));
                    sw.Write(xml);
                }
                else
                {
                    inStream.CopyTo(outStream);
                }
            }
        }

        return output.ToArray();
    }

    private static Dictionary<string, string> BuildNdaReplacements(ClientLegalDocument doc, Client client)
    {
        var cityDate = DateTime.Now.ToString("dd 'de' MMMM yyyy", new CultureInfo("es-MX"));
        var legalName = Safe(client.Name);
        var legalAddress = Safe(client.FiscalAddress, client.Address, "Domicilio por confirmar");
        var signer = Safe(client.LegalRepresentative, client.ContactName, "Representante legal");

        return new Dictionary<string, string>
        {
            ["CLIENTE"] = legalName,
            ["con domicilio en"] = $"con domicilio en {legalAddress}",
            ["con fecha"] = $"con fecha {cityDate}",
            ["Jorge Alberto Santana Torres"] = signer,
            ["HUBNET INFRAESTRUCTURE TECHNOLOGY SOLUTIONS"] = "HUBNET INFRAESTRUCTURE TECHNOLOGY SOLUTIONS"
        };
    }

    private static Dictionary<string, string> BuildContractReplacements(ClientLegalDocument doc, Client client, ClientServiceContract? contract)
    {
        var nowText = DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var period = BuildContractPeriod(doc, contract);
        var serviceName = Safe(contract?.Label, "Servicio de telecomunicaciones");
        var monthly = (doc.MonthlyAmount ?? contract?.MonthlyAmount ?? 0m).ToString("N2", new CultureInfo("es-MX"));

        var replacements = new Dictionary<string, string>
        {
            ["Nombre o Razón Social:"] = $"Nombre o Razón Social: {Safe(client.Name)}",
            ["Nombre o RazÃ³n Social:"] = $"Nombre o Razón Social: {Safe(client.Name)}",
            ["Fecha Contrato:"] = $"Fecha Contrato: {nowText}",
            ["RFC:"] = $"RFC: {Safe(client.Rfc, "XAXX010101000")}",
            ["Nombre Comercial:"] = $"Nombre Comercial: {Safe(client.Name)}",
            ["Correo Electrónico:"] = $"Correo Electrónico: {Safe(client.Email, client.LegalEmail, "por-confirmar@cliente.com")}",
            ["Correo ElectrÃ³nico:"] = $"Correo Electrónico: {Safe(client.Email, client.LegalEmail, "por-confirmar@cliente.com")}",
            ["Calle o Avenida:"] = $"Calle o Avenida: {Safe(client.Address, client.FiscalAddress, "Por definir")}",
            ["Ciudad:"] = $"Ciudad: {Safe(client.Address, "Ciudad de México")}",
            ["Estado:"] = "Estado: México",
            ["Código Postal:"] = $"Código Postal: {Safe(client.FiscalZipCode, "00000")}",
            ["CÃ³digo Postal:"] = $"Código Postal: {Safe(client.FiscalZipCode, "00000")}",
            ["Servici"] = "Servicio(s) Contratados: " + serviceName,
            ["Periodo de Contratación:"] = $"Periodo de Contratación: {period}",
            ["Periodo de ContrataciÃ³n:"] = $"Periodo de Contratación: {period}",
            ["$"] = "$" + monthly,
            ["Ubicación del Servicio"] = "Ubicación del Servicio: " + Safe(contract?.BranchAddress, client.Address, "Por definir"),
            ["UbicaciÃ³n del Servicio"] = "Ubicación del Servicio: " + Safe(contract?.BranchAddress, client.Address, "Por definir")
        };

        return replacements;
    }

    private static Dictionary<string, string> BuildDeliveryReplacements(ProjectDeliveryFormat delivery, Client client, Project? project)
    {
        var dt = delivery.DeliveryDate.ToString("dd 'de' MMMM yyyy", new CultureInfo("es-MX"));
        return new Dictionary<string, string>
        {
            ["Naturasol S.A. de C.V."] = Safe(client.Name),
            ["Sistema BioTime PRO"] = Safe(project?.Title, "Servicio contratado"),
            ["Jorge Alberto Santana Torres"] = Safe(delivery.SignedByName, "Responsable HN"),
            ["Ramsés Estrada Gaona"] = Safe(delivery.ReceiverName, "Recibe cliente"),
            ["Ramses Estrada Gaona"] = Safe(delivery.ReceiverName, "Recibe cliente"),
            ["Oficinas Corporativas"] = Safe(delivery.DeliveryLocation),
            ["15 de julio 2025"] = dt,
            ["Cliente:"] = $"Cliente: {Safe(client.Name)}",
            ["Proyecto"] = $"Proyecto: {Safe(project?.Title, "Sin proyecto")}",
            ["Recibe"] = $"Recibe: {Safe(delivery.ReceiverName)}",
            ["Teléfono:"] = $"Teléfono: {Safe(delivery.ReceiverPhone)}",
            ["TelÃ©fono:"] = $"Teléfono: {Safe(delivery.ReceiverPhone)}"
        };
    }

    private static string BuildContractPeriod(ClientLegalDocument doc, ClientServiceContract? contract)
    {
        if (doc.ContractStartDate.HasValue || doc.ContractEndDate.HasValue)
        {
            var start = doc.ContractStartDate?.ToString("dd/MM/yyyy") ?? "-";
            var end = doc.ContractEndDate?.ToString("dd/MM/yyyy") ?? "-";
            return $"{start} al {end}";
        }

        if (contract?.ContractStartDate.HasValue == true || contract?.ContractEndDate.HasValue == true)
        {
            var start = contract.ContractStartDate?.ToString("dd/MM/yyyy") ?? "-";
            var end = contract.ContractEndDate?.ToString("dd/MM/yyyy") ?? "-";
            return $"{start} al {end}";
        }

        return "12 meses";
    }

    private static string Safe(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return "-";
    }
}
