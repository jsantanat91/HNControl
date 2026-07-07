using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HNControl.Web.Services.Clients;

public class MercadoPagoService : IMercadoPagoService
{
    private readonly IConfiguration _cfg;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ApplicationDbContext _db;
    private readonly ISecretProtector _protector;

    public MercadoPagoService(
        IConfiguration cfg,
        IHttpClientFactory httpFactory,
        ApplicationDbContext db,
        ISecretProtector protector)
    {
        _cfg = cfg;
        _httpFactory = httpFactory;
        _db = db;
        _protector = protector;
    }

    public async Task<bool> ProcessPaymentNotificationAsync(string paymentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
            return false;

        SystemConfiguration? settings = null;
        try
        {
            settings = await _db.SystemConfigurations
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            settings = null;
        }

        var token = !string.IsNullOrWhiteSpace(settings?.MercadoPagoAccessTokenProtected)
            ? _protector.Unprotect(settings.MercadoPagoAccessTokenProtected)
            : (_cfg["MercadoPago:AccessToken"] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(token))
            return false;

        // Reconsultamos el pago en la API oficial: esto valida que el pago existe y es
        // nuestro (evita notificaciones falsas), sin depender de la firma del webhook.
        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.GetAsync($"https://api.mercadopago.com/v1/payments/{paymentId}", ct);
        if (!response.IsSuccessStatusCode)
            return false;

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
        var externalRef = root.TryGetProperty("external_reference", out var e) ? e.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(externalRef))
            return false;

        var domiciliation = await _db.ClientCardDomiciliations
            .FirstOrDefaultAsync(x => x.MercadoPagoExternalReference == externalRef, ct);
        if (domiciliation is null)
            return false;

        var newStatus = status switch
        {
            "approved" => ClientCardDomiciliationStatus.Active,
            "authorized" => ClientCardDomiciliationStatus.Active,
            "rejected" => ClientCardDomiciliationStatus.Rejected,
            "cancelled" => ClientCardDomiciliationStatus.Cancelled,
            "refunded" => ClientCardDomiciliationStatus.Cancelled,
            "charged_back" => ClientCardDomiciliationStatus.Cancelled,
            _ => ClientCardDomiciliationStatus.Pending
        };

        if (domiciliation.Status != newStatus)
        {
            domiciliation.Status = newStatus;
            domiciliation.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return true;
    }

    public async Task<MercadoPagoCheckoutResult> CheckConnectionAsync(CancellationToken ct = default)
    {
        SystemConfiguration? settings = null;
        try
        {
            settings = await _db.SystemConfigurations
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            settings = null;
        }

        var token = !string.IsNullOrWhiteSpace(settings?.MercadoPagoAccessTokenProtected)
            ? _protector.Unprotect(settings.MercadoPagoAccessTokenProtected)
            : (_cfg["MercadoPago:AccessToken"] ?? "").Trim();

        if (string.IsNullOrWhiteSpace(token))
            return new MercadoPagoCheckoutResult { Success = false, Message = "Mercado Pago no esta configurado (falta AccessToken). Guarda primero." };

        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.GetAsync("https://api.mercadopago.com/users/me", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = body.Length > 250 ? body[..250] : body;
            return new MercadoPagoCheckoutResult { Success = false, Message = $"Mercado Pago respondio ({(int)response.StatusCode}): {detail}" };
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var nickname = root.TryGetProperty("nickname", out var n) ? n.GetString() : null;
            var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
            var siteId = root.TryGetProperty("site_id", out var s) ? s.GetString() : null;
            var id = root.TryGetProperty("id", out var i) ? i.ToString() : null;
            var label = nickname ?? email ?? id ?? "cuenta";
            return new MercadoPagoCheckoutResult { Success = true, Message = $"{label} · sitio {siteId ?? "?"} · ID {id ?? "?"}" };
        }
        catch
        {
            return new MercadoPagoCheckoutResult { Success = true, Message = "Token valido." };
        }
    }

    public async Task<MercadoPagoCheckoutResult> CreateCardDomiciliationCheckoutAsync(
        string clientCode,
        string clientName,
        string payerEmail,
        decimal referenceAmount,
        string description,
        CancellationToken ct = default)
    {
        SystemConfiguration? settings = null;
        try
        {
            settings = await _db.SystemConfigurations
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            settings = null;
        }

        var token = !string.IsNullOrWhiteSpace(settings?.MercadoPagoAccessTokenProtected)
            ? _protector.Unprotect(settings.MercadoPagoAccessTokenProtected)
            : (_cfg["MercadoPago:AccessToken"] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return new MercadoPagoCheckoutResult
            {
                Success = false,
                Message = "Mercado Pago no esta configurado (falta AccessToken)."
            };
        }

        var siteUrl = (settings?.PublicBaseUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(siteUrl))
            siteUrl = (_cfg["PublicLinks:BaseUrl"] ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(siteUrl))
            siteUrl = "https://localhost";

        var externalRef = $"HNPORTAL-{clientCode}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var payload = new
        {
            items = new[]
            {
                new
                {
                    title = $"Pago de servicios - {clientName}",
                    description,
                    quantity = 1,
                    currency_id = "MXN",
                    unit_price = Math.Round(referenceAmount <= 0 ? 1m : referenceAmount, 2)
                }
            },
            payer = new { email = payerEmail },
            external_reference = externalRef,
            back_urls = new
            {
                success = $"{siteUrl}/Portal?mp=success",
                pending = $"{siteUrl}/Portal?mp=pending",
                failure = $"{siteUrl}/Portal?mp=failure"
            },
            auto_return = "approved",
            // Meta/MP notificará aquí el resultado del pago para confirmar la domiciliación.
            notification_url = $"{siteUrl}/api/mercadopago/webhook"
        };

        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.PostAsJsonAsync("https://api.mercadopago.com/checkout/preferences", payload, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return new MercadoPagoCheckoutResult
            {
                Success = false,
                Message = $"Mercado Pago devolvio error ({(int)response.StatusCode}): {body}"
            };
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var initPoint = root.TryGetProperty("init_point", out var p) ? p.GetString() ?? "" : "";
        var prefId = root.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";

        return new MercadoPagoCheckoutResult
        {
            Success = !string.IsNullOrWhiteSpace(initPoint),
            Url = initPoint,
            PreferenceId = prefId,
            ExternalReference = externalRef,
            Message = string.IsNullOrWhiteSpace(initPoint)
                ? "Mercado Pago respondio sin URL de pago."
                : "OK"
        };
    }
}
