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
    public Guid? AccountId { get; set; }
    public string AccountNumber { get; set; } = "";
    public string AccountLabel { get; set; } = "";
    public string AccountEmail { get; set; } = "";
    public string ProviderAccountNumber { get; set; } = "";
    public string AccountNotes { get; set; } = "";
    public bool AccountIsActive { get; set; }
    public bool CanCopyPassword { get; set; }
    public decimal? MonthlyCost { get; set; }
    public string Notes { get; set; } = "";
    public int? ContractMonths { get; set; }
    public DateOnly? ContractStartsOn { get; set; }
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
    public string AccountSummary => !string.IsNullOrWhiteSpace(ProviderAccountNumber) ? "Cuenta: " + ProviderAccountNumber
        : !string.IsNullOrWhiteSpace(AccountNumber) ? "Servicio / línea: " + AccountNumber : "Cuenta: " + (AccountId.HasValue ? AccountLabel : "Sin vincular");
    public string? SupportNumber
    {
        get
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(SupportPhone ?? "", @"^\+?[\d\s().-]+$")) return null;
            var digits = new string((SupportPhone ?? "").Where(char.IsAsciiDigit).ToArray());
            return digits.Length is >= 7 and <= 15 ? ((SupportPhone ?? "").TrimStart().StartsWith('+') ? "+" : "") + digits : null;
        }
    }
    public string Carrier => CarrierKey == "otro" ? OtherCarrierName : CarrierKey.ToUpperInvariant();
    public string Summary => string.Join(" · ", new[] { PlanName, PlanType, DeliveryType, SpeedMbps.HasValue ? $"{SpeedMbps:0.##} Mbps" : "" }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
