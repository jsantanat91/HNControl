using System.Text;
using System.Text.Json;
using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HNControl.Web.Services;

public class OpenWaWhatsAppSender : IWhatsAppSender
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenWaWhatsAppSender> _log;

    public OpenWaWhatsAppSender(
        ApplicationDbContext db,
        ISecretProtector protector,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenWaWhatsAppSender> log)
    {
        _db = db;
        _protector = protector;
        _httpClientFactory = httpClientFactory;
        _log = log;
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
    {
        var cfg = await LoadConfigSafeAsync(ct);
        return cfg?.WhatsAppEnabled == true && !string.IsNullOrWhiteSpace(cfg.WhatsAppGatewayUrl);
    }

    public async Task SendAsync(string phone, string message, CancellationToken ct = default)
    {
        var to = NormalizePhone(phone);
        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(message))
            return;

        var cfg = await LoadConfigSafeAsync(ct);
        if (cfg?.WhatsAppEnabled != true || string.IsNullOrWhiteSpace(cfg.WhatsAppGatewayUrl))
            return;

        var client = _httpClientFactory.CreateClient("openwa");
        var baseUrl = cfg.WhatsAppGatewayUrl.Trim().TrimEnd('/');
        var apiKey = !string.IsNullOrWhiteSpace(cfg.WhatsAppApiKeyProtected)
            ? _protector.Unprotect(cfg.WhatsAppApiKeyProtected)
            : "";

        var payload = JsonSerializer.Serialize(new { to, message }, JsonOptions);
        var errors = new List<string>();

        var sendUrl = baseUrl + "/send";
        using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, sendUrl) { Content = content };
            if (!string.IsNullOrWhiteSpace(apiKey))
                request.Headers.TryAddWithoutValidation("x-api-key", apiKey);

            var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(ct);
            errors.Add($"/send: {(int)response.StatusCode} {Truncate(body, 260)}");
        }

        // Algunos proxies internos no pasan headers personalizados; el gateway tambien acepta ?key=.
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(sendUrl + "?key=" + Uri.EscapeDataString(apiKey), content, ct);
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(ct);
            errors.Add($"/send?key=***: {(int)response.StatusCode} {Truncate(body, 260)}");
        }

        var hint = errors.Any(x => x.Contains("401", StringComparison.OrdinalIgnoreCase))
            ? " Verifica que el token guardado coincida con HNCONTROL_OPENWA_API_KEY o con el valor default hncontrol_wa_key_2026 del contenedor."
            : "";

        throw new InvalidOperationException("OpenWA no pudo enviar mensaje. " + string.Join(" | ", errors) + hint);
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
            _log.LogWarning(ex, "Esquema de SystemConfigurations desactualizado. Se carga WhatsApp en modo compatible.");

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
                        WhatsAppInternalPhonesCsv = x.WhatsAppInternalPhonesCsv,
                        WhatsAppNotifyTickets = x.WhatsAppNotifyTickets,
                        WhatsAppNotifyCustomers = x.WhatsAppNotifyCustomers,
                        WhatsAppOtpTemplate = x.WhatsAppOtpTemplate,
                        WhatsAppPayrollReceiptTemplate = x.WhatsAppPayrollReceiptTemplate,
                        UpdatedAt = x.UpdatedAt
                    })
                    .FirstOrDefaultAsync(ct);
            }
            catch (PostgresException nestedEx) when (nestedEx.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                _log.LogWarning(nestedEx, "Esquema de SystemConfigurations sin columnas WhatsApp. Se omite WhatsApp.");
                return null;
            }
        }
    }

    private static string NormalizePhone(string value)
    {
        var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 10)
            digits = "52" + digits;
        return digits;
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
