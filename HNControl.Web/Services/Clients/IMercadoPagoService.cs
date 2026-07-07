namespace HNControl.Web.Services.Clients;

public interface IMercadoPagoService
{
    Task<MercadoPagoCheckoutResult> CreateCardDomiciliationCheckoutAsync(
        string clientCode,
        string clientName,
        string payerEmail,
        decimal referenceAmount,
        string description,
        CancellationToken ct = default);

    /// <summary>
    /// Valida el Access Token contra la API de Mercado Pago (GET /users/me) sin crear pagos.
    /// Devuelve datos de la cuenta si es valido; Success=false con el error si no.
    /// </summary>
    Task<MercadoPagoCheckoutResult> CheckConnectionAsync(CancellationToken ct = default);
}

public sealed class MercadoPagoCheckoutResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string Url { get; set; } = "";
    public string PreferenceId { get; set; } = "";
    public string ExternalReference { get; set; } = "";
}
