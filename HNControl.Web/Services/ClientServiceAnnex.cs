using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HNControl.Web.Models;

namespace HNControl.Web.Services;

public sealed class ClientServiceAnnexData
{
    [RegularExpression("Dedicado|BestEffort", ErrorMessage = "Seleccione una modalidad válida.")]
    public string? Modality { get; set; }
    [RegularExpression("Reventa|RedPropia|Mixta", ErrorMessage = "Seleccione una forma de prestación válida.")]
    public string? NetworkMode { get; set; }
    [MaxLength(70)] public string? AuthorizationReference { get; set; }
    [MaxLength(100)] public string? AuthorizationHolder { get; set; }
    [MaxLength(60)] public string? ProfecoRegistration { get; set; }
    [MaxLength(30)] public string? UploadCapacity { get; set; }
    [RegularExpression("Simetrico|Asimetrico", ErrorMessage = "Seleccione la simetría del enlace.")]
    public string? Symmetry { get; set; }
    [MaxLength(40)] public string? AccessMedium { get; set; }
    [MaxLength(120)] public string? DeliveryPoint { get; set; }
    [DataType(DataType.Date)] public DateTime? InstallationDate { get; set; }
    [MaxLength(100)] public string? PublicIpConditions { get; set; }
    [MaxLength(60)] public string? Latency { get; set; }
    [MaxLength(60)] public string? Jitter { get; set; }
    [MaxLength(60)] public string? PacketLoss { get; set; }
    [MaxLength(120)] public string? MeasurementScope { get; set; }
    [MaxLength(100)] public string? EscalationContact { get; set; }
}

/// <summary>Technical annex fields are kept in existing contract metadata and frozen with Notes.</summary>
public static class ClientServiceAnnex
{
    private static readonly (string Key, string Property)[] Fields =
    [
        ("AnexoModalidad", nameof(ClientServiceAnnexData.Modality)),
        ("AnexoRed", nameof(ClientServiceAnnexData.NetworkMode)),
        ("AnexoTitulo", nameof(ClientServiceAnnexData.AuthorizationReference)),
        ("AnexoTitular", nameof(ClientServiceAnnexData.AuthorizationHolder)),
        ("AnexoProfeco", nameof(ClientServiceAnnexData.ProfecoRegistration)),
        ("AnexoSubida", nameof(ClientServiceAnnexData.UploadCapacity)),
        ("AnexoSimetria", nameof(ClientServiceAnnexData.Symmetry)),
        ("AnexoMedio", nameof(ClientServiceAnnexData.AccessMedium)),
        ("AnexoPunto", nameof(ClientServiceAnnexData.DeliveryPoint)),
        ("AnexoInstalacion", nameof(ClientServiceAnnexData.InstallationDate)),
        ("AnexoIP", nameof(ClientServiceAnnexData.PublicIpConditions)),
        ("AnexoLatencia", nameof(ClientServiceAnnexData.Latency)),
        ("AnexoJitter", nameof(ClientServiceAnnexData.Jitter)),
        ("AnexoPerdida", nameof(ClientServiceAnnexData.PacketLoss)),
        ("AnexoMedicion", nameof(ClientServiceAnnexData.MeasurementScope)),
        ("AnexoEscalamiento", nameof(ClientServiceAnnexData.EscalationContact))
    ];

    // Retained for previously prepared contracts; these fields are no longer edited by the form.
    private static readonly HashSet<string> HistoricalFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AnexoRed", "AnexoMedio", "AnexoPunto", "AnexoIP", "AnexoLatencia", "AnexoJitter",
        "AnexoPerdida", "AnexoMedicion", "AnexoEscalamiento"
    };

    public static ClientServiceAnnexData Read(string? notes)
    {
        var values = ClientServiceContractMetadata.ParseMeta(notes);
        var data = new ClientServiceAnnexData();
        foreach (var field in Fields)
            if (values.TryGetValue(field.Key, out var value))
            {
                var property = typeof(ClientServiceAnnexData).GetProperty(field.Property)!;
                if (property.PropertyType == typeof(DateTime?))
                {
                    if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                        property.SetValue(data, date.Date);
                }
                else property.SetValue(data, value);
            }
        return data;
    }

    public static string Write(string? notes, ClientServiceAnnexData? data)
    {
        data ??= new();
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in Fields)
        {
            var raw = typeof(ClientServiceAnnexData).GetProperty(field.Property)!.GetValue(data);
            var value = (raw is DateTime date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : (string?)raw ?? "")
                .Replace('\r', ' ').Replace('\n', ' ').Trim();
            values[field.Key] = value;
        }
        var replacedKeys = values.Where(x => !HistoricalFields.Contains(x.Key) || x.Value.Length > 0)
            .Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lines = (notes ?? "").Split('\n').Select(x => x.TrimEnd('\r'))
            .Where(x => !replacedKeys.Contains(MetaKey(x))).ToList();
        foreach (var value in values.Where(x => x.Value.Length > 0))
            lines.Add($"[META] {value.Key}={value.Value}");
        return string.Join(Environment.NewLine, lines).Trim();
    }

    // Existing edit forms rebuild known metadata. Keep unrelated keys (e.g. feasibility links)
    // while allowing explicit fields from the new form to win.
    public static string PreserveMetadata(string? original, string rebuilt)
    {
        var keys = ClientServiceContractMetadata.ParseMeta(rebuilt).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var managed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Recurrencia", "Plazo", "CostoInstalacion", "COSTOINST", "TiposServicio", "VentaId",
            "InternetCapacidad", "InternetCapacidadOtro", "TelefoniaExtensiones", "TelefoniaTroncales",
            "TelefoniaDID", "CCTVCanales", "CCTVCanalesOtro", "SeguridadMarca", "SeguridadMarcaOtro",
            "ServidorSO", "ServidorNucleos", "ServidorRAM", "ServidorDisco"
        };
        var extra = (original ?? "").Split('\n').Select(x => x.Trim().TrimEnd('\r'))
            .Where(x => MetaKey(x) is { Length: > 0 } key && !managed.Contains(key) && keys.Add(key));
        return string.Join(Environment.NewLine, new[] { rebuilt }.Concat(extra)).Trim();
    }

    public static string? ValidateSymmetry(ClientServiceAnnexData? data, string? download, string? customDownload)
    {
        if (data?.Symmetry != "Simetrico") return null;
        decimal? Mbps(string? value)
        {
            var match = System.Text.RegularExpressions.Regex.Match(value?.Trim() ?? "",
                @"^(\d+(?:[.,]\d+)?)\s*(Mbps|Gbps)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success || !decimal.TryParse(match.Groups[1].Value.Replace(',', '.'),
                System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var speed)) return null;
            return speed * (match.Groups[2].Value.Equals("Gbps", StringComparison.OrdinalIgnoreCase) ? 1000m : 1m);
        }
        var down = Mbps(download == "Otro" ? customDownload : download);
        var up = Mbps(data.UploadCapacity);
        return down.HasValue && up.HasValue && down != up
            ? "Un enlace simétrico debe tener la misma velocidad de bajada y subida. Revise las capacidades o seleccione Asimétrico."
            : null;
    }

    private static string MetaKey(string line)
    {
        var clean = line.Trim();
        if (!clean.StartsWith("[META]", StringComparison.OrdinalIgnoreCase)) return "";
        var split = clean[6..].Trim().Split('=', 2, StringSplitOptions.TrimEntries);
        return split.Length == 2 ? split[0] : "";
    }

    public static IReadOnlyList<string> MissingRequired(ClientServiceContract? contract)
    {
        if (contract == null) return ["servicio contratado"];
        var technical = ClientServiceContractMetadata.ParseTechnical(contract.Notes);
        if (EventContract.IsEvent(contract) || (contract.ServiceType != ClientServiceType.Internet
            && !technical.ServiceTypes.Contains("Internet", StringComparer.OrdinalIgnoreCase))) return [];
        var data = Read(contract.Notes);
        var missing = new List<string>();
        if (data.Modality is not ("Dedicado" or "BestEffort")) missing.Add("modalidad de Internet");
        if (string.IsNullOrWhiteSpace(technical.InternetCapacity) || (technical.InternetCapacity == "Otro"
            && string.IsNullOrWhiteSpace(technical.InternetCapacityOther))) missing.Add("velocidad de bajada");
        var hasUpload = !string.IsNullOrWhiteSpace(data.UploadCapacity);
        var hasSymmetry = !string.IsNullOrWhiteSpace(data.Symmetry);
        if (hasUpload || hasSymmetry)
        {
            if (!hasUpload) missing.Add("velocidad de subida para la simetría indicada");
            if (data.Symmetry is not ("Simetrico" or "Asimetrico")) missing.Add("simetría para la velocidad de subida indicada");
            if (hasUpload && hasSymmetry && ValidateSymmetry(data, technical.InternetCapacity, technical.InternetCapacityOther) != null)
                missing.Add("velocidades coherentes con la simetría");
        }
        if (string.IsNullOrWhiteSpace(contract.BranchAddress)) missing.Add("dirección del servicio");
        if (!contract.ContractStartDate.HasValue) missing.Add("fecha de inicio del servicio");
        var metadata = ClientServiceContractMetadata.ParseMeta(contract.Notes);
        if (metadata.TryGetValue("AnexoInstalacion", out var installation) && !string.IsNullOrWhiteSpace(installation)
            && !data.InstallationDate.HasValue) missing.Add("fecha pactada de instalación o activación válida");
        metadata.TryGetValue("Plazo", out var term);
        var indefinite = string.Equals(term, "Indefinido", StringComparison.OrdinalIgnoreCase);
        var monthlyTerm = int.TryParse(term, out var months) && months > 0;
        if (!indefinite && !monthlyTerm && !contract.ContractEndDate.HasValue) missing.Add("plazo o fecha de fin del servicio");
        if (contract.ContractStartDate.HasValue && contract.ContractEndDate.HasValue
            && contract.ContractEndDate.Value.Date < contract.ContractStartDate.Value.Date)
            missing.Add("fechas de vigencia coherentes");
        if (indefinite && contract.ContractEndDate.HasValue)
            missing.Add("plazo indefinido sin fecha de fin");
        if (monthlyTerm && contract.ContractStartDate.HasValue)
        {
            try
            {
                var anniversary = contract.ContractStartDate.Value.Date.AddMonths(months);
                if (contract.ContractEndDate.HasValue && contract.ContractEndDate.Value.Date != anniversary
                    && contract.ContractEndDate.Value.Date != anniversary.AddDays(-1))
                    missing.Add("plazo coherente con las fechas de vigencia");
            }
            catch (ArgumentOutOfRangeException)
            {
                missing.Add("plazo dentro del rango de fechas admitido");
            }
        }
        return missing;
    }

    // Frozen v4 wording must keep its original completeness rules when another party signs later.
    public static IReadOnlyList<string> MissingRequiredV4(ClientServiceContract? contract)
    {
        if (contract == null) return ["servicio contratado"];
        var technical = ClientServiceContractMetadata.ParseTechnical(contract.Notes);
        if (EventContract.IsEvent(contract) || (contract.ServiceType != ClientServiceType.Internet
            && !technical.ServiceTypes.Contains("Internet", StringComparer.OrdinalIgnoreCase))) return [];
        var data = Read(contract.Notes);
        var missing = new List<string>();
        if (data.Modality is not ("Dedicado" or "BestEffort")) missing.Add("modalidad de Internet");
        if (data.NetworkMode is not ("Reventa" or "RedPropia" or "Mixta")) missing.Add("forma de prestación");
        if (string.IsNullOrWhiteSpace(technical.InternetCapacity) || (technical.InternetCapacity == "Otro"
            && string.IsNullOrWhiteSpace(technical.InternetCapacityOther))) missing.Add("velocidad de bajada");
        if (string.IsNullOrWhiteSpace(data.UploadCapacity)) missing.Add("velocidad de subida");
        if (data.Symmetry is not ("Simetrico" or "Asimetrico")) missing.Add("simetría");
        if (ValidateSymmetry(data, technical.InternetCapacity, technical.InternetCapacityOther) != null)
            missing.Add("velocidades coherentes con la simetría");
        if (string.IsNullOrWhiteSpace(contract.BranchAddress)) missing.Add("dirección del servicio");
        if (string.IsNullOrWhiteSpace(data.DeliveryPoint)) missing.Add("punto de entrega");
        if (string.IsNullOrWhiteSpace(data.AccessMedium)) missing.Add("medio de acceso");
        if (string.IsNullOrWhiteSpace(data.MeasurementScope)) missing.Add("perímetro y método de medición");
        if (!data.InstallationDate.HasValue) missing.Add("fecha pactada de instalación o activación");
        if (!contract.ContractStartDate.HasValue) missing.Add("fecha de inicio del servicio");
        var metadata = ClientServiceContractMetadata.ParseMeta(contract.Notes);
        metadata.TryGetValue("Plazo", out var term);
        var indefinite = string.Equals(term, "Indefinido", StringComparison.OrdinalIgnoreCase);
        var monthlyTerm = int.TryParse(term, out var months) && months > 0;
        var validTerm = indefinite || monthlyTerm;
        if (!validTerm && !contract.ContractEndDate.HasValue) missing.Add("plazo o fecha de fin del servicio");
        if (contract.ContractStartDate.HasValue && contract.ContractEndDate.HasValue
            && contract.ContractEndDate.Value.Date < contract.ContractStartDate.Value.Date)
            missing.Add("fechas de vigencia coherentes");
        if (indefinite && contract.ContractEndDate.HasValue)
            missing.Add("plazo indefinido sin fecha de fin");
        if (monthlyTerm && contract.ContractStartDate.HasValue)
        {
            try
            {
                var anniversary = contract.ContractStartDate.Value.Date.AddMonths(months);
                if (contract.ContractEndDate.HasValue && contract.ContractEndDate.Value.Date != anniversary
                    && contract.ContractEndDate.Value.Date != anniversary.AddDays(-1))
                    missing.Add("plazo coherente con las fechas de vigencia");
            }
            catch (ArgumentOutOfRangeException)
            {
                missing.Add("plazo dentro del rango de fechas admitido");
            }
        }
        if ((data.NetworkMode is "Reventa" or "Mixta") && string.IsNullOrWhiteSpace(contract.Provider)) missing.Add("proveedor de acceso");
        return missing;
    }

    public static Dictionary<string, string> Replacements(ClientServiceContract? contract)
    {
        var data = Read(contract?.Notes);
        var tech = ClientServiceContractMetadata.ParseTechnical(contract?.Notes);
        string Known(string? value) => string.IsNullOrWhiteSpace(value) ? "No especificado" : value.Trim();
        string Speed(string? value) => System.Text.RegularExpressions.Regex.IsMatch(value?.Trim() ?? "", @"^\d+(?:[.,]\d+)?$")
            ? value!.Trim() + " Mbps" : Known(value);
        return new()
        {
            ["ANEXONUMERO"] = Known(contract?.ContractNumber),
            ["ANEXOSEDE"] = Known(contract?.Branch),
            ["ANEXODIRECCION"] = Known(contract?.BranchAddress),
            ["ANEXOMODALIDAD"] = data.Modality switch { "Dedicado" => "Internet Dedicado", "BestEffort" => "Best Effort", _ => "No especificada" },
            ["ANEXORED"] = data.NetworkMode switch { "Reventa" => "Reventa de servicio", "RedPropia" => "Red propia", "Mixta" => "Red propia y acceso de terceros", _ => "No especificada" },
            ["ANEXOPROVEEDOR"] = Known(contract?.Provider),
            ["ANEXOTITULO"] = Known(data.AuthorizationReference),
            ["ANEXOTITULAR"] = Known(data.AuthorizationHolder),
            ["ANEXOPROFECO"] = Known(data.ProfecoRegistration),
            ["ANEXOBAJADA"] = Speed(tech.InternetCapacity == "Otro" ? tech.InternetCapacityOther : tech.InternetCapacity),
            ["ANEXOSUBIDA"] = Speed(data.UploadCapacity),
            ["ANEXOSIMETRIA"] = data.Symmetry switch { "Simetrico" => "Simétrico", "Asimetrico" => "Asimétrico", _ => "No especificada" },
            ["ANEXOMEDIO"] = Known(data.AccessMedium),
            ["ANEXOPUNTO"] = Known(data.DeliveryPoint),
            ["ANEXOINSTALACION"] = data.InstallationDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "No especificada",
            ["ANEXOIP"] = Known(data.PublicIpConditions),
            ["ANEXOLATENCIA"] = Known(data.Latency),
            ["ANEXOJITTER"] = Known(data.Jitter),
            ["ANEXOPERDIDA"] = Known(data.PacketLoss),
            ["ANEXOMEDICION"] = Known(data.MeasurementScope),
            ["ANEXOESCALAMIENTO"] = Known(data.EscalationContact)
        };
    }
}
