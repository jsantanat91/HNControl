using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum SalesOpportunityStatus
{
    Prospect = 1,
    ClosedWon = 2,
    ClosedLost = 3,
    ContractSigned = 4,
    CommissionApplied = 5
}

public enum SalesWorkflowStage
{
    Lead = 1,
    Quotation = 2,
    Closing = 3,
    Contract = 4,
    Signature = 5,
    Billing = 6,
    Commission = 7,
    ClosedWon = 8,
    ClosedLost = 9
}

public class SalesSellerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string EmployeeUserId { get; set; } = "";
    public EmployeeProfile? Employee { get; set; }

    public decimal DefaultCommissionPercent { get; set; } = 0.05m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SalesOpportunity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid QuoteRequestId { get; set; }
    public QuoteRequest? QuoteRequest { get; set; }

    public Guid? SellerProfileId { get; set; }
    public SalesSellerProfile? SellerProfile { get; set; }

    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    public SalesOpportunityStatus Status { get; set; } = SalesOpportunityStatus.Prospect;
    public decimal CommissionPercent { get; set; } = 0.05m;
    public decimal CommissionAmount { get; set; }

    [MaxLength(1200)]
    public string Notes { get; set; } = "";

    public DateTime? ClosedAt { get; set; }
    public DateTime? ContractSignedAt { get; set; }

    public Guid? BonusDeductionId { get; set; }

    public SalesWorkflowStage WorkflowStage { get; set; } = SalesWorkflowStage.Lead;
    public DateTime StageChangedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StageDueAt { get; set; }

    [MaxLength(64)]
    public string? OwnerUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum SalesCallResult
{
    Initiated = 1,
    Connected = 2,
    Completed = 3,
    Failed = 4,
    Canceled = 5
}

public class SalesSipAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string UserId { get; set; } = "";
    public EmployeeProfile? Employee { get; set; }

    [MaxLength(220)]
    public string Host { get; set; } = "";

    [MaxLength(180)]
    public string SipUser { get; set; } = "";

    [MaxLength(2000)]
    public string SipPasswordProtected { get; set; } = "";

    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SalesCallLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string UserId { get; set; } = "";
    public EmployeeProfile? Employee { get; set; }

    public Guid? SalesOpportunityId { get; set; }
    public SalesOpportunity? SalesOpportunity { get; set; }

    [MaxLength(60)]
    public string DialedNumber { get; set; } = "";

    public SalesCallResult Result { get; set; } = SalesCallResult.Initiated;
    public int DurationSeconds { get; set; }

    [MaxLength(2000)]
    public string Notes { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
