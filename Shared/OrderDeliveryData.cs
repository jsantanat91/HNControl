using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HNControl.Shared;

public sealed class OrderDeliveryData
{
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Kind { get; set; } = "service";
    public bool HasRouter { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string RouterBrand { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string RouterModel { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string RouterSerial { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string RouterMode { get; set; } = "Comodato";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string RouterCondition { get; set; } = "Nuevo";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string RouterAccessories { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Notes { get; set; } = "";
    public List<DeliveryEquipment> Items { get; set; } = new();
    public static readonly string[] Kinds = ["service", "hardware", "both"];
    public static readonly string[] Modes = ["Venta", "Comodato", "Renta", "Propiedad del cliente"];
    public static readonly string[] Conditions = ["Nuevo", "Usado", "Reacondicionado"];
    [JsonIgnore] public string KindLabel => Kind switch { "hardware" => "Entrega de hardware", "both" => "Servicio + hardware", _ => "Servicio" };
    [JsonIgnore] public IEnumerable<DeliveryEquipment> Equipment => (HasRouter ? new[] { new DeliveryEquipment
    {
        Name = "Router", Brand = RouterBrand, Model = RouterModel, Serial = RouterSerial,
        Mode = RouterMode, Condition = RouterCondition, Accessories = RouterAccessories, Quantity = 1
    } } : Array.Empty<DeliveryEquipment>()).Concat(Items ?? new());

    public static OrderDeliveryData Read(string? json) => string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<OrderDeliveryData>(json) ?? new();
    public string Serialize() => JsonSerializer.Serialize(this);

    public string? Validate(bool closing = false)
    {
        if (!Kinds.Contains(Kind)) return "Selecciona servicio, hardware o ambos.";
        if (Items == null || Items.Count > 20 || Items.Any(x => x == null)) return "Puedes registrar hasta 20 equipos adicionales.";
        if ((Notes?.Length ?? 0) > 2000) return "Las observaciones admiten hasta 2000 caracteres.";
        if (HasRouter && (string.IsNullOrWhiteSpace(RouterBrand) || string.IsNullOrWhiteSpace(RouterSerial)))
            return "Escribe la marca y la serie del router.";
        foreach (var item in Equipment)
        {
            if (string.IsNullOrWhiteSpace(item.Name)) return "Escribe el nombre de cada equipo.";
            if (item.Quantity < 1 || item.Quantity > 10000) return "La cantidad debe estar entre 1 y 10000.";
            if (!Modes.Contains(item.Mode) || !Conditions.Contains(item.Condition)) return "Selecciona modalidad y estado válidos para cada equipo.";
            if (new[] { item.Name, item.Brand, item.Model, item.Serial }.Any(x => (x?.Length ?? 0) > 120)
                || (item.Accessories?.Length ?? 0) > 400) return "Equipo, marca, modelo y serie admiten 120 caracteres; accesorios, 400.";
            if (!string.IsNullOrWhiteSpace(item.Serial) && item.Quantity != 1) return "Registra una fila por equipo con serie; su cantidad debe ser 1.";
        }
        var serials = Equipment.Select(x => x.Serial?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (serials.Distinct(StringComparer.OrdinalIgnoreCase).Count() != serials.Count) return "Hay números de serie repetidos en la entrega.";
        if (closing && Kind != "service" && !Equipment.Any()) return "Agrega el router o al menos un equipo antes de firmar la entrega de hardware.";
        if (Serialize().Length > 30000) return "Reduce las observaciones y accesorios de la entrega.";
        return null;
    }
}

public sealed class DeliveryEquipment
{
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Name { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Brand { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Model { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Serial { get; set; } = "";
    public int Quantity { get; set; } = 1;
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Accessories { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Condition { get; set; } = "Nuevo";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Mode { get; set; } = "Venta";
}
