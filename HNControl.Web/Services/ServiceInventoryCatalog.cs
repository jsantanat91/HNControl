namespace HNControl.Web.Services;

public sealed record ServiceInventoryCarrierOption(string Id, string Name, string LogoPath);

public sealed record ServiceInventoryModelOption(string Value, string Label);

/// <summary>
/// Display suggestions for the independent service inventory. Models remain free text
/// so existing kit aliases and future hardware can be registered without a migration.
/// Official sources and original logo assets are documented in docs/service-inventory-catalog.md.
/// </summary>
public static class ServiceInventoryCatalog
{
    public static IReadOnlyList<ServiceInventoryCarrierOption> Carriers { get; } =
    [
        new("starlink", "Starlink", "/img/carriers/starlink.png"),
        new("telmex", "Telmex", "/img/carriers/telmex.svg"),
        new("totalplay", "Totalplay", "/img/carriers/totalplay.svg"),
        new("izzi", "izzi", "/img/carriers/izzi.svg"),
        new("megacable", "Megacable / MEGA", "/img/carriers/megacable.png"),
        new("otro", "Otro carrier", "")
    ];

    // Starlink now distinguishes the antenna kit name from the router generation.
    // Standard 4 and Standard 4 X must not be inferred from a user's "v3"/"v4" alias.
    public static IReadOnlyList<ServiceInventoryModelOption> StarlinkModels { get; } =
    [
        new("Standard Circular", "Standard Circular · circular motorizada (legado)"),
        new("Standard Actuated", "Standard Actuated · rectangular motorizada"),
        new("Standard 4", "Standard 4 · kit con Router Mini"),
        new("Standard 4 X", "Standard 4 X · kit con Router 3"),
        new("Mini", "Mini · antena compacta con Wi-Fi integrado"),
        new("Enterprise", "Enterprise · kit empresarial"),
        new("Performance (Gen 1)", "Performance (Gen 1) · alto rendimiento motorizada"),
        new("Performance (Gen 2)", "Performance (Gen 2) · alto rendimiento plana"),
        new("Performance (Gen 3)", "Performance (Gen 3) · alto rendimiento")
    ];
}
