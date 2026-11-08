using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HNControl.Web.Models;

// Separado del catálogo histórico InternetCarrier / ClientCarrierService.
public class ServiceInventoryAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(32)] public string CarrierKey { get; set; } = "";
    [MaxLength(120)] public string OtherCarrierName { get; set; } = "";
    [MaxLength(140)] public string Label { get; set; } = "";
    [MaxLength(256)] public string Email { get; set; } = "";
    [MaxLength(120)] public string AccountNumber { get; set; } = "";
    [JsonIgnore] public string PasswordProtected { get; set; } = "";
    [MaxLength(2000)] public string Notes { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
    public List<ServiceInventoryItem> Services { get; set; } = new();
}

public class ServiceInventoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(32)] public string CarrierKey { get; set; } = "";
    [MaxLength(120)] public string OtherCarrierName { get; set; } = "";
    [MaxLength(180)] public string Name { get; set; } = "";
    public Guid? AccountId { get; set; }
    public ServiceInventoryAccount? Account { get; set; }
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }
    [MaxLength(120)] public string AccountNumber { get; set; } = "";
    [MaxLength(160)] public string PlanName { get; set; } = "";
    [MaxLength(32)] public string PlanType { get; set; } = "";
    [MaxLength(32)] public string DeliveryType { get; set; } = "";
    [MaxLength(120)] public string DeviceModel { get; set; } = "";
    [MaxLength(120)] public string SerialNumber { get; set; } = "";
    [MaxLength(120)] public string KitNumber { get; set; } = "";
    public decimal? SpeedMbps { get; set; }
    public decimal? MonthlyCost { get; set; }
    [MaxLength(500)] public string PhysicalLocation { get; set; } = "";
    [MaxLength(80)] public string Lan { get; set; } = "";
    [MaxLength(253)] public string Ddns { get; set; } = "";
    [MaxLength(60)] public string SupportPhone { get; set; } = "";
    [MaxLength(64)] public string FixedIp { get; set; } = "";
    [MaxLength(64)] public string SubnetMask { get; set; } = "";
    [MaxLength(64)] public string Gateway { get; set; } = "";
    [MaxLength(200)] public string Dns { get; set; } = "";
    public int? ContractMonths { get; set; }
    public DateOnly? ContractStartsOn { get; set; }
    public DateOnly? ContractEndsOn { get; set; }
    [MaxLength(3000)] public string Notes { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}
