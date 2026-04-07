using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            throw new FileNotFoundException($"No se encontrÃ³ la plantilla {fileName} en assets/legal-templates.");
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
        var tpl = ParseContractTemplateData(doc.TermsBody);
        var legalName = Safe(tpl.RSCLIENTE, client.Name);
        var legalAddress = Safe(tpl.DIRECCIONC, client.FiscalAddress, client.Address, "Domicilio por confirmar");
        var signer = Safe(tpl.RLCLIENTE, client.LegalRepresentative, client.ContactName, "Representante legal");
        var periodo = Safe(BuildContractPeriod(doc, doc.ClientServiceContract), tpl.PERIODOC, "12 meses");
        var firma = BuildDigitalSignatureText(doc, signer);

        return new Dictionary<string, string>
        {
            ["RSCLIENTE"] = legalName,
            ["RLCLIENTE"] = signer,
            ["RFCC"] = Safe(client.Rfc, "XAXX010101000"),
            ["FECHAHOY"] = cityDate,
            ["DIRECCIONC"] = legalAddress,
            ["ESTADOC"] = Safe(tpl.ESTADOC, client.State, "MÃ©xico"),
            ["CPC"] = Safe(client.FiscalZipCode, "00000"),
            ["EMAILC"] = Safe(client.BillingEmail, client.Email, "-"),
            ["CONTRATOC"] = Safe(tpl.CONTRATOC, doc.Title, "NDA"),
            ["PERIODOC"] = periodo,
            ["SUCURSALC"] = Safe(tpl.SUCURSALC, doc.ClientServiceContract?.Branch, "-"),
            ["COSTOCLIENTE"] = Safe(tpl.COSTOCLIENTE, (doc.MonthlyAmount ?? doc.ClientServiceContract?.MonthlyAmount ?? 0m).ToString("N2", new CultureInfo("es-MX"))),
            ["FIRMACLIENTE"] = firma,
            ["NOMBREPROYECTO"] = Safe(tpl.NOMBREPROYECTO, doc.ClientServiceContract?.Label, doc.Title, "-"),
            ["NOMBRETECNICO"] = Safe(tpl.NOMBRETECNICO, "-")
        };
    }

    private static Dictionary<string, string> BuildContractReplacements(ClientLegalDocument doc, Client client, ClientServiceContract? contract)
    {
        var tpl = ParseContractTemplateData(doc.TermsBody);
        var nowText = DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var period = BuildContractPeriod(doc, contract);
        var serviceName = Safe(tpl.CONTRATOC, contract?.Label, "Servicio de telecomunicaciones");
        var monthly = Safe(tpl.COSTOCLIENTE, (doc.MonthlyAmount ?? contract?.MonthlyAmount ?? 0m).ToString("N2", new CultureInfo("es-MX")));
        var firma = BuildDigitalSignatureText(doc, Safe(client.LegalRepresentative, client.ContactName));

        var replacements = new Dictionary<string, string>
        {
            ["RSCLIENTE"] = Safe(tpl.RSCLIENTE, client.Name),
            ["RLCLIENTE"] = Safe(tpl.RLCLIENTE, client.LegalRepresentative, client.ContactName, "-"),
            ["RFCC"] = Safe(client.Rfc, "XAXX010101000"),
            ["FECHAHOY"] = nowText,
            ["DIRECCIONC"] = Safe(tpl.DIRECCIONC, client.FiscalAddress, client.Address, "Por definir"),
            ["ESTADOC"] = Safe(tpl.ESTADOC, client.State, "MÃ©xico"),
            ["CPC"] = Safe(client.FiscalZipCode, "00000"),
            ["EMAILC"] = Safe(client.BillingEmail, client.Email, "por-confirmar@cliente.com"),
            ["CONTRATOC"] = Safe(tpl.CONTRATOC, doc.Title, serviceName),
            ["PERIODOC"] = Safe(period, tpl.PERIODOC, "12 meses"),
            ["SUCURSALC"] = Safe(tpl.SUCURSALC, contract?.Branch, contract?.Label, "-"),
            ["COSTOCLIENTE"] = monthly,
            ["FIRMACLIENTE"] = firma,
            ["NOMBREPROYECTO"] = Safe(tpl.NOMBREPROYECTO, contract?.Label, doc.Title, "-"),
            ["NOMBRETECNICO"] = Safe(tpl.NOMBRETECNICO, "-")
        };

        return replacements;
    }

    private static Dictionary<string, string> BuildDeliveryReplacements(ProjectDeliveryFormat delivery, Client client, Project? project)
    {
        var tpl = ParseDeliveryTemplateData(delivery.ServiceSummary, delivery.EquipmentSummary);
        var dt = delivery.DeliveryDate.ToString("dd 'de' MMMM yyyy", new CultureInfo("es-MX"));
        var r = new Dictionary<string, string>
        {
            ["RSCLIENTE"] = Safe(client.Name),
            ["FECHAHOY"] = dt,
            ["DIRECCIONC"] = Safe(client.FiscalAddress, client.Address, delivery.DeliveryLocation, "-"),
            ["NOMBREPROYECTO"] = Safe(project?.Title, tpl.NOMBREPROYECTO, delivery.Title, "-"),
            ["NOMBRETECNICO"] = Safe(tpl.NOMBRETECNICO, delivery.SignedByName, "TÃ©cnico asignado"),
            ["FIRMACLIENTE"] = delivery.Status == ProjectDeliveryFormatStatus.Signed
                ? Safe(delivery.SignedByName, delivery.ReceiverName, "-")
                : "PENDIENTE DE FIRMA",

            // Compatibilidad con documento previo.
            ["Naturasol S.A. de C.V."] = Safe(client.Name),
            ["Sistema BioTime PRO"] = Safe(project?.Title, tpl.NOMBREPROYECTO, "Servicio contratado"),
            ["Jorge Alberto Santana Torres"] = Safe(delivery.SignedByName, tpl.NOMBRETECNICO, "Responsable HN"),
            ["RamsÃ©s Estrada Gaona"] = Safe(delivery.ReceiverName, "Recibe cliente"),
            ["Ramses Estrada Gaona"] = Safe(delivery.ReceiverName, "Recibe cliente"),
            ["Oficinas Corporativas"] = Safe(delivery.DeliveryLocation),
            ["15 de julio 2025"] = dt,
            ["Cliente:"] = $"Cliente: {Safe(client.Name)}",
            ["Proyecto"] = $"Proyecto: {Safe(project?.Title, tpl.NOMBREPROYECTO, "Sin proyecto")}",
            ["Recibe"] = $"Recibe: {Safe(delivery.ReceiverName)}",
            ["TelÃ©fono:"] = $"TelÃ©fono: {Safe(delivery.ReceiverPhone)}",
            ["TelÃƒÂ©fono:"] = $"TelÃ©fono: {Safe(delivery.ReceiverPhone)}"
        };

        for (var i = 1; i <= 5; i++)
        {
            var idx = i - 1;
            var s = idx < tpl.Services.Count ? tpl.Services[idx] : new DeliveryServiceRow();
            var h = idx < tpl.Equipment.Count ? tpl.Equipment[idx] : new DeliveryEquipmentRow();
            r[$"SERVICIO{i:00}"] = Safe(s.Servicio, "");
            r[$"SMODAL{i:00}"] = Safe(s.Modalidad, "");
            r[$"SPLAZO{i:00}"] = Safe(s.Plazo, "");
            r[$"EQUIPO{i:00}"] = Safe(h.Equipo, "");
            r[$"ECANTIDAD{i:00}"] = Safe(h.Cantidad, "");
        }

        return r;
    }

    private static string BuildContractPeriod(ClientLegalDocument doc, ClientServiceContract? contract)
    {
        static int? EstimateMonths(DateTime? start, DateTime? end)
        {
            if (!start.HasValue || !end.HasValue)
                return null;

            var s = start.Value.Date;
            var e = end.Value.Date;
            if (e < s)
                (s, e) = (e, s);

            var months = (int)Math.Round((e - s).TotalDays / 30.4375, MidpointRounding.AwayFromZero);
            return Math.Max(1, months);
        }

        var months =
            EstimateMonths(doc.ContractStartDate, doc.ContractEndDate) ??
            EstimateMonths(contract?.ContractStartDate, contract?.ContractEndDate);

        return $"{(months ?? 12)} meses";
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

    
    private static string BuildDigitalSignatureText(ClientLegalDocument doc, string fallbackName)
    {
        if (doc.Status != ClientLegalDocumentStatus.Signed || !doc.SignedAt.HasValue)
            return "PENDIENTE DE FIRMA";

        var signer = Safe(doc.SignedByName, fallbackName);
        var email = Safe(doc.SignedByEmail, "-");
        var date = doc.SignedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        var payload = $"{doc.Id}|{signer}|{email}|{doc.SignedAt:O}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..16];
        return $"Firmado digitalmente por {signer} ({email}) el {date}. Hash: {hash}";
    }private static ContractTemplateData ParseContractTemplateData(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith("__TPLJSON__", StringComparison.Ordinal))
            return new ContractTemplateData();

        try
        {
            var json = raw["__TPLJSON__".Length..];
            return JsonSerializer.Deserialize<ContractTemplateData>(json) ?? new ContractTemplateData();
        }
        catch
        {
            return new ContractTemplateData();
        }
    }

    private static DeliveryTemplateData ParseDeliveryTemplateData(string? serviceSummary, string? equipmentSummary)
    {
        if (!string.IsNullOrWhiteSpace(serviceSummary) && serviceSummary.StartsWith("__DELIVERYJSON__", StringComparison.Ordinal))
        {
            try
            {
                var json = serviceSummary["__DELIVERYJSON__".Length..];
                var parsed = JsonSerializer.Deserialize<DeliveryTemplateData>(json);
                if (parsed != null)
                    return parsed;
            }
            catch
            {
            }
        }

        return new DeliveryTemplateData();
    }

    private sealed class ContractTemplateData
    {
        public string? RSCLIENTE { get; set; }
        public string? RLCLIENTE { get; set; }
        public string? DIRECCIONC { get; set; }
        public string? ESTADOC { get; set; }
        public string? CONTRATOC { get; set; }
        public string? PERIODOC { get; set; }
        public string? SUCURSALC { get; set; }
        public string? COSTOCLIENTE { get; set; }
        public string? FIRMACLIENTE { get; set; }
        public string? NOMBREPROYECTO { get; set; }
        public string? NOMBRETECNICO { get; set; }
    }

    private sealed class DeliveryTemplateData
    {
        public string? NOMBREPROYECTO { get; set; }
        public string? NOMBRETECNICO { get; set; }
        public List<DeliveryServiceRow> Services { get; set; } = [];
        public List<DeliveryEquipmentRow> Equipment { get; set; } = [];
    }

    private sealed class DeliveryServiceRow
    {
        public string? Servicio { get; set; }
        public string? Modalidad { get; set; }
        public string? Plazo { get; set; }
    }

    private sealed class DeliveryEquipmentRow
    {
        public string? Equipo { get; set; }
        public string? Cantidad { get; set; }
    }
}
