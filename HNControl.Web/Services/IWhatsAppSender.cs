namespace HNControl.Web.Services;

public interface IWhatsAppSender
{
    Task SendAsync(string phone, string message, CancellationToken ct = default);
    Task<bool> IsConfiguredAsync(CancellationToken ct = default);
}
