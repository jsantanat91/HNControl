using System.Net.Http.Headers;
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

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Remove("x-api-key");
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        var payload = JsonSerializer.Serialize(new { to, message }, JsonOptions);
        var errors = new List<string>();

        foreach (var path in new[] { "/send", "/send-message" })
        {
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(baseUrl + path, content, ct);
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(ct);
            errors.Add($"{path}: {(int)response.StatusCode} {Truncate(body, 260)}");
        }

        throw new InvalidOperationException("OpenWA no pudo enviar mensaje. " + string.Join(" | ", errors));
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
