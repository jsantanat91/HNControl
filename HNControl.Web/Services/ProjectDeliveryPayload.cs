using System.Text.Json;

namespace HNControl.Web.Services;

public static class ProjectDeliveryPayload
{
    public const string Prefix = "__DELIVERYJSON__";
    public const int MaxRows = 5;
    public const int MaxEvidenceFiles = 5;

    public static DeliveryTemplateData Parse(string? serviceSummary)
    {
        if (string.IsNullOrWhiteSpace(serviceSummary) || !serviceSummary.StartsWith(Prefix, StringComparison.Ordinal))
            return new DeliveryTemplateData();

        try
        {
            var json = serviceSummary[Prefix.Length..];
            return JsonSerializer.Deserialize<DeliveryTemplateData>(json) ?? new DeliveryTemplateData();
        }
        catch
        {
            return new DeliveryTemplateData();
        }
    }

    public static string Serialize(DeliveryTemplateData data) =>
        Prefix + JsonSerializer.Serialize(data);

    public static string ServicesDisplay(DeliveryTemplateData root)
    {
        var lines = root.Services
            .Where(x => !string.IsNullOrWhiteSpace(x.Servicio) || !string.IsNullOrWhiteSpace(x.Modalidad) || !string.IsNullOrWhiteSpace(x.Plazo))
            .Select(x => $"{Safe(x.Servicio)} | {Safe(x.Modalidad)} | {Safe(x.Plazo)}")
            .ToList();

        return lines.Count == 0 ? "-" : string.Join("\n", lines);
    }

    public static string EquipmentDisplay(DeliveryTemplateData root)
    {
        var lines = root.Equipment
            .Where(x => !string.IsNullOrWhiteSpace(x.Equipo) || !string.IsNullOrWhiteSpace(x.Cantidad))
            .Select(x => $"{Safe(x.Equipo)} ({Safe(x.Cantidad)})")
            .ToList();

        return lines.Count == 0 ? "-" : string.Join("\n", lines);
    }

    public static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}

public sealed class DeliveryTemplateData
{
    public string? NOMBREPROYECTO { get; set; }
    public string? NOMBRETECNICO { get; set; }
    public string? SEGMENTOLAN { get; set; }
    public string? IPPUBLICA { get; set; }
    public List<DeliveryServiceRow> Services { get; set; } = [];
    public List<DeliveryEquipmentRow> Equipment { get; set; } = [];
    public List<DeliveryEvidenceRow> Evidences { get; set; } = [];
}

public sealed class DeliveryServiceRow
{
    public string? Servicio { get; set; }
    public string? Modalidad { get; set; }
    public string? Plazo { get; set; }
}

public sealed class DeliveryEquipmentRow
{
    public string? Equipo { get; set; }
    public string? Cantidad { get; set; }
}

public sealed class DeliveryEvidenceRow
{
    public string? StoragePath { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
}
