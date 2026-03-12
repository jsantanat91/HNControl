using HNControl.Web.Models;

namespace HNControl.Web.Services;

public record PayrollImssLine(string Concept, decimal MonthlyAmount, decimal PeriodAmount);
public record PayrollAdjustmentLine(string Concept, string Kind, decimal Amount);

public class PayrollReceiptData
{
    public string UserId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Position { get; set; } = "";
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
    public decimal ImssMonthlyTotal => ImssLines.Sum(x => x.MonthlyAmount);
    public decimal ImssPeriodTotal => ImssLines.Sum(x => x.PeriodAmount);
}

public interface IPayrollReceiptService
{
    Task<PayrollReceiptData?> BuildAsync(string userId, DateTime periodStart, DateTime periodEnd, DateTime payrollDate);
    byte[] RenderPdf(PayrollReceiptData data);
}

