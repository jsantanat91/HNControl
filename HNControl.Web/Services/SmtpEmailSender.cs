using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace HNControl.Web.Services;

/// <summary>
/// Envío SMTP usando MailKit.
/// Soporta configuraciones típicas de cPanel/Exim.
///
/// Config (acepta nombres nuevos y legacy):
///   Smtp:Host
///   Smtp:Port
///   Smtp:User  (o Smtp:Username)
///   Smtp:Password
///   Smtp:FromEmail
///   Smtp:FromName
///   Smtp:Security = StartTls | SslOnConnect | None | Auto
///   Smtp:UseSsl (legacy bool)
///   Smtp:UseStartTls (legacy bool)
///   Smtp:TimeoutMs (default 15000)
///   Smtp:HeloDomain (recomendado p/ Exim: FQDN sin guiones raros)
///
/// Nota: Algunos Exim rechazan HELO si no es un FQDN válido.
///       Por default, si el hostname local NO tiene punto, usamos el dominio de FromEmail.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<SmtpEmailSender> _log;

    public SmtpEmailSender(IConfiguration cfg, ILogger<SmtpEmailSender> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        byte[]? attachmentBytes = null,
        string? attachmentName = null,
        string? attachmentContentType = null)
    {
        var host = (_cfg["Smtp:Host"] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("SMTP no configurado: falta Smtp:Host");

        var port = int.TryParse(_cfg["Smtp:Port"], out var p) ? p : 587;

        // Compatibilidad: User vs Username
        var user = (_cfg["Smtp:User"] ?? _cfg["Smtp:Username"] ?? "").Trim();
        var pass = _cfg["Smtp:Password"] ?? "";

        var fromEmail = (_cfg["Smtp:FromEmail"] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(fromEmail))
            throw new InvalidOperationException("SMTP no configurado: falta Smtp:FromEmail");

        var fromName = (_cfg["Smtp:FromName"] ?? "HN Control").Trim();

        var timeoutMs = int.TryParse(_cfg["Smtp:TimeoutMs"], out var t) ? t : 15000;

        // Nuevos vs legacy
        var security = (_cfg["Smtp:Security"] ?? "").Trim();
        var legacyUseSsl = bool.TryParse(_cfg["Smtp:UseSsl"], out var ussl) && ussl;
        var legacyStartTls = bool.TryParse(_cfg["Smtp:UseStartTls"], out var st) ? st : true;

        var options = ParseSecurity(security, port, legacyUseSsl, legacyStartTls);

        // HELO/EHLO: Exim puede rechazar hostnames sin FQDN
        var heloDomain = BuildHeloDomain(fromEmail);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };
        if (attachmentBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(attachmentName))
        {
            builder.Attachments.Add(
                attachmentName,
                attachmentBytes,
                ContentType.Parse(attachmentContentType ?? "application/octet-stream"));
        }
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        client.Timeout = timeoutMs;
        client.LocalDomain = heloDomain;

        using var cts = new CancellationTokenSource(timeoutMs);

        try
        {
            await client.ConnectAsync(host, port, options, cts.Token);

            // Si hay user, autentica; si no, intenta sin auth (algunos SMTP internos).
            if (!string.IsNullOrWhiteSpace(user))
                await client.AuthenticateAsync(user, pass, cts.Token);

            await client.SendAsync(message, cts.Token);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Error SMTP a {To} via {Host}:{Port} ({Options}). HELO={Helo}",
                toEmail, host, port, options, heloDomain);
            throw;
        }
        finally
        {
            try { await client.DisconnectAsync(true); } catch { /* ignore */ }
        }
    }

    private string BuildHeloDomain(string fromEmail)
    {
        var explicitHelo = (_cfg["Smtp:HeloDomain"] ?? _cfg["Smtp:LocalDomain"] ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(explicitHelo))
            return SanitizeFqdn(explicitHelo);

        var hostName = Dns.GetHostName();
        if (!string.IsNullOrWhiteSpace(hostName) && hostName.Contains('.'))
            return SanitizeFqdn(hostName);

        // Si el hostname no es FQDN, usa el dominio del remitente.
        var fromDomain = fromEmail.Split('@').LastOrDefault()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(fromDomain))
            return "hncontrol.localdomain";

        // Asegura FQDN (con punto). Si el fromDomain ya lo tiene, úsalo.
        var fqdn = fromDomain.Contains('.') ? fromDomain : $"hncontrol.{fromDomain}";
        return SanitizeFqdn(fqdn);
    }

    private static string SanitizeFqdn(string value)
    {
        value = (value ?? "").Trim().Trim('.');
        if (string.IsNullOrWhiteSpace(value))
            return "hncontrol.localdomain";

        // Exim suele ser estricto: letras, números, guion y puntos.
        var chars = value.Select(ch =>
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '-' || ch == '.')
                return ch;
            return '-';
        }).ToArray();

        var cleaned = new string(chars);

        // No permitir doble punto ni guion al inicio/fin de label.
        cleaned = string.Join('.', cleaned
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(label => label.Trim('-'))
            .Where(label => !string.IsNullOrWhiteSpace(label)));

        if (!cleaned.Contains('.'))
            cleaned = $"hncontrol.{cleaned}";

        return cleaned;
    }

    private static SecureSocketOptions ParseSecurity(string security, int port, bool legacyUseSsl, bool legacyStartTls)
    {
        if (!string.IsNullOrWhiteSpace(security))
        {
            return security.Trim().ToLowerInvariant() switch
            {
                "sslonconnect" or "ssl" or "tls" => SecureSocketOptions.SslOnConnect,
                "starttls" => SecureSocketOptions.StartTls,
                "starttlswhenavailable" => SecureSocketOptions.StartTlsWhenAvailable,
                "none" => SecureSocketOptions.None,
                "auto" => SecureSocketOptions.Auto,
                _ => SecureSocketOptions.Auto
            };
        }

        // Legacy: UseSsl
        if (legacyUseSsl)
        {
            // 465 suele ser SSL on connect, 587 suele ser StartTLS
            if (port == 465) return SecureSocketOptions.SslOnConnect;
            return SecureSocketOptions.StartTls;
        }

        if (port == 465) return SecureSocketOptions.SslOnConnect;
        return legacyStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.Auto;
    }
}
