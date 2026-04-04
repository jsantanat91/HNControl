using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum BillingInvoiceType
{
    Ingreso = 1,
    Egreso = 2,
    Traslado = 3,
    Nomina = 4,
    Pago = 5
}

public enum BillingPeriodicity
{
    OneTime = 1,
    Weekly = 2,
    Biweekly = 3,
    Monthly = 4,
    Bimonthly = 5,
    Quarterly = 6,
    Semiannual = 7,
    Annual = 8
}

public enum BillingPlanStatus
{
    Draft = 1,
    Active = 2,
    Paused = 3,
    Completed = 4
}

public enum BillingRunStatus
{
    Scheduled = 1,
    Sent = 2,
    Cancelled = 3
}

public enum BillingCfdiStatus
{
    Pending = 1,
    Vigente = 2,
    CancelPending = 3,
    Cancelled = 4,
    Unknown = 99
}

public class BillingInvoicePlan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid? QuoteRequestId { get; set; }
    public QuoteRequest? QuoteRequest { get; set; }

    public Guid? SalesOpportunityId { get; set; }
    public SalesOpportunity? SalesOpportunity { get; set; }

    [MaxLength(220)]
    public string Concept { get; set; } = "";

    [MaxLength(10)]
    public string Currency { get; set; } = "MXN";

    public decimal Subtotal { get; set; }
    public decimal VatRate { get; set; } = 0.16m;
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }

    public BillingInvoiceType InvoiceType { get; set; } = BillingInvoiceType.Ingreso;

    [MaxLength(4)]
    public string CfdiUseCode { get; set; } = "G03";

    [MaxLength(4)]
    public string FiscalRegimeCode { get; set; } = "601";

    [MaxLength(4)]
    public string PaymentMethodCode { get; set; } = "PUE";

    [MaxLength(4)]
    public string PaymentFormCode { get; set; } = "03";

    public BillingPeriodicity Periodicity { get; set; } = BillingPeriodicity.Monthly;

    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime NextRunDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; }
    public int? RemainingRuns { get; set; }

    [MaxLength(256)]
    public string SendToEmail { get; set; } = "";

    [MaxLength(600)]
    public string CcEmails { get; set; } = "";

    [MaxLength(2000)]
    public string Notes { get; set; } = "";

    public BillingPlanStatus Status { get; set; } = BillingPlanStatus.Active;
    public DateTime? LastSentAt { get; set; }

    [MaxLength(64)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<BillingInvoiceRun> Runs { get; set; } = new();
}

public class BillingInvoiceRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlanId { get; set; }
    public BillingInvoicePlan? Plan { get; set; }

    [MaxLength(60)]
    public string PeriodLabel { get; set; } = "";

    public DateTime ScheduledFor { get; set; } = DateTime.UtcNow.Date;
    public BillingRunStatus Status { get; set; } = BillingRunStatus.Scheduled;

    [MaxLength(256)]
    public string SentToEmail { get; set; } = "";

    public DateTime? SentAt { get; set; }

    [MaxLength(500)]
    public string? PdfStoragePath { get; set; }

    [MaxLength(1200)]
    public string? ErrorMessage { get; set; }

    [MaxLength(60)]
    public string CfdiUuid { get; set; } = "";

    public BillingCfdiStatus CfdiStatus { get; set; } = BillingCfdiStatus.Pending;

    [MaxLength(40)]
    public string CancelReasonCode { get; set; } = "";

    [MaxLength(1200)]
    public string SatStatusMessage { get; set; } = "";

    [MaxLength(120)]
    public string PacTrackingId { get; set; } = "";

    public DateTime? LastSyncAt { get; set; }
    public DateTime? CancellationRequestedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class SatCatalogs
{
    public static readonly (string Code, string Label)[] TipoComprobante =
    [
        ("I", "Ingreso"),
        ("E", "Egreso"),
        ("T", "Traslado"),
        ("N", "Nomina"),
        ("P", "Pago")
    ];

    public static readonly (string Code, string Label)[] UsoCfdi =
    [
        ("G01", "Adquisicion de mercancias"),
        ("G02", "Devoluciones, descuentos o bonificaciones"),
        ("G03", "Gastos en general"),
        ("I01", "Construcciones"),
        ("I08", "Otra maquinaria y equipo"),
        ("S01", "Sin efectos fiscales")
    ];

    public static readonly (string Code, string Label)[] RegimenFiscal =
    [
        ("601", "General de Ley Personas Morales"),
        ("603", "Personas Morales con Fines no Lucrativos"),
        ("605", "Sueldos y Salarios"),
        ("612", "Personas Fisicas con Actividades Empresariales"),
        ("626", "RESICO"),
        ("616", "Sin obligaciones fiscales")
    ];

    public static readonly (string Code, string Label)[] MetodoPago =
    [
        ("PUE", "Pago en una sola exhibicion"),
        ("PPD", "Pago en parcialidades o diferido")
    ];

    public static readonly (string Code, string Label)[] FormaPago =
    [
        ("01", "Efectivo"),
        ("02", "Cheque nominativo"),
        ("03", "Transferencia electronica"),
        ("04", "Tarjeta de credito"),
        ("28", "Tarjeta de debito"),
        ("99", "Por definir")
    ];
}
