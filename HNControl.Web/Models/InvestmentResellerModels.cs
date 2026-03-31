using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum InvestmentPeriodicity
{
    [Display(Name = "Mensual")]
    Monthly = 1,

    [Display(Name = "Quincenal")]
    Biweekly = 2,

    [Display(Name = "Semanal")]
    Weekly = 3
}

public enum InvestmentInvestorType
{
    [Display(Name = "Empleado")]
    Employee = 1,

    [Display(Name = "Externo")]
    External = 2
}

public class InvestmentInvestor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public InvestmentInvestorType InvestorType { get; set; } = InvestmentInvestorType.External;

    [MaxLength(64)]
    public string? EmployeeUserId { get; set; }
    public EmployeeProfile? Employee { get; set; }

    [MaxLength(200)]
    public string FullName { get; set; } = "";

    [MaxLength(256)]
    public string Email { get; set; } = "";

    [MaxLength(40)]
    public string Phone { get; set; } = "";

    [MaxLength(1200)]
    public string Notes { get; set; } = "";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<InvestmentPlan> Plans { get; set; } = new();
}

public class InvestmentPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvestorId { get; set; }
    public InvestmentInvestor? Investor { get; set; }
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = "";

    public decimal PrincipalAmount { get; set; }
    public decimal ProfitPercent { get; set; } // 0.15 = 15%
    public int PaymentCount { get; set; }
    public InvestmentPeriodicity Periodicity { get; set; } = InvestmentPeriodicity.Monthly;
    public DateTime StartDate { get; set; } = DateTime.Today;

    [MaxLength(1200)]
    public string Notes { get; set; } = "";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<InvestmentPayment> Payments { get; set; } = new();
}

public class InvestmentPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanId { get; set; }
    public InvestmentPlan? Plan { get; set; }

    public int PeriodNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal PrincipalPortion { get; set; }
    public decimal ProfitPortion { get; set; }
    public decimal TotalAmount { get; set; }

    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }

    [MaxLength(200)]
    public string PaymentReference { get; set; } = "";

    public DateTime? StatementSentAt { get; set; }
}

public enum ResellerPartyType
{
    [Display(Name = "Empleado")]
    Employee = 1,

    [Display(Name = "Externo")]
    External = 2
}

public enum ResellerSourceType
{
    [Display(Name = "Orden de servicio")]
    ServiceOrder = 1,

    [Display(Name = "Cotizacion")]
    Quote = 2
}

public enum ResellerCommissionPeriodicity
{
    [Display(Name = "Unica")]
    OneTime = 1,

    [Display(Name = "Semanal")]
    Weekly = 2,

    [Display(Name = "Quincenal")]
    Biweekly = 3,

    [Display(Name = "Mensual")]
    Monthly = 4
}

public class ResellerPartner
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ResellerPartyType PartyType { get; set; } = ResellerPartyType.External;

    [MaxLength(64)]
    public string? EmployeeUserId { get; set; }
    public EmployeeProfile? Employee { get; set; }

    [MaxLength(200)]
    public string FullName { get; set; } = "";

    [MaxLength(256)]
    public string Email { get; set; } = "";

    [MaxLength(40)]
    public string Phone { get; set; } = "";

    [MaxLength(1200)]
    public string Notes { get; set; } = "";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ResellerCommissionPlan> CommissionPlans { get; set; } = new();
}

public class ResellerCommissionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartnerId { get; set; }
    public ResellerPartner? Partner { get; set; }
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    public ResellerSourceType SourceType { get; set; }
    public Guid? ServiceOrderId { get; set; }
    public ServiceOrder? ServiceOrder { get; set; }
    public Guid? QuoteRequestId { get; set; }
    public QuoteRequest? QuoteRequest { get; set; }

    [MaxLength(220)]
    public string Description { get; set; } = "";

    public decimal BaseAmount { get; set; }
    public decimal CommissionPercent { get; set; } // 0.10 = 10%
    public decimal CommissionAmount { get; set; }

    public int PeriodCount { get; set; } = 1;
    public ResellerCommissionPeriodicity Periodicity { get; set; } = ResellerCommissionPeriodicity.OneTime;
    public DateTime StartDate { get; set; } = DateTime.Today;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ResellerCommissionPayment> Payments { get; set; } = new();
}

public class ResellerCommissionPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanId { get; set; }
    public ResellerCommissionPlan? Plan { get; set; }

    public int PeriodNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }

    [MaxLength(220)]
    public string PaymentReference { get; set; } = "";
}
