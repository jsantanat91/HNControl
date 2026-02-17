using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace HNControl.Web.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _cfg;

    public SmtpEmailSender(IConfiguration cfg) => _cfg = cfg;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, byte[]? attachmentBytes = null, string? attachmentName = null, string? attachmentContentType = null)
    {
        var host = _cfg["Smtp:Host"]!;
        var port = int.Parse(_cfg["Smtp:Port"] ?? "587");
        var startTls = bool.Parse(_cfg["Smtp:UseStartTls"] ?? "true");
        var user = _cfg["Smtp:User"]!;
        var pass = _cfg["Smtp:Password"]!;
        var fromEmail = _cfg["Smtp:FromEmail"]!;
        var fromName = _cfg["Smtp:FromName"] ?? "HN Control";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };

        if (attachmentBytes != null && attachmentBytes.Length > 0 && !string.IsNullOrWhiteSpace(attachmentName))
        {
            builder.Attachments.Add(attachmentName, attachmentBytes, ContentType.Parse(attachmentContentType ?? "application/octet-stream"));
        }

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, startTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);
        await client.AuthenticateAsync(user, pass);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
