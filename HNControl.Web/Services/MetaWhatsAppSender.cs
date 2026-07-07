using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HNControl.Web.Services;

/// <summary>
/// Envia WhatsApp mediante la Cloud API oficial de Meta (graph.facebook.com).
/// Reemplaza al gateway OpenWA. La configuracion vive en SystemConfigurations:
///  - WhatsAppGatewayUrl      -> Phone Number ID de Meta
///  - WhatsAppApiKeyProtected -> Access Token permanente (cifrado con DataProtection)
///  - WhatsAppGraphApiVersion -> version del Graph API (default v21.0)
///  - WhatsAppTemplateLanguage-> idioma por defecto de plantillas (default es_MX)
/// </summary>
public class MetaWhatsAppSender : IWhatsAppSender
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MetaWhatsAppSender> _log;

    public MetaWhatsAppSender(
        ApplicationDbContext db,
        ISecretProtector protector,
        IHttpClientFactory httpClientFactory,
        ILogger<MetaWhatsAppSender> log)
    {
        _db = db;
        _protector = protector;
        _httpClientFactory = httpClientFactory;
        _log = log;
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
    {
        var cfg = await LoadConfigSafeAsync(ct);
        return cfg?.WhatsAppEnabled == true
            && !string.IsNullOrWhiteSpace(cfg.WhatsAppGatewayUrl)      // Phone Number ID
            && !string.IsNullOrWhiteSpace(cfg.WhatsAppApiKeyProtected); // Access Token
    }

    public async Task SendAsync(string phone, string message, CancellationToken ct = default)
    {
        var to = NormalizePhone(phone);
        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(message))
            return;

        await PostAsync(to, new
        {
            messaging_product = "whatsapp",
            to,
            type = "text",
            text = new { preview_url = true, body = message }
        }, ct);
    }

    public async Task SendTemplateAsync(WhatsAppTemplateMessage message, CancellationToken ct = default)
    {
        var to = NormalizePhone(message.Phone);
        if (string.IsNullOrWhiteSpace(to)) return;

        // Sin plantilla configurada: caemos a texto libre (solo entrega en ventana 24h / pruebas).
        if (string.IsNullOrWhiteSpace(message.TemplateName))
        {
            if (!string.IsNullOrWhiteSpace(message.FallbackText))
                await SendAsync(to, message.FallbackText!, ct);
            return;
        }

        var cfg = await LoadConfigSafeAsync(ct);
        var language = !string.IsNullOrWhiteSpace(message.Language)
            ? message.Language!
            : string.IsNullOrWhiteSpace(cfg?.WhatsAppTemplateLanguage) ? "es_MX" : cfg!.WhatsAppTemplateLanguage;

        var parameters = message.Parameters ?? Array.Empty<string>();
        var components = new List<object>();

        if (parameters.Count > 0)
        {
            components.Add(new
            {
                type = "body",
                parameters = parameters.Select(p => new { type = "text", text = p ?? string.Empty }).ToArray()
            });
        }

        // Plantillas de categoria Authentication (OTP): Meta exige el boton copy-code con el codigo.
        if (message.AuthenticationCode && parameters.Count > 0)
        {
            components.Add(new
            {
                type = "button",
                sub_type = "url",
                index = "0",
                parameters = new object[] { new { type = "text", text = parameters[0] } }
            });
        }

        await PostAsync(to, new
        {
            messaging_product = "whatsapp",
            to,
            type = "template",
            template = new
            {
                name = message.TemplateName,
                language = new { code = language },
                components = components.ToArray()
            }
        }, ct);
    }

    public async Task<string> CheckConnectionAsync(CancellationToken ct = default)
    {
        var cfg = await LoadConfigSafeAsync(ct);
        var phoneNumberId = (cfg?.WhatsAppGatewayUrl ?? "").Trim();
        var accessToken = !string.IsNullOrWhiteSpace(cfg?.WhatsAppApiKeyProtected)
            ? _protector.Unprotect(cfg!.WhatsAppApiKeyProtected)
            : "";

        if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Falta Phone Number ID o Access Token. Guarda la configuracion primero.");

        var version = string.IsNullOrWhiteSpace(cfg?.WhatsAppGraphApiVersion) ? "v21.0" : cfg!.WhatsAppGraphApiVersion.Trim();
        var url = $"https://graph.facebook.com/{version}/{phoneNumberId}?fields=verified_name,display_phone_number,quality_rating";

        var client = _httpClientFactory.CreateClient("whatsapp");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Meta respondio {(int)response.StatusCode}: {Truncate(body, 300)}");

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var name = root.TryGetProperty("verified_name", out var n) ? n.GetString() : null;
            var phone = root.TryGetProperty("display_phone_number", out var p) ? p.GetString() : null;
            var quality = root.TryGetProperty("quality_rating", out var q) ? q.GetString() : null;
            var label = string.IsNullOrWhiteSpace(name) ? phoneNumberId : name;
            return $"{label} ({phone ?? "?"}) · calidad: {quality ?? "n/d"}";
        }
        catch
        {
            return Truncate(body, 200);
        }
    }

    private async Task PostAsync(string to, object body, CancellationToken ct)
    {
        var cfg = await LoadConfigSafeAsync(ct);
        if (cfg?.WhatsAppEnabled != true)
            return;

        var phoneNumberId = (cfg.WhatsAppGatewayUrl ?? "").Trim();
        var accessToken = !string.IsNullOrWhiteSpace(cfg.WhatsAppApiKeyProtected)
            ? _protector.Unprotect(cfg.WhatsAppApiKeyProtected)
            : "";

        if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("WhatsApp (Meta) no configurado: falta Phone Number ID o Access Token.");

        var version = string.IsNullOrWhiteSpace(cfg.WhatsAppGraphApiVersion) ? "v21.0" : cfg.WhatsAppGraphApiVersion.Trim();
        var url = $"https://graph.facebook.com/{version}/{phoneNumberId}/messages";

        var client = _httpClientFactory.CreateClient("whatsapp");
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            return;

        var err = await response.Content.ReadAsStringAsync(ct);
        _log.LogWarning("Meta Cloud API respondio {Status}: {Body}", (int)response.StatusCode, Truncate(err, 400));
        throw new InvalidOperationException($"Meta Cloud API respondio {(int)response.StatusCode}: {Truncate(err, 300)}");
    }

    private async Task<Models.SystemConfiguration?> LoadConfigSafeAsync(CancellationToken ct)
    {
        try
        {
            return await _db.SystemConfigurations
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _log.LogWarning(ex, "Esquema de SystemConfigurations desactualizado; WhatsApp en modo compatible.");
            try
            {
                return await _db.SystemConfigurations
                    .AsNoTracking()
                    .OrderByDescending(x => x.UpdatedAt)
                    .Select(x => new Models.SystemConfiguration
                    {
                        Id = x.Id,
                        WhatsAppEnabled = x.WhatsAppEnabled,
                        WhatsAppGatewayUrl = x.WhatsAppGatewayUrl,
                        WhatsAppApiKeyProtected = x.WhatsAppApiKeyProtected,
                        UpdatedAt = x.UpdatedAt
                    })
                    .FirstOrDefaultAsync(ct);
            }
            catch (PostgresException nestedEx) when (nestedEx.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                _log.LogWarning(nestedEx, "SystemConfigurations sin columnas WhatsApp; se omite WhatsApp.");
                return null;
            }
        }
    }

    /// <summary>
    /// Normaliza a E.164 sin '+' (formato de la Cloud API). Mexico: 52 + 10 digitos
    /// (sin el '1' de movil que usaba WhatsApp Web).
    /// </summary>
    private static string NormalizePhone(string? value)
    {
        var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal))
            digits = digits[2..];
        if (digits.Length == 10)
            return "52" + digits;
        if (digits.Length == 13 && digits.StartsWith("521", StringComparison.Ordinal))
            return "52" + digits[3..];
        return digits;
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
