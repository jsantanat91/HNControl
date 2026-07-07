using System.Text.Json;
using HNControl.Web.Services.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HNControl.Web.Controllers;

/// <summary>
/// Recibe las notificaciones (webhooks) de Mercado Pago para confirmar pagos de
/// domiciliacion. Mercado Pago llama a esta URL (notification_url) tras cada pago.
/// Siempre respondemos 200 para que MP no reintente en bucle; el emparejamiento y
/// la validacion real se hacen consultando el pago en la API oficial.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/mercadopago")]
public class MercadoPagoWebhookController : ControllerBase
{
    private readonly IMercadoPagoService _mercadoPago;
    private readonly ILogger<MercadoPagoWebhookController> _log;

    public MercadoPagoWebhookController(IMercadoPagoService mercadoPago, ILogger<MercadoPagoWebhookController> log)
    {
        _mercadoPago = mercadoPago;
        _log = log;
    }

    [HttpPost("webhook")]
    [HttpGet("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        try
        {
            var (type, paymentId) = await ExtractAsync(ct);

            // Solo nos interesan notificaciones de pago.
            if (!string.IsNullOrWhiteSpace(paymentId) &&
                (string.IsNullOrWhiteSpace(type) || type.Contains("payment", StringComparison.OrdinalIgnoreCase)))
            {
                await _mercadoPago.ProcessPaymentNotificationAsync(paymentId, ct);
            }
        }
        catch (Exception ex)
        {
            // Nunca fallamos hacia MP: registramos y devolvemos 200.
            _log.LogWarning(ex, "Error procesando webhook de Mercado Pago");
        }

        return Ok();
    }

    private async Task<(string? type, string? paymentId)> ExtractAsync(CancellationToken ct)
    {
        // 1) Query params: ?type=payment&data.id=123  o  ?topic=payment&id=123
        var type = Request.Query["type"].FirstOrDefault() ?? Request.Query["topic"].FirstOrDefault();
        var paymentId = Request.Query["data.id"].FirstOrDefault()
            ?? Request.Query["id"].FirstOrDefault();

        // 2) Cuerpo JSON: { "type":"payment", "data": { "id": "123" } }
        if (string.IsNullOrWhiteSpace(paymentId) && Request.ContentLength is > 0)
        {
            using var reader = new StreamReader(Request.Body);
            var raw = await reader.ReadToEndAsync(ct);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    var root = doc.RootElement;
                    if (string.IsNullOrWhiteSpace(type) && root.TryGetProperty("type", out var t))
                        type = t.GetString();
                    if (root.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var idEl))
                        paymentId = idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString();
                }
                catch
                {
                    // cuerpo no-JSON: lo ignoramos, ya intentamos por query
                }
            }
        }

        return (type, paymentId);
    }
}
