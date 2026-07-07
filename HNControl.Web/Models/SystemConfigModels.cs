using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum PacProvider
{
    None = 0,
    Facturama = 1,
    Finkok = 2,
    SwSapien = 3
}

public class SystemConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(180)]
    public string CompanyName { get; set; } = "HN Solutions";

    [MaxLength(180)]
    public string CompanyLegalName { get; set; } = "";

    [MaxLength(13)]
    public string CompanyRfc { get; set; } = "";

    [MaxLength(4)]
    public string CompanyFiscalRegimeCode { get; set; } = "601";

    [MaxLength(10)]
    public string CompanyFiscalZipCode { get; set; } = "";

    [MaxLength(400)]
    public string CompanyFiscalAddress { get; set; } = "";

    [MaxLength(256)]
    public string BillingEmail { get; set; } = "";

    [MaxLength(500)]
    public string CompanyLogoStoragePath { get; set; } = "";

    [MaxLength(255)]
    public string CompanyLogoOriginalFileName { get; set; } = "";

    [MaxLength(120)]
    public string SmtpHost { get; set; } = "";

    public int SmtpPort { get; set; } = 587;

    [MaxLength(180)]
    public string SmtpUser { get; set; } = "";

    [MaxLength(2200)]
    public string SmtpPasswordProtected { get; set; } = "";

    [MaxLength(256)]
    public string SmtpFromEmail { get; set; } = "";

    [MaxLength(180)]
    public string SmtpFromName { get; set; } = "HN Control";

    [MaxLength(30)]
    public string SmtpSecurity { get; set; } = "StartTls";

    [MaxLength(120)]
    public string SmtpHeloDomain { get; set; } = "";

    public int SmtpTimeoutMs { get; set; } = 15000;

    public PacProvider BillingPacProvider { get; set; } = PacProvider.None;

    [MaxLength(220)]
    public string BillingPacApiBaseUrl { get; set; } = "";

    [MaxLength(220)]
    public string BillingPacApiKey { get; set; } = "";

    [MaxLength(2200)]
    public string BillingPacApiSecretProtected { get; set; } = "";

    [MaxLength(180)]
    public string BillingPacUsername { get; set; } = "";

    [MaxLength(2200)]
    public string BillingPacPasswordProtected { get; set; } = "";

    [MaxLength(10)]
    public string CfdiVersion { get; set; } = "4.0";

    [MaxLength(20)]
    public string CfdiSerieDefault { get; set; } = "A";

    [MaxLength(500)]
    public string CsdCerStoragePath { get; set; } = "";

    [MaxLength(500)]
    public string CsdKeyStoragePath { get; set; } = "";

    [MaxLength(2200)]
    public string CsdPasswordProtected { get; set; } = "";

    [MaxLength(2200)]
    public string MercadoPagoAccessTokenProtected { get; set; } = "";

    [MaxLength(220)]
    public string MercadoPagoPublicKey { get; set; } = "";

    [MaxLength(2200)]
    public string MercadoPagoWebhookSecretProtected { get; set; } = "";

    public bool WhatsAppEnabled { get; set; }

    [MaxLength(300)]
    public string WhatsAppGatewayUrl { get; set; } = "";

    [MaxLength(2200)]
    public string WhatsAppApiKeyProtected { get; set; } = "";

    [MaxLength(1000)]
    public string WhatsAppInternalPhonesCsv { get; set; } = "";

    public bool WhatsAppNotifyTickets { get; set; } = true;

    public bool WhatsAppNotifyCustomers { get; set; }

    [MaxLength(2000)]
    public string WhatsAppOtpTemplate { get; set; } = "";

    [MaxLength(2000)]
    public string WhatsAppPayrollReceiptTemplate { get; set; } = "";

    // ── Meta WhatsApp Cloud API ───────────────────────────────────────────
    // Nota: se reutiliza WhatsAppGatewayUrl como Phone Number ID y
    // WhatsAppApiKeyProtected como Access Token permanente (cifrado).

    /// <summary>ID de la cuenta de WhatsApp Business (WABA ID). Informativo.</summary>
    [MaxLength(64)]
    public string WhatsAppWabaId { get; set; } = "";

    /// <summary>Version del Graph API de Meta. Ej: v21.0</summary>
    [MaxLength(12)]
    public string WhatsAppGraphApiVersion { get; set; } = "v21.0";

    /// <summary>Idioma por defecto de las plantillas HSM. Ej: es_MX</summary>
    [MaxLength(12)]
    public string WhatsAppTemplateLanguage { get; set; } = "es_MX";

    /// <summary>Token de verificacion del webhook (lo eliges tu; debe coincidir en Meta).</summary>
    [MaxLength(120)]
    public string WhatsAppWebhookVerifyToken { get; set; } = "";

    // Nombres de plantillas HSM aprobadas en Meta (si van vacias, se envia texto libre).
    /// <summary>Plantilla de codigo OTP de login (categoria Authentication).</summary>
    [MaxLength(200)]
    public string WhatsAppOtpTemplateName { get; set; } = "";

    /// <summary>Plantilla de aviso de recibo de nomina (categoria Utility).</summary>
    [MaxLength(200)]
    public string WhatsAppPayrollTemplateName { get; set; } = "";

    /// <summary>Plantilla de alertas de tickets (categoria Utility).</summary>
    [MaxLength(200)]
    public string WhatsAppTicketTemplateName { get; set; } = "";

    [MaxLength(220)]
    public string PublicBaseUrl { get; set; } = "";

    [MaxLength(400)]
    public string Notes { get; set; } = "";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
