using System.ComponentModel.DataAnnotations;
using System.Net;

namespace HNControl.Web.Services;

public class ServiceInventoryAccountInput
{
    public Guid RecordId { get; set; } = Guid.NewGuid();
    public Guid Version { get; set; }
    [Required(ErrorMessage = "Selecciona un carrier."), MaxLength(32)] public string CarrierKey { get; set; } = "starlink";
    [MaxLength(120)] public string? OtherCarrierName { get; set; }
    [Required(ErrorMessage = "Escribe un nombre para identificar la cuenta."), MaxLength(140)] public string Label { get; set; } = "";
    [Required(ErrorMessage = "Escribe el correo de la cuenta."), EmailAddress(ErrorMessage = "Escribe un correo válido."), MaxLength(256)] public string Email { get; set; } = "";
    [MaxLength(120)] public string? AccountNumber { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ServiceInventoryItemInput : IValidatableObject
{
    public Guid RecordId { get; set; } = Guid.NewGuid();
    public Guid Version { get; set; }
    [Required(ErrorMessage = "Selecciona un carrier."), MaxLength(32)] public string CarrierKey { get; set; } = "starlink";
    [MaxLength(120)] public string? OtherCarrierName { get; set; }
    [Required(ErrorMessage = "Escribe el nombre del servicio."), MaxLength(180)] public string Name { get; set; } = "";
    public Guid? AccountId { get; set; }
    public Guid? ClientId { get; set; }
    [MaxLength(120)] public string? AccountNumber { get; set; }
    [MaxLength(160)] public string? PlanName { get; set; }
    [MaxLength(32)] public string? PlanType { get; set; }
    [MaxLength(32)] public string? DeliveryType { get; set; }
    [MaxLength(120)] public string? DeviceModel { get; set; }
    [MaxLength(120)] public string? SerialNumber { get; set; }
    [MaxLength(120)] public string? KitNumber { get; set; }
    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "La velocidad debe ser mayor que cero.")] public decimal? SpeedMbps { get; set; }
    [Range(typeof(decimal), "0", "999999999.99", ErrorMessage = "El costo no puede ser negativo.")] public decimal? MonthlyCost { get; set; }
    [MaxLength(500)] public string? PhysicalLocation { get; set; }
    [MaxLength(80)] public string? Lan { get; set; }
    [MaxLength(253)] public string? Ddns { get; set; }
    [MaxLength(60)] public string? SupportPhone { get; set; }
    [MaxLength(64)] public string? FixedIp { get; set; }
    [MaxLength(64)] public string? SubnetMask { get; set; }
    [MaxLength(64)] public string? Gateway { get; set; }
    [MaxLength(200)] public string? Dns { get; set; }
    [Range(1, 240, ErrorMessage = "El plazo debe estar entre 1 y 240 meses.")] public int? ContractMonths { get; set; }
    [DataType(DataType.Date)] public DateOnly? ContractStartsOn { get; set; }
    [DataType(DataType.Date)] public DateOnly? ContractEndsOn { get; set; }
    [MaxLength(3000)] public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ContractStartsOn.HasValue && ContractEndsOn.HasValue && ContractEndsOn < ContractStartsOn)
            yield return new("La fecha de término no puede ser anterior al inicio.", new[] { nameof(ContractEndsOn) });
        foreach (var (name, value) in new[] { (nameof(FixedIp), FixedIp), (nameof(Gateway), Gateway), (nameof(SubnetMask), SubnetMask) })
            if (!string.IsNullOrWhiteSpace(value) && !IPAddress.TryParse(value.Trim(), out _))
                yield return new("Escribe una dirección IP válida.", new[] { name });
        if (!string.IsNullOrWhiteSpace(Dns) && Dns.Split(new[] { ',', ';', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Any(x => !IPAddress.TryParse(x, out _)))
            yield return new("Escribe direcciones DNS válidas, separadas por coma.", new[] { nameof(Dns) });
        if (MonthlyCost.HasValue && decimal.Round(MonthlyCost.Value, 2) != MonthlyCost.Value)
            yield return new("Usa un máximo de dos decimales para el costo.", new[] { nameof(MonthlyCost) });
    }
}

public record ServiceInventoryIssue(string Field, string Message);
public record ServiceInventorySaveResult(Guid? Id, IReadOnlyList<ServiceInventoryIssue> Errors)
{
    public bool Success => Id.HasValue && Errors.Count == 0;
    public static ServiceInventorySaveResult Error(string field, string message) => new(null, new[] { new ServiceInventoryIssue(field, message) });
}

public static class ServiceInventoryDisplay
{
    public static string CarrierName(string key, string? other = null) => key == "otro" && !string.IsNullOrWhiteSpace(other)
        ? other : ServiceInventoryCatalog.Carriers.FirstOrDefault(x => x.Id == key)?.Name ?? key;
    public static string? Logo(string key) => ServiceInventoryCatalog.Carriers.FirstOrDefault(x => x.Id == key)?.LogoPath;
    public static readonly string[] PlanTypes = { "Residencial", "Itinerante", "Empresarial", "Otro" };
    public static readonly string[] DeliveryTypes = { "Best effort", "Dedicado", "Satelital", "Otro" };
}
