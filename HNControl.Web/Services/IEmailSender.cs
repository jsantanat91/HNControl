namespace HNControl.Web.Services;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, byte[]? attachmentBytes = null, string? attachmentName = null, string? attachmentContentType = null);
}
