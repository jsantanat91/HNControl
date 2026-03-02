using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum EmployeeDeductionType
{
    [Display(Name = "Pensión alimenticia")]
    PensionAlimenticia = 1,

    [Display(Name = "Préstamo")]
    Prestamo = 2,

    [Display(Name = "Prima vacacional")]
    PrimaVacacional = 3,

    [Display(Name = "Otro")]
    Otro = 99
}

/// <summary>
/// Dirección del ajuste:
/// 1 = Deducción (resta), 2 = Bono (suma).
/// </summary>
public enum EmployeeDeductionDirection
{
    [Display(Name = "Deducción")]
    Deduct = 1,

    [Display(Name = "Bono")]
    Bonus = 2
}

public enum EmployeeDeductionMode
{
    /// <summary>Importe fijo por periodo (ej: $1,200 por quincena).</summary>
    [Display(Name = "Monto fijo")]
    FixedAmount = 1,

    /// <summary>Porcentaje sobre el sueldo base (quincenal).</summary>
    [Display(Name = "% sobre sueldo base")]
    PercentOfBase = 2,

    /// <summary>Porcentaje sobre el estimado quincenal (80/20 ya aplicado).</summary>
    [Display(Name = "% sobre nómina estimada (80/20)")]
    PercentOfEstimatedPay = 3
}

public enum EmployeeDeductionFrequency
{
    [Display(Name = "Quincenal")]
    Biweekly = 1,

    [Display(Name = "Mensual")]
    Monthly = 2
}

public enum EmployeeDeductionApplyOnHalf
{
    [Display(Name = "1ª quincena (1–15)")]
    First = 1,

    [Display(Name = "2ª quincena (16–fin)")]
    Second = 2
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

    public EmployeeDeductionDirection Direction { get; set; } = EmployeeDeductionDirection.Deduct;

    [Required, MaxLength(200)]
    public string Concept { get; set; } = "";

    public EmployeeDeductionMode Mode { get; set; } = EmployeeDeductionMode.FixedAmount;

    /// <summary>
    /// Frecuencia de aplicación:
    /// - Quincenal: aplica en cada nómina.
    /// - Mensual: aplica 1 vez al mes (según ApplyOnHalf).
    /// </summary>
    public EmployeeDeductionFrequency Frequency { get; set; } = EmployeeDeductionFrequency.Biweekly;

    /// <summary>
    /// Solo si Frequency = Monthly: en qué quincena se aplica.
    /// NULL = por defecto 1ª quincena.
    /// </summary>
    public EmployeeDeductionApplyOnHalf? ApplyOnHalf { get; set; }

    /// <summary>
    /// Plazo en periodos.
    /// - Quincenal: número de quincenas
    /// - Mensual: número de meses
    /// NULL = indefinido
    /// </summary>
    public int? TermCount { get; set; }

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
