using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum LevantamientoSurveyStatus
{
    [Display(Name = "Nueva")] New = 1,
    [Display(Name = "En revisión")] InReview = 2,
    [Display(Name = "Viable")] Feasible = 3,
    [Display(Name = "No viable")] NotFeasible = 4,
    [Display(Name = "Cotizada")] Quoted = 5
}

/// <summary>Technical survey received from the public questionnaire (Factibilidades module, 2.6.0).</summary>
public class LevantamientoSurvey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Questionnaire file name; makes resubmissions idempotent.</summary>
    [MaxLength(120)] public string ExternalId { get; set; } = "";
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    [MaxLength(20)] public string ClientCodeSent { get; set; } = "";
    [MaxLength(13)] public string Rfc { get; set; } = "";
    [MaxLength(200)] public string Company { get; set; } = "";
    [MaxLength(160)] public string ContactName { get; set; } = "";
    [MaxLength(120)] public string ContactRole { get; set; } = "";
    [MaxLength(256)] public string Email { get; set; } = "";
    [MaxLength(60)] public string Phone { get; set; } = "";
    [MaxLength(200)] public string City { get; set; } = "";
    [MaxLength(400)] public string Address { get; set; } = "";
    [MaxLength(180)] public string BusinessLine { get; set; } = "";
    [MaxLength(400)] public string Services { get; set; } = "";
    [MaxLength(120)] public string Urgency { get; set; } = "";
    [MaxLength(120)] public string Budget { get; set; } = "";
    [MaxLength(120)] public string Executive { get; set; } = "";
    [MaxLength(120)] public string Visit { get; set; } = "";

    /// <summary>[{titulo, items:[{h}|{k,v}]}] exactly as shown to the customer.</summary>
    public string SectionsJson { get; set; } = "[]";
    [MaxLength(200)] public string PdfFileName { get; set; } = "";
    public byte[]? PdfBytes { get; set; }
    /// <summary>Optional multi-site file (dedicated internet): address and MB per site.</summary>
    [MaxLength(200)] public string SitesFileName { get; set; } = "";
    public byte[]? SitesBytes { get; set; }

    public LevantamientoSurveyStatus Status { get; set; } = LevantamientoSurveyStatus.New;
    [MaxLength(4000)] public string InternalNotes { get; set; } = "";
    // Dictamen de viabilidad (2.6.3): se emite antes de la orden; "No viable" detiene el flujo.
    /// <summary>[{k, r}] with r = "si" | "no" | "na" for each <see cref="LevantamientoReview.Checks"/> key.</summary>
    public string ReviewChecksJson { get; set; } = "[]";
    /// <summary>Comments sent to the customer with the decision (reasons, conditions, next steps).</summary>
    [MaxLength(4000)] public string DecisionComments { get; set; } = "";
    public DateTime? DecidedAt { get; set; }
    [MaxLength(64)] public string? DecidedByUserId { get; set; }
    [MaxLength(256)] public string DecisionEmailTo { get; set; } = "";
    public DateTime? DecisionEmailSentAt { get; set; }

    public Guid? ServiceOrderId { get; set; }
    public ServiceOrder? ServiceOrder { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(64)] public string? UpdatedByUserId { get; set; }
}
