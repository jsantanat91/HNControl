using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HNControl.Web.Pages.Admin.SystemPages;

[Authorize(Roles = AppRoles.Admin)]
public class ConfigurationModel : PageModel
{
    public const string DefaultChildCheckInTemplate =
        "Hola {TutorNombre}, registramos el check-in de {NinoNombre} el {Fecha} a las {Hora}.";

    public const string DefaultChildCheckOutTemplate =
        "Hola {TutorNombre}, registramos el check-out de {NinoNombre} el {Fecha} a las {Hora}.";

    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly ISecretProtector _protector;
    private readonly IWhatsAppSender _whatsApp;

    public ConfigurationModel(ApplicationDbContext db, IFileStorage storage, ISecretProtector protector, IWhatsAppSender whatsApp)
    {
        _db = db;
        _storage = storage;
        _protector = protector;
        _whatsApp = whatsApp;
    }

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }
    [TempData] public string? FiscalValidation { get; set; }
    [TempData] public string? FiscalValidationType { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public SelectList PacProviders => new(Enum.GetValues<PacProvider>().Select(x => new { Value = x, Text = PacLabel(x) }), "Value", "Text");
    public bool HasLogo { get; set; }
    public bool HasSmtpPassword { get; set; }
    public bool HasPacSecret { get; set; }
    public bool HasPacPassword { get; set; }
    public bool HasCsdPassword { get; set; }
    public bool HasMercadoPagoToken { get; set; }
    public bool HasMercadoPagoWebhookSecret { get; set; }
    public bool HasWhatsAppApiKey { get; set; }
    public string LogoName { get; set; } = "";

    public class InputModel
    {
        public Guid? Id { get; set; }

        [MaxLength(180)] public string CompanyName { get; set; } = "";
        [MaxLength(180)] public string CompanyLegalName { get; set; } = "";
        [MaxLength(13)] public string CompanyRfc { get; set; } = "";
        [MaxLength(4)] public string CompanyFiscalRegimeCode { get; set; } = "601";
        [MaxLength(10)] public string CompanyFiscalZipCode { get; set; } = "";
        [MaxLength(400)] public string CompanyFiscalAddress { get; set; } = "";
        [MaxLength(256)] public string BillingEmail { get; set; } = "";

        [MaxLength(120)] public string SmtpHost { get; set; } = "";
        [Range(1, 65535)] public int SmtpPort { get; set; } = 587;
        [MaxLength(180)] public string SmtpUser { get; set; } = "";
        [MaxLength(200)] public string SmtpPassword { get; set; } = "";
        [MaxLength(256)] public string SmtpFromEmail { get; set; } = "";
        [MaxLength(180)] public string SmtpFromName { get; set; } = "HN Control";
        [MaxLength(30)] public string SmtpSecurity { get; set; } = "StartTls";
        [MaxLength(120)] public string SmtpHeloDomain { get; set; } = "";
        [Range(1000, 120000)] public int SmtpTimeoutMs { get; set; } = 15000;

        public PacProvider BillingPacProvider { get; set; } = PacProvider.None;
        [MaxLength(220)] public string BillingPacApiBaseUrl { get; set; } = "";
        [MaxLength(220)] public string BillingPacApiKey { get; set; } = "";
        [MaxLength(200)] public string BillingPacApiSecret { get; set; } = "";
        [MaxLength(180)] public string BillingPacUsername { get; set; } = "";
        [MaxLength(200)] public string BillingPacPassword { get; set; } = "";
        [MaxLength(10)] public string CfdiVersion { get; set; } = "4.0";
        [MaxLength(20)] public string CfdiSerieDefault { get; set; } = "A";
        [MaxLength(200)] public string CsdPassword { get; set; } = "";
        [MaxLength(220)] public string PublicBaseUrl { get; set; } = "";
        [MaxLength(220)] public string MercadoPagoPublicKey { get; set; } = "";
        [MaxLength(220)] public string MercadoPagoAccessToken { get; set; } = "";
        [MaxLength(220)] public string MercadoPagoWebhookSecret { get; set; } = "";
        public bool WhatsAppEnabled { get; set; }
        [MaxLength(300)] public string WhatsAppGatewayUrl { get; set; } = "";
        [MaxLength(2200)] public string WhatsAppApiKey { get; set; } = "";
        [MaxLength(1000)] public string WhatsAppInternalPhonesCsv { get; set; } = "";
        public bool WhatsAppNotifyTickets { get; set; } = true;
        public bool WhatsAppNotifyCustomers { get; set; }
        [MaxLength(40)] public string WhatsAppTestPhone { get; set; } = "";
        [MaxLength(2000)] public string WhatsAppChildCheckInTemplate { get; set; } = DefaultChildCheckInTemplate;
        [MaxLength(2000)] public string WhatsAppChildCheckOutTemplate { get; set; } = DefaultChildCheckOutTemplate;

        [MaxLength(400)] public string Notes { get; set; } = "";
        public IFormFile? CompanyLogo { get; set; }
        public IFormFile? CsdCerFile { get; set; }
        public IFormFile? CsdKeyFile { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        return await OnPostSaveGeneralAsync();
    }

    public async Task<IActionResult> OnPostSaveGeneralAsync()
    {
        SystemConfiguration entity;
        try
        {
            entity = await GetOrCreateAsync();
        }
        catch (InvalidOperationException ex)
        {
            Flash = ex.Message;
            FlashType = "warning";
            return RedirectToPage();
        }

        entity.CompanyName = (Input.CompanyName ?? "").Trim();
        entity.CompanyLegalName = (Input.CompanyLegalName ?? "").Trim();
        entity.BillingEmail = (Input.BillingEmail ?? "").Trim();

        if (Input.CompanyLogo is { Length: > 0 })
        {
            if (!string.IsNullOrWhiteSpace(entity.CompanyLogoStoragePath))
                await _storage.DeleteIfExistsAsync(entity.CompanyLogoStoragePath);

            var saved = await _storage.SaveFileAsync(
                Input.CompanyLogo,
                "branding",
                $"company_logo_{DateTime.UtcNow:yyyyMMddHHmmss}",
                new[] { ".png", ".jpg", ".jpeg", ".webp" },
                8 * 1024 * 1024);

            entity.CompanyLogoStoragePath = saved.storagePath;
            entity.CompanyLogoOriginalFileName = saved.originalName;
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Flash = "Configuracion general guardada.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveSmtpAsync()
    {
        SystemConfiguration entity;
        try
        {
            entity = await GetOrCreateAsync();
        }
        catch (InvalidOperationException ex)
        {
            Flash = ex.Message;
            FlashType = "warning";
            return RedirectToPage();
        }

        entity.SmtpHost = (Input.SmtpHost ?? "").Trim();
        entity.SmtpPort = Input.SmtpPort is > 0 and <= 65535 ? Input.SmtpPort : 587;
        entity.SmtpUser = (Input.SmtpUser ?? "").Trim();
        entity.SmtpFromEmail = (Input.SmtpFromEmail ?? "").Trim();
        entity.SmtpFromName = string.IsNullOrWhiteSpace(Input.SmtpFromName) ? "HN Control" : Input.SmtpFromName.Trim();
        entity.SmtpSecurity = string.IsNullOrWhiteSpace(Input.SmtpSecurity) ? "StartTls" : Input.SmtpSecurity.Trim();
        entity.SmtpHeloDomain = (Input.SmtpHeloDomain ?? "").Trim();
        entity.SmtpTimeoutMs = Input.SmtpTimeoutMs is >= 1000 and <= 120000 ? Input.SmtpTimeoutMs : 15000;

        if (!string.IsNullOrWhiteSpace(Input.SmtpPassword))
            entity.SmtpPasswordProtected = _protector.Protect(Input.SmtpPassword.Trim());

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Flash = "Configuracion SMTP guardada.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostValidateSmtpAsync()
    {
        var cfg = await GetLatestConfigSafeAsync();

        var host = string.IsNullOrWhiteSpace(Input.SmtpHost) ? (cfg?.SmtpHost ?? "").Trim() : Input.SmtpHost.Trim();
        var port = Input.SmtpPort > 0 ? Input.SmtpPort : (cfg?.SmtpPort > 0 ? cfg.SmtpPort : 587);
        var user = string.IsNullOrWhiteSpace(Input.SmtpUser) ? (cfg?.SmtpUser ?? "").Trim() : Input.SmtpUser.Trim();
        var pass = !string.IsNullOrWhiteSpace(Input.SmtpPassword)
            ? Input.SmtpPassword.Trim()
            : (!string.IsNullOrWhiteSpace(cfg?.SmtpPasswordProtected) ? _protector.Unprotect(cfg!.SmtpPasswordProtected) : "");
        var fromEmail = string.IsNullOrWhiteSpace(Input.SmtpFromEmail) ? (cfg?.SmtpFromEmail ?? "").Trim() : Input.SmtpFromEmail.Trim();
        var security = string.IsNullOrWhiteSpace(Input.SmtpSecurity) ? (cfg?.SmtpSecurity ?? "StartTls") : Input.SmtpSecurity.Trim();
        var timeoutMs = Input.SmtpTimeoutMs > 0 ? Input.SmtpTimeoutMs : (cfg?.SmtpTimeoutMs > 0 ? cfg.SmtpTimeoutMs : 15000);

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
        {
            Flash = "Para validar SMTP completa Servidor SMTP y Correo remitente.";
            FlashType = "warning";
            return RedirectToPage();
        }

        try
        {
            using var client = new SmtpClient();
            client.Timeout = timeoutMs;
            await client.ConnectAsync(host, port, ParseSecurity(security));
            if (!string.IsNullOrWhiteSpace(user))
                await client.AuthenticateAsync(user, pass);
            await client.DisconnectAsync(true);

            Flash = "Conexion SMTP valida. El servidor respondio correctamente.";
            FlashType = "success";
        }
        catch (Exception ex)
        {
            Flash = $"No se pudo validar SMTP: {ex.Message}";
            FlashType = "danger";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveFiscalAsync()
    {
        SystemConfiguration entity;
        try
        {
            entity = await GetOrCreateAsync();
        }
        catch (InvalidOperationException ex)
        {
            Flash = ex.Message;
            FlashType = "warning";
            return RedirectToPage();
        }

        entity.CompanyRfc = (Input.CompanyRfc ?? "").Trim().ToUpperInvariant();
        entity.CompanyFiscalRegimeCode = (Input.CompanyFiscalRegimeCode ?? "").Trim().ToUpperInvariant();
        entity.CompanyFiscalZipCode = (Input.CompanyFiscalZipCode ?? "").Trim();
        entity.CompanyFiscalAddress = (Input.CompanyFiscalAddress ?? "").Trim();
        entity.BillingPacProvider = Input.BillingPacProvider;
        entity.BillingPacApiBaseUrl = (Input.BillingPacApiBaseUrl ?? "").Trim();
        entity.BillingPacApiKey = (Input.BillingPacApiKey ?? "").Trim();
        entity.BillingPacUsername = (Input.BillingPacUsername ?? "").Trim();
        entity.CfdiVersion = string.IsNullOrWhiteSpace(Input.CfdiVersion) ? "4.0" : Input.CfdiVersion.Trim();
        entity.CfdiSerieDefault = string.IsNullOrWhiteSpace(Input.CfdiSerieDefault) ? "A" : Input.CfdiSerieDefault.Trim().ToUpperInvariant();
        entity.Notes = (Input.Notes ?? "").Trim();

        if (!string.IsNullOrWhiteSpace(Input.BillingPacApiSecret))
            entity.BillingPacApiSecretProtected = _protector.Protect(Input.BillingPacApiSecret.Trim());

        if (!string.IsNullOrWhiteSpace(Input.BillingPacPassword))
            entity.BillingPacPasswordProtected = _protector.Protect(Input.BillingPacPassword.Trim());

        if (!string.IsNullOrWhiteSpace(Input.CsdPassword))
            entity.CsdPasswordProtected = _protector.Protect(Input.CsdPassword.Trim());

        if (Input.CsdCerFile is { Length: > 0 })
        {
            if (!string.IsNullOrWhiteSpace(entity.CsdCerStoragePath))
                await _storage.DeleteIfExistsAsync(entity.CsdCerStoragePath);

            var savedCer = await _storage.SaveFileAsync(
                Input.CsdCerFile,
                "fiscal",
                $"csd_{DateTime.UtcNow:yyyyMMddHHmmss}",
                new[] { ".cer" },
                8 * 1024 * 1024);

            entity.CsdCerStoragePath = savedCer.storagePath;
        }

        if (Input.CsdKeyFile is { Length: > 0 })
        {
            if (!string.IsNullOrWhiteSpace(entity.CsdKeyStoragePath))
                await _storage.DeleteIfExistsAsync(entity.CsdKeyStoragePath);

            var savedKey = await _storage.SaveFileAsync(
                Input.CsdKeyFile,
                "fiscal",
                $"csd_{DateTime.UtcNow:yyyyMMddHHmmss}",
                new[] { ".key" },
                8 * 1024 * 1024);

            entity.CsdKeyStoragePath = savedKey.storagePath;
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Flash = "Configuracion fiscal / PAC guardada.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveApiAsync()
    {
        SystemConfiguration entity;
        try
        {
            entity = await GetOrCreateAsync();
        }
        catch (InvalidOperationException ex)
        {
            Flash = ex.Message;
            FlashType = "warning";
            return RedirectToPage();
        }

        entity.PublicBaseUrl = (Input.PublicBaseUrl ?? "").Trim();
        entity.MercadoPagoPublicKey = (Input.MercadoPagoPublicKey ?? "").Trim();

        if (!string.IsNullOrWhiteSpace(Input.MercadoPagoAccessToken))
            entity.MercadoPagoAccessTokenProtected = _protector.Protect(Input.MercadoPagoAccessToken.Trim());

        if (!string.IsNullOrWhiteSpace(Input.MercadoPagoWebhookSecret))
            entity.MercadoPagoWebhookSecretProtected = _protector.Protect(Input.MercadoPagoWebhookSecret.Trim());

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Flash = "Configuracion API guardada.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveWhatsAppAsync()
    {
        SystemConfiguration entity;
        try
        {
            entity = await GetOrCreateAsync();
        }
        catch (InvalidOperationException ex)
        {
            Flash = ex.Message;
            FlashType = "warning";
            return RedirectToPage();
        }

        entity.WhatsAppEnabled = Input.WhatsAppEnabled;
        entity.WhatsAppGatewayUrl = (Input.WhatsAppGatewayUrl ?? "").Trim();
        entity.WhatsAppInternalPhonesCsv = (Input.WhatsAppInternalPhonesCsv ?? "").Trim();
        entity.WhatsAppNotifyTickets = Input.WhatsAppNotifyTickets;
        entity.WhatsAppNotifyCustomers = Input.WhatsAppNotifyCustomers;
        entity.WhatsAppChildCheckInTemplate = NormalizeTemplate(Input.WhatsAppChildCheckInTemplate, DefaultChildCheckInTemplate);
        entity.WhatsAppChildCheckOutTemplate = NormalizeTemplate(Input.WhatsAppChildCheckOutTemplate, DefaultChildCheckOutTemplate);

        if (!string.IsNullOrWhiteSpace(Input.WhatsAppApiKey))
            entity.WhatsAppApiKeyProtected = _protector.Protect(Input.WhatsAppApiKey.Trim());

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Flash = "Configuracion WhatsApp guardada.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTestWhatsAppAsync()
    {
        var phone = (Input.WhatsAppTestPhone ?? "").Trim();
        if (string.IsNullOrWhiteSpace(phone))
        {
            Flash = "Ingresa un telefono de prueba para WhatsApp.";
            FlashType = "warning";
            return RedirectToPage();
        }

        try
        {
            await _whatsApp.SendAsync(phone, "Prueba de WhatsApp desde HN Control. Configuracion OpenWA activa.");
            Flash = $"WhatsApp enviado a {phone}.";
            FlashType = "success";
        }
        catch (Exception ex)
        {
            Flash = $"No se pudo enviar WhatsApp: {ex.Message}";
            FlashType = "danger";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostValidateFiscalAsync()
    {
        var cfg = await GetLatestConfigSafeAsync();

        if (cfg == null)
        {
            FiscalValidation = "Primero guarda la configuracion fiscal y PAC.";
            FiscalValidationType = "warning";
            return RedirectToPage();
        }

        var missing = new List<string>();
        if (cfg.BillingPacProvider == PacProvider.None) missing.Add("Proveedor PAC");
        if (string.IsNullOrWhiteSpace(cfg.BillingPacApiBaseUrl)) missing.Add("API base URL");
        if (string.IsNullOrWhiteSpace(cfg.BillingPacApiKey)) missing.Add("API key");
        if (string.IsNullOrWhiteSpace(cfg.BillingPacApiSecretProtected)) missing.Add("API secret");
        if (string.IsNullOrWhiteSpace(cfg.BillingPacUsername)) missing.Add("Usuario PAC");
        if (string.IsNullOrWhiteSpace(cfg.BillingPacPasswordProtected)) missing.Add("Password PAC");
        if (string.IsNullOrWhiteSpace(cfg.CsdCerStoragePath)) missing.Add("Archivo CSD .cer");
        if (string.IsNullOrWhiteSpace(cfg.CsdKeyStoragePath)) missing.Add("Archivo CSD .key");
        if (string.IsNullOrWhiteSpace(cfg.CsdPasswordProtected)) missing.Add("Password CSD");
        if (string.IsNullOrWhiteSpace(cfg.CompanyRfc)) missing.Add("RFC emisor");
        if (string.IsNullOrWhiteSpace(cfg.CompanyFiscalRegimeCode)) missing.Add("Regimen fiscal");
        if (string.IsNullOrWhiteSpace(cfg.CompanyFiscalZipCode)) missing.Add("CP fiscal");

        if (missing.Count == 0)
        {
            FiscalValidation = "Configuracion fiscal validada: listo para timbrado oficial, sincronizacion SAT en vivo y cancelacion CFDI.";
            FiscalValidationType = "success";
        }
        else
        {
            FiscalValidation = "Faltan datos para operacion fiscal SAT/PAC: " + string.Join(", ", missing) + ".";
            FiscalValidationType = "warning";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetDownloadLogoAsync()
    {
        var cfg = await GetLatestConfigSafeAsync();
        if (cfg == null || string.IsNullOrWhiteSpace(cfg.CompanyLogoStoragePath))
            return NotFound();

        var (stream, contentType, downloadName) = await _storage.OpenAsync(
            cfg.CompanyLogoStoragePath,
            string.IsNullOrWhiteSpace(cfg.CompanyLogoOriginalFileName) ? "logo.png" : cfg.CompanyLogoOriginalFileName);
        return File(stream, contentType, downloadName);
    }

    private async Task LoadAsync()
    {
        var cfg = await GetLatestConfigSafeAsync();

        if (cfg == null)
        {
            Input = new InputModel
            {
                CompanyName = "HN Solutions",
                CompanyFiscalRegimeCode = "601",
                CfdiVersion = "4.0",
                CfdiSerieDefault = "A",
                SmtpPort = 587,
                SmtpSecurity = "StartTls",
                SmtpTimeoutMs = 15000,
                PublicBaseUrl = "",
                WhatsAppNotifyTickets = true,
                WhatsAppChildCheckInTemplate = DefaultChildCheckInTemplate,
                WhatsAppChildCheckOutTemplate = DefaultChildCheckOutTemplate
            };
            HasLogo = false;
            return;
        }

        Input = new InputModel
        {
            Id = cfg.Id,
            CompanyName = cfg.CompanyName,
            CompanyLegalName = cfg.CompanyLegalName,
            CompanyRfc = cfg.CompanyRfc,
            CompanyFiscalRegimeCode = cfg.CompanyFiscalRegimeCode,
            CompanyFiscalZipCode = cfg.CompanyFiscalZipCode,
            CompanyFiscalAddress = cfg.CompanyFiscalAddress,
            BillingEmail = cfg.BillingEmail,
            SmtpHost = cfg.SmtpHost,
            SmtpPort = cfg.SmtpPort,
            SmtpUser = cfg.SmtpUser,
            SmtpFromEmail = cfg.SmtpFromEmail,
            SmtpFromName = cfg.SmtpFromName,
            SmtpSecurity = cfg.SmtpSecurity,
            SmtpHeloDomain = cfg.SmtpHeloDomain,
            SmtpTimeoutMs = cfg.SmtpTimeoutMs,
            BillingPacProvider = cfg.BillingPacProvider,
            BillingPacApiBaseUrl = cfg.BillingPacApiBaseUrl,
            BillingPacApiKey = cfg.BillingPacApiKey,
            BillingPacUsername = cfg.BillingPacUsername,
            CfdiVersion = cfg.CfdiVersion,
            CfdiSerieDefault = cfg.CfdiSerieDefault,
            PublicBaseUrl = cfg.PublicBaseUrl,
            MercadoPagoPublicKey = cfg.MercadoPagoPublicKey,
            WhatsAppEnabled = cfg.WhatsAppEnabled,
            WhatsAppGatewayUrl = cfg.WhatsAppGatewayUrl,
            WhatsAppInternalPhonesCsv = cfg.WhatsAppInternalPhonesCsv,
            WhatsAppNotifyTickets = cfg.WhatsAppNotifyTickets,
            WhatsAppNotifyCustomers = cfg.WhatsAppNotifyCustomers,
            WhatsAppChildCheckInTemplate = NormalizeTemplate(cfg.WhatsAppChildCheckInTemplate, DefaultChildCheckInTemplate),
            WhatsAppChildCheckOutTemplate = NormalizeTemplate(cfg.WhatsAppChildCheckOutTemplate, DefaultChildCheckOutTemplate),
            Notes = cfg.Notes
        };

        HasLogo = !string.IsNullOrWhiteSpace(cfg.CompanyLogoStoragePath);
        LogoName = cfg.CompanyLogoOriginalFileName;
        HasSmtpPassword = !string.IsNullOrWhiteSpace(cfg.SmtpPasswordProtected);
        HasPacSecret = !string.IsNullOrWhiteSpace(cfg.BillingPacApiSecretProtected);
        HasPacPassword = !string.IsNullOrWhiteSpace(cfg.BillingPacPasswordProtected);
        HasCsdPassword = !string.IsNullOrWhiteSpace(cfg.CsdPasswordProtected);
        HasMercadoPagoToken = !string.IsNullOrWhiteSpace(cfg.MercadoPagoAccessTokenProtected);
        HasMercadoPagoWebhookSecret = !string.IsNullOrWhiteSpace(cfg.MercadoPagoWebhookSecretProtected);
        HasWhatsAppApiKey = !string.IsNullOrWhiteSpace(cfg.WhatsAppApiKeyProtected);
    }

    private async Task<SystemConfiguration?> GetLatestConfigSafeAsync()
    {
        try
        {
            return await _db.SystemConfigurations
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            Flash = "La configuracion API de Mercado Pago aun no esta en el esquema de BD. Se cargo modo compatible sin esos campos.";
            FlashType = "warning";

            return await _db.SystemConfigurations
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .Select(x => new SystemConfiguration
                {
                    Id = x.Id,
                    CompanyName = x.CompanyName,
                    CompanyLegalName = x.CompanyLegalName,
                    CompanyRfc = x.CompanyRfc,
                    CompanyFiscalRegimeCode = x.CompanyFiscalRegimeCode,
                    CompanyFiscalZipCode = x.CompanyFiscalZipCode,
                    CompanyFiscalAddress = x.CompanyFiscalAddress,
                    BillingEmail = x.BillingEmail,
                    CompanyLogoStoragePath = x.CompanyLogoStoragePath,
                    CompanyLogoOriginalFileName = x.CompanyLogoOriginalFileName,
                    SmtpHost = x.SmtpHost,
                    SmtpPort = x.SmtpPort,
                    SmtpUser = x.SmtpUser,
                    SmtpPasswordProtected = x.SmtpPasswordProtected,
                    SmtpFromEmail = x.SmtpFromEmail,
                    SmtpFromName = x.SmtpFromName,
                    SmtpSecurity = x.SmtpSecurity,
                    SmtpHeloDomain = x.SmtpHeloDomain,
                    SmtpTimeoutMs = x.SmtpTimeoutMs,
                    BillingPacProvider = x.BillingPacProvider,
                    BillingPacApiBaseUrl = x.BillingPacApiBaseUrl,
                    BillingPacApiKey = x.BillingPacApiKey,
                    BillingPacApiSecretProtected = x.BillingPacApiSecretProtected,
                    BillingPacUsername = x.BillingPacUsername,
                    BillingPacPasswordProtected = x.BillingPacPasswordProtected,
                    CfdiVersion = x.CfdiVersion,
                    CfdiSerieDefault = x.CfdiSerieDefault,
                    CsdCerStoragePath = x.CsdCerStoragePath,
                    CsdKeyStoragePath = x.CsdKeyStoragePath,
                    CsdPasswordProtected = x.CsdPasswordProtected,
                    Notes = x.Notes,
                    UpdatedAt = x.UpdatedAt
                })
                .FirstOrDefaultAsync();
        }
    }

    private async Task<SystemConfiguration> GetOrCreateAsync()
    {
        SystemConfiguration? entity;
        try
        {
            entity = await _db.SystemConfigurations.OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            throw new InvalidOperationException("Configuración requiere actualización de esquema en BD (faltan columnas de API). Ejecuta el script de actualización con usuario OWNER y vuelve a intentar.", ex);
        }
        if (entity != null) return entity;

        entity = new SystemConfiguration();
        _db.SystemConfigurations.Add(entity);
        return entity;
    }

    private static SecureSocketOptions ParseSecurity(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "sslonconnect" or "ssl" or "tls" => SecureSocketOptions.SslOnConnect,
            "starttls" => SecureSocketOptions.StartTls,
            "starttlswhenavailable" => SecureSocketOptions.StartTlsWhenAvailable,
            "none" => SecureSocketOptions.None,
            _ => SecureSocketOptions.Auto
        };
    }

    private static string PacLabel(PacProvider provider) => provider switch
    {
        PacProvider.Facturama => "Facturama",
        PacProvider.Finkok => "Finkok",
        PacProvider.SwSapien => "SW Sapien",
        _ => "Sin timbrado"
    };

    private static string NormalizeTemplate(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
