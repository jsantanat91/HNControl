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
    public const string DefaultWhatsAppOtpTemplate =
        "Hola {NombreEmpleado}, tu codigo de acceso a HN Control es {Codigo}. Vence en {MinutosValidez} minutos.";

    public const string DefaultWhatsAppPayrollReceiptTemplate =
        "Hola {NombreEmpleado}, tu recibo de nomina del periodo {Periodo} esta disponible. Neto: {TotalNeto}. Ingresa al portal para consultarlo.";

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
        [MaxLength(2000)] public string WhatsAppOtpTemplate { get; set; } = DefaultWhatsAppOtpTemplate;
        [MaxLength(2000)] public string WhatsAppPayrollReceiptTemplate { get; set; } = DefaultWhatsAppPayrollReceiptTemplate;

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

        Flash = "Configuracion Mercado Pago guardada.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveWhatsAppAsync()
    {
        try
        {
            var entity = await GetOrCreateAsync();

            ApplyWhatsAppInput(entity);

            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        catch (InvalidOperationException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UndefinedColumn })
        {
            await SaveWhatsAppCompatibleAsync();
        }
        catch (DbUpdateException ex) when (ex.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.UndefinedColumn })
        {
            await SaveWhatsAppCompatibleAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            await SaveWhatsAppCompatibleAsync();
        }

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
                WhatsAppOtpTemplate = DefaultWhatsAppOtpTemplate,
                WhatsAppPayrollReceiptTemplate = DefaultWhatsAppPayrollReceiptTemplate
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
            WhatsAppOtpTemplate = NormalizeTemplate(cfg.WhatsAppOtpTemplate, DefaultWhatsAppOtpTemplate),
            WhatsAppPayrollReceiptTemplate = NormalizeTemplate(cfg.WhatsAppPayrollReceiptTemplate, DefaultWhatsAppPayrollReceiptTemplate),
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

            return await LoadLatestConfigCompatibleRawAsync();
        }
    }

    private async Task<SystemConfiguration?> LoadLatestConfigCompatibleRawAsync()
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != global::System.Data.ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """SELECT * FROM "SystemConfigurations" ORDER BY "UpdatedAt" DESC LIMIT 1""";
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            var cfg = new SystemConfiguration
            {
                Id = ReadGuid(reader, "Id", Guid.NewGuid()),
                CompanyName = ReadString(reader, "CompanyName", "HN Solutions"),
                CompanyLegalName = ReadString(reader, "CompanyLegalName"),
                CompanyRfc = ReadString(reader, "CompanyRfc"),
                CompanyFiscalRegimeCode = ReadString(reader, "CompanyFiscalRegimeCode", "601"),
                CompanyFiscalZipCode = ReadString(reader, "CompanyFiscalZipCode"),
                CompanyFiscalAddress = ReadString(reader, "CompanyFiscalAddress"),
                BillingEmail = ReadString(reader, "BillingEmail"),
                CompanyLogoStoragePath = ReadString(reader, "CompanyLogoStoragePath"),
                CompanyLogoOriginalFileName = ReadString(reader, "CompanyLogoOriginalFileName"),
                SmtpHost = ReadString(reader, "SmtpHost"),
                SmtpPort = ReadInt(reader, "SmtpPort", 587),
                SmtpUser = ReadString(reader, "SmtpUser"),
                SmtpPasswordProtected = ReadString(reader, "SmtpPasswordProtected"),
                SmtpFromEmail = ReadString(reader, "SmtpFromEmail"),
                SmtpFromName = ReadString(reader, "SmtpFromName", "HN Control"),
                SmtpSecurity = ReadString(reader, "SmtpSecurity", "StartTls"),
                SmtpHeloDomain = ReadString(reader, "SmtpHeloDomain"),
                SmtpTimeoutMs = ReadInt(reader, "SmtpTimeoutMs", 15000),
                BillingPacProvider = (PacProvider)ReadInt(reader, "BillingPacProvider", (int)PacProvider.None),
                BillingPacApiBaseUrl = ReadString(reader, "BillingPacApiBaseUrl"),
                BillingPacApiKey = ReadString(reader, "BillingPacApiKey"),
                BillingPacApiSecretProtected = ReadString(reader, "BillingPacApiSecretProtected"),
                BillingPacUsername = ReadString(reader, "BillingPacUsername"),
                BillingPacPasswordProtected = ReadString(reader, "BillingPacPasswordProtected"),
                CfdiVersion = ReadString(reader, "CfdiVersion", "4.0"),
                CfdiSerieDefault = ReadString(reader, "CfdiSerieDefault", "A"),
                CsdCerStoragePath = ReadString(reader, "CsdCerStoragePath"),
                CsdKeyStoragePath = ReadString(reader, "CsdKeyStoragePath"),
                CsdPasswordProtected = ReadString(reader, "CsdPasswordProtected"),
                MercadoPagoAccessTokenProtected = ReadString(reader, "MercadoPagoAccessTokenProtected"),
                MercadoPagoPublicKey = ReadString(reader, "MercadoPagoPublicKey"),
                MercadoPagoWebhookSecretProtected = ReadString(reader, "MercadoPagoWebhookSecretProtected"),
                WhatsAppEnabled = ReadBool(reader, "WhatsAppEnabled"),
                WhatsAppGatewayUrl = ReadString(reader, "WhatsAppGatewayUrl"),
                WhatsAppApiKeyProtected = ReadString(reader, "WhatsAppApiKeyProtected"),
                WhatsAppInternalPhonesCsv = ReadString(reader, "WhatsAppInternalPhonesCsv"),
                WhatsAppNotifyTickets = ReadBool(reader, "WhatsAppNotifyTickets", true),
                WhatsAppNotifyCustomers = ReadBool(reader, "WhatsAppNotifyCustomers"),
                WhatsAppOtpTemplate = ReadString(reader, "WhatsAppOtpTemplate"),
                WhatsAppPayrollReceiptTemplate = ReadString(reader, "WhatsAppPayrollReceiptTemplate"),
                PublicBaseUrl = ReadString(reader, "PublicBaseUrl"),
                Notes = ReadString(reader, "Notes"),
                UpdatedAt = ReadDateTime(reader, "UpdatedAt", DateTime.UtcNow)
            };

            return cfg;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static int GetOrdinalOrMissing(global::System.Data.Common.DbDataReader reader, string columnName)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static string ReadString(global::System.Data.Common.DbDataReader reader, string columnName, string fallback = "")
    {
        var ordinal = GetOrdinalOrMissing(reader, columnName);
        return ordinal < 0 || reader.IsDBNull(ordinal) ? fallback : Convert.ToString(reader.GetValue(ordinal)) ?? fallback;
    }

    private static int ReadInt(global::System.Data.Common.DbDataReader reader, string columnName, int fallback = 0)
    {
        var ordinal = GetOrdinalOrMissing(reader, columnName);
        return ordinal < 0 || reader.IsDBNull(ordinal) ? fallback : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static bool ReadBool(global::System.Data.Common.DbDataReader reader, string columnName, bool fallback = false)
    {
        var ordinal = GetOrdinalOrMissing(reader, columnName);
        return ordinal < 0 || reader.IsDBNull(ordinal) ? fallback : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private static Guid ReadGuid(global::System.Data.Common.DbDataReader reader, string columnName, Guid fallback)
    {
        var ordinal = GetOrdinalOrMissing(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal))
            return fallback;

        var value = reader.GetValue(ordinal);
        return value is Guid guid || Guid.TryParse(value.ToString(), out guid) ? guid : fallback;
    }

    private static DateTime ReadDateTime(global::System.Data.Common.DbDataReader reader, string columnName, DateTime fallback)
    {
        var ordinal = GetOrdinalOrMissing(reader, columnName);
        return ordinal < 0 || reader.IsDBNull(ordinal) ? fallback : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private void ApplyWhatsAppInput(SystemConfiguration entity)
    {
        entity.WhatsAppEnabled = Input.WhatsAppEnabled;
        entity.WhatsAppGatewayUrl = (Input.WhatsAppGatewayUrl ?? "").Trim();
        entity.WhatsAppInternalPhonesCsv = (Input.WhatsAppInternalPhonesCsv ?? "").Trim();
        entity.WhatsAppNotifyTickets = Input.WhatsAppNotifyTickets;
        entity.WhatsAppNotifyCustomers = Input.WhatsAppNotifyCustomers;
        entity.WhatsAppOtpTemplate = NormalizeTemplate(Input.WhatsAppOtpTemplate, DefaultWhatsAppOtpTemplate);
        entity.WhatsAppPayrollReceiptTemplate = NormalizeTemplate(Input.WhatsAppPayrollReceiptTemplate, DefaultWhatsAppPayrollReceiptTemplate);

        if (!string.IsNullOrWhiteSpace(Input.WhatsAppApiKey))
            entity.WhatsAppApiKeyProtected = _protector.Protect(Input.WhatsAppApiKey.Trim());
    }

    private async Task SaveWhatsAppCompatibleAsync()
    {
        await EnsureWhatsAppColumnsAsync();

        var id = await GetLatestConfigIdRawAsync() ?? Guid.NewGuid();
        var now = DateTime.UtcNow;
        var otpTemplate = NormalizeTemplate(Input.WhatsAppOtpTemplate, DefaultWhatsAppOtpTemplate);
        var payrollTemplate = NormalizeTemplate(Input.WhatsAppPayrollReceiptTemplate, DefaultWhatsAppPayrollReceiptTemplate);
        var gatewayUrl = (Input.WhatsAppGatewayUrl ?? "").Trim();
        var internalPhones = (Input.WhatsAppInternalPhonesCsv ?? "").Trim();
        var apiKeyProtected = !string.IsNullOrWhiteSpace(Input.WhatsAppApiKey)
            ? _protector.Protect(Input.WhatsAppApiKey.Trim())
            : null;

        if (!await SystemConfigurationExistsRawAsync(id))
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "SystemConfigurations"
                    ("Id", "CompanyName", "CompanyFiscalRegimeCode", "CfdiVersion", "CfdiSerieDefault", "SmtpPort", "SmtpSecurity", "SmtpTimeoutMs",
                     "WhatsAppEnabled", "WhatsAppGatewayUrl", "WhatsAppApiKeyProtected", "WhatsAppInternalPhonesCsv", "WhatsAppNotifyTickets",
                     "WhatsAppNotifyCustomers", "WhatsAppOtpTemplate", "WhatsAppPayrollReceiptTemplate", "UpdatedAt")
                VALUES
                    ({id}, {"HN Solutions"}, {"601"}, {"4.0"}, {"A"}, {587}, {"StartTls"}, {15000},
                     {Input.WhatsAppEnabled}, {gatewayUrl}, {apiKeyProtected ?? ""}, {internalPhones}, {Input.WhatsAppNotifyTickets},
                     {Input.WhatsAppNotifyCustomers}, {otpTemplate}, {payrollTemplate}, {now})
                """);
            return;
        }

        if (!string.IsNullOrWhiteSpace(apiKeyProtected))
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "SystemConfigurations"
                SET "WhatsAppEnabled" = {Input.WhatsAppEnabled},
                    "WhatsAppGatewayUrl" = {gatewayUrl},
                    "WhatsAppApiKeyProtected" = {apiKeyProtected},
                    "WhatsAppInternalPhonesCsv" = {internalPhones},
                    "WhatsAppNotifyTickets" = {Input.WhatsAppNotifyTickets},
                    "WhatsAppNotifyCustomers" = {Input.WhatsAppNotifyCustomers},
                    "WhatsAppOtpTemplate" = {otpTemplate},
                    "WhatsAppPayrollReceiptTemplate" = {payrollTemplate},
                    "UpdatedAt" = {now}
                WHERE "Id" = {id}
                """);
        }
        else
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "SystemConfigurations"
                SET "WhatsAppEnabled" = {Input.WhatsAppEnabled},
                    "WhatsAppGatewayUrl" = {gatewayUrl},
                    "WhatsAppInternalPhonesCsv" = {internalPhones},
                    "WhatsAppNotifyTickets" = {Input.WhatsAppNotifyTickets},
                    "WhatsAppNotifyCustomers" = {Input.WhatsAppNotifyCustomers},
                    "WhatsAppOtpTemplate" = {otpTemplate},
                    "WhatsAppPayrollReceiptTemplate" = {payrollTemplate},
                    "UpdatedAt" = {now}
                WHERE "Id" = {id}
                """);
        }
    }

    private async Task EnsureWhatsAppColumnsAsync()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE IF EXISTS "SystemConfigurations" ADD COLUMN IF NOT EXISTS "WhatsAppEnabled" boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE IF EXISTS "SystemConfigurations" ADD COLUMN IF NOT EXISTS "WhatsAppGatewayUrl" character varying(300) NOT NULL DEFAULT '';
            ALTER TABLE IF EXISTS "SystemConfigurations" ADD COLUMN IF NOT EXISTS "WhatsAppApiKeyProtected" character varying(2200) NOT NULL DEFAULT '';
            ALTER TABLE IF EXISTS "SystemConfigurations" ADD COLUMN IF NOT EXISTS "WhatsAppInternalPhonesCsv" character varying(1000) NOT NULL DEFAULT '';
            ALTER TABLE IF EXISTS "SystemConfigurations" ADD COLUMN IF NOT EXISTS "WhatsAppNotifyTickets" boolean NOT NULL DEFAULT TRUE;
            ALTER TABLE IF EXISTS "SystemConfigurations" ADD COLUMN IF NOT EXISTS "WhatsAppNotifyCustomers" boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE IF EXISTS "SystemConfigurations" ADD COLUMN IF NOT EXISTS "WhatsAppOtpTemplate" character varying(2000) NOT NULL DEFAULT '';
            ALTER TABLE IF EXISTS "SystemConfigurations" ADD COLUMN IF NOT EXISTS "WhatsAppPayrollReceiptTemplate" character varying(2000) NOT NULL DEFAULT '';
            """);
    }

    private async Task<Guid?> GetLatestConfigIdRawAsync()
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != global::System.Data.ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """SELECT "Id" FROM "SystemConfigurations" ORDER BY "UpdatedAt" DESC LIMIT 1""";
            var result = await cmd.ExecuteScalarAsync();
            if (result is Guid guid)
                return guid;
            return Guid.TryParse(result?.ToString(), out var parsed) ? parsed : null;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<bool> SystemConfigurationExistsRawAsync(Guid id)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != global::System.Data.ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """SELECT EXISTS (SELECT 1 FROM "SystemConfigurations" WHERE "Id" = @id)""";
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = "id";
            parameter.Value = id;
            cmd.Parameters.Add(parameter);
            return await cmd.ExecuteScalarAsync() is true;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
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
