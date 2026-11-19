using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HNControl.Web.Models;

namespace HNControl.Web.Services;

public sealed class WebServiceContractData
{
    [MaxLength(100)] public string? PlatformName { get; set; }
    [MaxLength(200), WebServiceUrl] public string? ServiceUrl { get; set; }
    [MaxLength(80)] public string? PlanName { get; set; }
    [MaxLength(600)] public string? IncludedScope { get; set; }
    [Range(1, 1000000)] public int? LicensedQuantity { get; set; }
    [MaxLength(40)] public string? LicenseUnit { get; set; }
    [MaxLength(120)] public string? SupportHours { get; set; }
}

public sealed class WebServiceUrlAttribute : ValidationAttribute
{
    public WebServiceUrlAttribute() => ErrorMessage = "Use una URL HTTPS pública, sin usuario, contraseña, parámetros ni fragmentos.";
    public override bool IsValid(object? value) => value == null || string.IsNullOrWhiteSpace(value.ToString())
        || WebServiceContract.IsValidUrl(value.ToString());
}

public static class WebServiceContract
{
    public const string Variant = "WebMensual";
    public const string StandardVariant = "Servicios";
    public const string CheckInUrl = "https://checkin.hubnet-solutions.net/";
    public const string GpsUrl = "https://gps.hubnet-solutions.com.mx/";
    private const string VariantKey = "VarianteContrato";
    private static readonly (string Key, string Property)[] Fields =
    [
        ("WebPlataforma", nameof(WebServiceContractData.PlatformName)), ("WebUrl", nameof(WebServiceContractData.ServiceUrl)),
        ("WebPlan", nameof(WebServiceContractData.PlanName)), ("WebAlcance", nameof(WebServiceContractData.IncludedScope)),
        ("WebCantidad", nameof(WebServiceContractData.LicensedQuantity)), ("WebUnidad", nameof(WebServiceContractData.LicenseUnit)),
        ("WebHorario", nameof(WebServiceContractData.SupportHours))
    ];

    public static bool IsWeb(ClientServiceContract? contract) => ClientServiceContractMetadata.ParseMeta(contract?.Notes)
        .TryGetValue(VariantKey, out var variant) && string.Equals(variant, Variant, StringComparison.OrdinalIgnoreCase);

    public static bool IsValidUrl(string? value) => !string.IsNullOrWhiteSpace(value)
        && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);

    public static WebServiceContractData Read(string? notes)
    {
        var meta = ClientServiceContractMetadata.ParseMeta(notes);
        var data = new WebServiceContractData();
        foreach (var field in Fields)
        {
            if (!meta.TryGetValue(field.Key, out var value)) continue;
            var property = typeof(WebServiceContractData).GetProperty(field.Property)!;
            if (property.PropertyType == typeof(int?))
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
                    property.SetValue(data, quantity);
            }
            else property.SetValue(data, value);
        }
        return data;
    }

    public static string Write(string? notes, WebServiceContractData? data, bool enabled)
    {
        var replaced = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { VariantKey };
        if (enabled)
        {
            replaced.UnionWith(Fields.Select(x => x.Key));
            replaced.UnionWith(["Recurrencia", "TiposServicio", "Plazo"]);
        }
        var lines = (notes ?? "").Split('\n').Select(x => x.TrimEnd('\r'))
            .Where(x => !replaced.Contains(MetaKey(x))).ToList();
        if (enabled)
        {
            lines.Add($"[META] {VariantKey}={Variant}");
            lines.Add("[META] Recurrencia=Mensual");
            lines.Add("[META] TiposServicio=Otro");
            lines.Add("[META] Plazo=Indefinido");
            data ??= new();
            foreach (var field in Fields)
            {
                var raw = typeof(WebServiceContractData).GetProperty(field.Property)!.GetValue(data);
                var value = raw is int quantity ? quantity.ToString(CultureInfo.InvariantCulture) : raw as string ?? "";
                value = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
                if (value.Length > 0) lines.Add($"[META] {field.Key}={value}");
            }
        }
        return string.Join(Environment.NewLine, lines).Trim();
    }

    // Shared commercial fields remain editable. Hidden technical fields retain their stored values.
    public static string PreserveHiddenServiceMetadata(string? original, string rebuilt)
    {
        var shared = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Recurrencia", "TiposServicio", "Plazo", "CostoInstalacion", "COSTOINST", "VentaId" };
        var preserved = ClientServiceContractMetadata.ParseMeta(original).Where(x => !shared.Contains(x.Key))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var lines = rebuilt.Split('\n').Select(x => x.TrimEnd('\r')).Where(x => !preserved.ContainsKey(MetaKey(x)));
        return string.Join(Environment.NewLine, lines.Concat(preserved.Select(x => $"[META] {x.Key}={x.Value}"))).Trim();
    }

    public static IReadOnlyList<string> MissingRequired(ClientServiceContract? contract)
    {
        if (contract == null) return ["servicio web vinculado"];
        if (!IsWeb(contract)) return [];
        var data = Read(contract.Notes);
        var meta = ClientServiceContractMetadata.ParseMeta(contract.Notes);
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(data.PlatformName)) missing.Add("nombre de la plataforma");
        if (!IsValidUrl(data.ServiceUrl)) missing.Add("URL HTTPS pública de la plataforma");
        if (string.IsNullOrWhiteSpace(data.IncludedScope)) missing.Add("alcance incluido del servicio web");
        if (string.IsNullOrWhiteSpace(data.SupportHours)) missing.Add("horario de soporte contratado");
        var hasQuantity = meta.TryGetValue("WebCantidad", out var quantityText) && !string.IsNullOrWhiteSpace(quantityText);
        var hasUnit = !string.IsNullOrWhiteSpace(data.LicenseUnit);
        if (hasQuantity || hasUnit)
        {
            if (data.LicensedQuantity is not (>= 1 and <= 1000000)) missing.Add("cantidad de licencias entre 1 y 1000000");
            if (!hasUnit) missing.Add("unidad de licencia");
        }
        if (contract.MonthlyAmount is not > 0) missing.Add("mensualidad mayor a cero");
        if (!contract.ContractStartDate.HasValue) missing.Add("fecha de inicio del servicio web");
        if (meta.TryGetValue("Recurrencia", out var recurrence) && !string.IsNullOrWhiteSpace(recurrence)
            && !recurrence.Equals("Mensual", StringComparison.OrdinalIgnoreCase)) missing.Add("facturación mensual del servicio web");
        if (meta.TryGetValue("Plazo", out var term) && !string.IsNullOrWhiteSpace(term)
            && !term.Equals("Indefinido", StringComparison.OrdinalIgnoreCase)) missing.Add("servicio web mensual sin plazo forzoso");
        if (contract.ContractEndDate.HasValue) missing.Add("servicio web sin fecha de vencimiento fija");
        return missing;
    }

    public static Dictionary<string, string> Replacements(ClientServiceContract? contract)
    {
        var data = Read(contract?.Notes);
        string Known(string? value) => string.IsNullOrWhiteSpace(value) ? "No especificado" : value.Trim();
        return new()
        {
            ["WEBPLATAFORMA"] = Known(data.PlatformName), ["WEBURL"] = IsValidUrl(data.ServiceUrl) ? data.ServiceUrl!.Trim() : "No especificada",
            ["WEBPLAN"] = Known(data.PlanName), ["WEBALCANCE"] = Known(data.IncludedScope),
            ["WEBLICENCIAS"] = data.LicensedQuantity is >= 1 and <= 1000000 && !string.IsNullOrWhiteSpace(data.LicenseUnit)
                ? $"{data.LicensedQuantity.Value.ToString(CultureInfo.InvariantCulture)} {data.LicenseUnit.Trim()}" : "No especificadas",
            ["WEBHORARIO"] = Known(data.SupportHours)
        };
    }

    private static string MetaKey(string line)
    {
        var value = line.Trim();
        if (!value.StartsWith("[META]", StringComparison.OrdinalIgnoreCase)) return "";
        var parts = value[6..].Trim().Split('=', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? parts[0] : "";
    }
}
