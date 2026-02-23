using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public class InventoryBrand
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<InventoryItem> Items { get; set; } = new();
}

public class InventoryCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class InventoryLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class InventoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // SKU es opcional (puede venir null)
    [MaxLength(60)]
    public string? Sku { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    // OJO: se guarda como texto (pero se elige desde catálogo)
    [MaxLength(100)]
    public string Category { get; set; } = "";

    // Marca / Modelo / Ubicación
    public Guid? BrandId { get; set; }
    public InventoryBrand? Brand { get; set; }

    [MaxLength(120)]
    public string? Model { get; set; }

    // OJO: se guarda como texto (pero se elige desde catálogo)
    [MaxLength(200)]
    public string? Location { get; set; }

    [MaxLength(40)]
    public string Unit { get; set; } = "pza";

    public bool IsConsumable { get; set; } = true;
    public bool IsActive { get; set; } = true;

    // Existencia actual (se actualiza al aprobar movimientos)
    public decimal QuantityOnHand { get; set; } = 0m;

    /// <summary>
    /// Umbral de reorden: cuando QuantityOnHand <= ReorderLevel se marca como bajo.
    /// </summary>
    public decimal ReorderLevel { get; set; } = 0m;

    [MaxLength(2000)]
    public string Notes { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<InventoryMovement> Movements { get; set; } = new();
}

public enum InventoryMovementType
{
    In = 1,
    Out = 2
}

public enum InventoryMovementStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ItemId { get; set; }
    public InventoryItem? Item { get; set; }

    public InventoryMovementType Type { get; set; } = InventoryMovementType.Out;

    public decimal Quantity { get; set; } = 0m;

    // Contexto
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>
    /// Si es hardware y se queda con cliente, aquí queda el ClientId. Para consumibles usualmente null.
    /// </summary>
    public Guid? AssignedClientId { get; set; }
    public Client? AssignedClient { get; set; }

    [MaxLength(120)]
    public string SerialNumber { get; set; } = "";

    [MaxLength(120)]
    public string Reference { get; set; } = ""; // OC / factura / remisión

    // Quién lo solicitó
    [MaxLength(64)]
    public string RequestedByUserId { get; set; } = "";

    [MaxLength(200)]
    public string RequestedByName { get; set; } = "";

    // Responsable (puede ser el mismo, pero admin podría asignar a otro)
    [MaxLength(64)]
    public string ResponsibleUserId { get; set; } = "";

    [MaxLength(200)]
    public string ResponsibleName { get; set; } = "";

    public InventoryMovementStatus Status { get; set; } = InventoryMovementStatus.Pending;

    [MaxLength(64)]
    public string? ApprovedByUserId { get; set; }

    [MaxLength(200)]
    public string? ApprovedByName { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(2000)]
    public string Notes { get; set; } = "";

    [MaxLength(2000)]
    public string AdminNote { get; set; } = "";
}