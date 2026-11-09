namespace HNControl.Mobile.Models;

public sealed class InventoryFilterDto
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public override string ToString() => Label;
}
public sealed class ServiceInventoryResponse
{
    public List<ServiceInventoryRow> Items { get; set; } = new();
    public List<InventoryFilterDto> Clients { get; set; } = new();
    public List<InventoryFilterDto> Carriers { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
}
public sealed class ServiceInventoryRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string CarrierKey { get; set; } = "";
    public string OtherCarrierName { get; set; } = "";
    public string Client { get; set; } = "";
    public string PlanName { get; set; } = "";
    public string PlanType { get; set; } = "";
    public string DeliveryType { get; set; } = "";
    public decimal? SpeedMbps { get; set; }
    public string DeviceModel { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string KitNumber { get; set; } = "";
    public string PhysicalLocation { get; set; } = "";
    public bool IsActive { get; set; }
    public string SupportPhone { get; set; } = "";
    public string FixedIp { get; set; } = "";
    public string SubnetMask { get; set; } = "";
    public string Gateway { get; set; } = "";
    public string Dns { get; set; } = "";
    public string Lan { get; set; } = "";
    public string Ddns { get; set; } = "";
    public DateOnly? ContractEndsOn { get; set; }
    public string Status => IsActive ? "Activo" : "Inactivo";
    public string Carrier => CarrierKey == "otro" ? OtherCarrierName : CarrierKey.ToUpperInvariant();
    public string Summary => string.Join(" · ", new[] { PlanName, PlanType, DeliveryType, SpeedMbps.HasValue ? $"{SpeedMbps:0.##} Mbps" : "" }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
