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



public enum EmployeeDeductionFrequency
{
    [Display(Name = "Quincenal")]
    Biweekly = 1,

    [Display(Name = "Mensual")]
    Monthly = 2
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

    public EmployeeDeductionDirection Direction { get; set; } = EmployeeDeductionDirection.Deduct;

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


    /// <summary>
    /// Frecuencia de aplicación.
    /// - Quincenal: aplica en cada nómina (1-15 y 16-fin).
    /// - Mensual: aplica 1 vez al mes, en la quincena indicada por ApplyOnHalf.
    /// </summary>
    public EmployeeDeductionFrequency Frequency { get; set; } = EmployeeDeductionFrequency.Biweekly;

    /// <summary>
    /// Solo para Frequency = Mensual.
    /// 1 = Primera quincena (1-15), 2 = Segunda quincena (16-fin).
    /// NULL => se asume 2.
    /// </summary>
    public int? ApplyOnHalf { get; set; }

    /// <summary>
    /// Plazo (cantidad de periodos de aplicación).
    /// - Quincenal: número de quincenas
    /// - Mensual: número de meses
    /// NULL => indefinido
    /// </summary>
    public int? TermCount { get; set; }

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
