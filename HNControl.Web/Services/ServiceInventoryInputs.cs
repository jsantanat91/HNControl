using System.ComponentModel.DataAnnotations;
using System.Net;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HNControl.Web.Services;

public class ServiceInventoryAccountInput
{
    public Guid RecordId { get; set; } = Guid.NewGuid();
    public Guid Version { get; set; }
    [Required(ErrorMessage = "Selecciona un proveedor."), MaxLength(32)] public string CarrierKey { get; set; } = "starlink";
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
    [Required(ErrorMessage = "Selecciona un proveedor."), MaxLength(32)] public string CarrierKey { get; set; } = "starlink";
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
    public bool HasNetworkDetails { get; set; }
    public string? Lan { get; set; }
    public string? Ddns { get; set; }
    [MaxLength(60)] public string? SupportPhone { get; set; }
    public string? FixedIp { get; set; }
    public string? SubnetMask { get; set; }
    public string? Gateway { get; set; }
    public string? Dns { get; set; }
    [Range(1, 240, ErrorMessage = "El plazo debe estar entre 1 y 240 meses.")] public int? ContractMonths { get; set; }
    [DataType(DataType.Date)] public DateOnly? ContractStartsOn { get; set; }
    [BindNever, DataType(DataType.Date)] public DateOnly? ContractEndsOn { get; set; }
    [MaxLength(3000)] public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (HasNetworkDetails)
        {
            foreach (var (name, value, limit) in new[] { (nameof(Lan), Lan, 80), (nameof(Ddns), Ddns, 253),
                (nameof(FixedIp), FixedIp, 64), (nameof(SubnetMask), SubnetMask, 64), (nameof(Gateway), Gateway, 64), (nameof(Dns), Dns, 200) })
                if (value?.Length > limit) yield return new($"Usa un máximo de {limit} caracteres.", new[] { name });
            foreach (var (name, value) in new[] { (nameof(FixedIp), FixedIp), (nameof(Gateway), Gateway), (nameof(SubnetMask), SubnetMask) })
                if (!string.IsNullOrWhiteSpace(value) && !IPAddress.TryParse(value.Trim(), out _))
                    yield return new("Escribe una dirección IP válida.", new[] { name });
            if (!string.IsNullOrWhiteSpace(Dns) && Dns.Split(new[] { ',', ';', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Any(x => !IPAddress.TryParse(x, out _)))
                yield return new("Escribe direcciones DNS válidas, separadas por coma.", new[] { nameof(Dns) });
        }
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
    public static IReadOnlyList<string> GetPlanTypes(string carrier) => carrier == "starlink"
        ? new[] { "Residencial", "Itinerante", "Empresarial" } : new[] { "Dedicado", "Best effort", "Empresarial" };
    public static IReadOnlyList<string> GetPlanNames(string carrier) => carrier == "starlink"
        ? new[] { "Itinerante 100 GB", "Itinerante Ilimitado", "Residencial", "Residencial Lite", "Empresarial Global" }
        : new[] { "Internet 50", "Internet 100", "Internet 200", "Internet 300", "Internet 400", "Internet 500", "Internet 1000" };
    public static IReadOnlyList<string> GetDeliveryTypes(string carrier) => carrier == "starlink"
        ? new[] { "Satelital" } : new[] { "Fibra óptica", "Microonda", "Otro" };
    public static bool HasNetworkDetails(ServiceInventoryItem item) => new[] { item.FixedIp, item.SubnetMask,
        item.Gateway, item.Dns, item.Lan, item.Ddns }.Any(x => !string.IsNullOrWhiteSpace(x));
    public static DateOnly? CalculateContractEnd(DateOnly? start, int? months)
    {
        if (!start.HasValue || months is not (>= 1 and <= 240)) return null;
        try { return start.Value.AddMonths(months.Value); }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}
