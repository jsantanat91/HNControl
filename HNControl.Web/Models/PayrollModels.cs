using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public class PayrollReceiptDispatch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string UserId { get; set; } = "";

    [MaxLength(256)]
    public string RecipientEmail { get; set; } = "";

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime PayrollDate { get; set; }

    public bool IsSent { get; set; } = false;
    public int AttemptCount { get; set; } = 0;
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }

    [MaxLength(1200)]
    public string? LastError { get; set; }
}

