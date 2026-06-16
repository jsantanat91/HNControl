using System.Globalization;

namespace HNControl.Web.Services;

public sealed record ClientServiceTechnicalMetadata(
    IReadOnlyList<string> ServiceTypes,
    string InternetCapacity,
    string InternetCapacityOther,
    string TelephonyExtensions,
    string TelephonyTrunks,
    string TelephonyDids,
    string CctvChannels,
    string CctvChannelsOther,
    string SecurityBrand,
    string SecurityBrandOther,
    string ServerOs,
    string ServerCpuCores,
    string ServerRam,
    string ServerDisk,
    decimal InstallationCost
)
{
    public static ClientServiceTechnicalMetadata Empty { get; } = new(
        [],
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        0m);
}

public static class ClientServiceContractMetadata
{
    private static readonly string[] KnownTypes =
    [
        "Internet",
        "Telefonia",
        "CCTV",
        "Seguridad",
        "Servidores",
        "Hardware",
        "Otro"
    ];

    public static ClientServiceTechnicalMetadata ParseTechnical(string? notes)
    {
        var map = ParseMeta(notes);
        var types = Read(map, "TiposServicio")
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => KnownTypes.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ClientServiceTechnicalMetadata(
            types,
            Read(map, "InternetCapacidad"),
            Read(map, "InternetCapacidadOtro"),
            Read(map, "TelefoniaExtensiones"),
            Read(map, "TelefoniaTroncales"),
            Read(map, "TelefoniaDID"),
            Read(map, "CCTVCanales"),
            Read(map, "CCTVCanalesOtro"),
            Read(map, "SeguridadMarca"),
            Read(map, "SeguridadMarcaOtro"),
            Read(map, "ServidorSO"),
            Read(map, "ServidorNucleos"),
            Read(map, "ServidorRAM"),
            Read(map, "ServidorDisco"),
            ParseMoney(Read(map, "CostoInstalacion")));
    }

    public static string StripMeta(string? notes)
    {
        var lines = (notes ?? string.Empty).Split('\n')
            .Select(x => x.TrimEnd('\r'))
            .Where(x => !x.StartsWith("[META]", StringComparison.OrdinalIgnoreCase));
        return string.Join(Environment.NewLine, lines).Trim();
    }

    private static Dictionary<string, string> ParseMeta(string? notes)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in (notes ?? string.Empty).Split('\n'))
        {
            var clean = line.Trim().TrimEnd('\r');
            if (!clean.StartsWith("[META]", StringComparison.OrdinalIgnoreCase))
                continue;

            var payload = clean[6..].Trim();
            var parts = payload.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
                map[parts[0]] = parts[1];
        }

        return map;
    }

    private static string Read(Dictionary<string, string> map, string key) =>
        map.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static decimal ParseMoney(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariant))
            return Math.Max(0m, invariant);
        if (decimal.TryParse(raw, NumberStyles.Any, new CultureInfo("es-MX"), out var localized))
            return Math.Max(0m, localized);
        return 0m;
    }
}
