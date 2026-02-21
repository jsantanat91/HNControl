using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum EmployeeDeductionType
{
    PensionAlimenticia = 1,
    Prestamo = 2,
    Otro = 99
}

public enum EmployeeDeductionMode
{
    /// <summary>Importe fijo por periodo (ej: $1,200 por quincena).</summary>
    FixedAmount = 1,

    /// <summary>Porcentaje sobre el sueldo base (quincenal).</summary>
    PercentOfBase = 2,

    /// <summary>Porcentaje sobre el estimado quincenal (80/20 ya aplicado).</summary>
    PercentOfEstimatedPay = 3
}

/// <summary>
/// Deducciones del empleado para cálculo de nómina.
/// - Puede ser con vencimiento (EndDate) o indefinida (EndDate NULL).
/// - Para préstamos, puedes llevar saldo (RemainingAmount) para cortar al llegar a cero.
/// </summary>
public class EmployeeDeduction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string UserId { get; set; } = "";
    public EmployeeProfile? Employee { get; set; }

    public EmployeeDeductionType Type { get; set; } = EmployeeDeductionType.Otro;

    [Required, MaxLength(200)]
    public string Concept { get; set; } = "";

    public EmployeeDeductionMode Mode { get; set; } = EmployeeDeductionMode.FixedAmount;

    /// <summary>
    /// Para FixedAmount: importe por periodo.
    /// Para modos Percent*: se usa como respaldo (opcional), el cálculo usa Rate.
    /// </summary>
    public decimal Amount { get; set; } = 0m;

    /// <summary>
    /// Para Percent*: 0.10 = 10%.
    /// </summary>
    public decimal Rate { get; set; } = 0m;

    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Solo para préstamos: saldo restante. Si es NULL, se considera indefinido.
    /// </summary>
    public decimal? RemainingAmount { get; set; }

    /// <summary>
    /// Solo para préstamos (opcional): monto original del préstamo (para referencia).
    /// </summary>
    public decimal? TotalAmount { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
