using HNControl.Web.Models;

namespace HNControl.Web.Services;

public record PayrollImssLine(
    string Concept,
    decimal EmployerRate,
    decimal EmployeeRate,
    decimal EmployerPeriodAmount,
    decimal EmployeePeriodAmount);
public record PayrollAdjustmentLine(string Concept, string Kind, decimal Amount);

public class PayrollReceiptData
{
    public string UserId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Position { get; set; } = "";
    public string EmployeeNumber { get; set; } = "";
    public string Nss { get; set; } = "";
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime PayrollDate { get; set; }

    public decimal SalaryBaseMonthly { get; set; }
    public decimal BaseQuincenal { get; set; }
    public decimal Fixed80 { get; set; }
    public decimal Max20 { get; set; }
    public decimal VariablePercent { get; set; }
    public decimal VariableAmount { get; set; }
    public decimal GrossEstimated { get; set; }

    public decimal Deductions { get; set; }
    public decimal Bonuses { get; set; }
    public decimal NetEstimated { get; set; }

    public List<PayrollAdjustmentLine> AppliedAdjustments { get; set; } = new();
    public List<PayrollImssLine> ImssLines { get; set; } = new();
    public decimal ImssEmployerPeriodTotal => ImssLines.Sum(x => x.EmployerPeriodAmount);
    public decimal ImssEmployeePeriodTotal => ImssLines.Sum(x => x.EmployeePeriodAmount);
    public decimal ImssPeriodTotal => ImssEmployerPeriodTotal + ImssEmployeePeriodTotal;
}

public interface IPayrollReceiptService
{
    Task<PayrollReceiptData?> BuildAsync(string userId, DateTime periodStart, DateTime periodEnd, DateTime payrollDate);
    byte[] RenderPdf(PayrollReceiptData data);
}
