using System.Net;
using HNControl.Web.Data;
using HNControl.Web.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace HNControl.Web.Services;

/// <summary>
/// Envio SMTP usando MailKit.
/// Soporta configuraciones tipicas de cPanel/Exim.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _cfg;
    private readonly ApplicationDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly ILogger<SmtpEmailSender> _log;

    public SmtpEmailSender(
        IConfiguration cfg,
        ApplicationDbContext db,
        ISecretProtector protector,
        ILogger<SmtpEmailSender> log)
    {
        _cfg = cfg;
        _db = db;
        _protector = protector;
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
        var dbCfg = await _db.SystemConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync();

        var legacyUseSsl = bool.TryParse(_cfg["Smtp:UseSsl"], out var ussl) && ussl;
        var legacyStartTls = bool.TryParse(_cfg["Smtp:UseStartTls"], out var st) ? st : true;

        var appProfile = BuildAppProfile(legacyUseSsl, legacyStartTls);
        var dbProfile = BuildDbProfile(dbCfg, legacyUseSsl, legacyStartTls, appProfile);

        var preferDb = bool.TryParse(_cfg["Smtp:PreferDbConfig"], out var preferDbParsed) && preferDbParsed;
        var profiles = new List<SmtpProfile>();

        if (preferDb)
        {
            AddIfValid(profiles, dbProfile);
            AddIfValid(profiles, appProfile);
        }
        else
        {
            // Por defecto priorizamos appsettings/codigo para evitar bloqueos por SMTP parcial en DB.
            AddIfValid(profiles, appProfile);
            AddIfValid(profiles, dbProfile);
        }

        if (profiles.Count == 0)
            throw new InvalidOperationException("SMTP no configurado: falta Smtp:Host/Smtp:FromEmail en codigo o configuracion.");

        Exception? lastError = null;

        foreach (var profile in profiles)
        {
            var message = BuildMessage(profile.FromName, profile.FromEmail, toEmail, subject, htmlBody, attachmentBytes, attachmentName, attachmentContentType);

            try
            {
                await SendWithRetryAsync(profile, message, toEmail);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                _log.LogWarning(ex,
                    "SMTP perfil fallo para {To} via {Host}:{Port} ({Options}). Intentando siguiente perfil...",
                    toEmail, profile.Host, profile.Port, profile.Options);
            }
        }

        throw new InvalidOperationException(
            "No se pudo enviar correo SMTP con los perfiles configurados (codigo y DB). Revisa conectividad de red/DNS y puerto SMTP.",
            lastError);
    }

    private SmtpProfile BuildAppProfile(bool legacyUseSsl, bool legacyStartTls)
    {
        var host = FirstNonEmpty(_cfg["Smtp:Host"]);
        var port = int.TryParse(_cfg["Smtp:Port"], out var p) ? p : 587;
        var user = FirstNonEmpty(_cfg["Smtp:User"], _cfg["Smtp:Username"]);
        var pass = FirstNonEmpty(_cfg["Smtp:Password"]);
        var fromEmail = FirstNonEmpty(_cfg["Smtp:FromEmail"]);
        var fromName = FirstNonEmpty(_cfg["Smtp:FromName"], "HN Control");
        var timeout = Math.Clamp(int.TryParse(_cfg["Smtp:TimeoutMs"], out var t) ? t : 15000, 15000, 120000);
        var securityRaw = FirstNonEmpty(_cfg["Smtp:Security"]);
        var options = NormalizeSecurityForPort(ParseSecurity(securityRaw, port, legacyUseSsl, legacyStartTls), port);
        var helo = BuildHeloDomain(fromEmail, _cfg["Smtp:HeloDomain"]);

        return new SmtpProfile(host, port, options, timeout, helo, user, pass, fromEmail, fromName);
    }

    private SmtpProfile BuildDbProfile(SystemConfiguration? dbCfg, bool legacyUseSsl, bool legacyStartTls, SmtpProfile appProfile)
    {
        var host = FirstNonEmpty(dbCfg?.SmtpHost);
        var port = dbCfg?.SmtpPort > 0 ? dbCfg.SmtpPort : appProfile.Port;
        var user = FirstNonEmpty(dbCfg?.SmtpUser);
        var pass = !string.IsNullOrWhiteSpace(dbCfg?.SmtpPasswordProtected)
            ? _protector.Unprotect(dbCfg!.SmtpPasswordProtected)
            : string.Empty;
        var fromEmail = FirstNonEmpty(dbCfg?.SmtpFromEmail);
        var fromName = FirstNonEmpty(dbCfg?.SmtpFromName, "HN Control");
        var timeout = Math.Clamp(dbCfg?.SmtpTimeoutMs > 0 ? dbCfg.SmtpTimeoutMs : 15000, 15000, 120000);
        var securityRaw = FirstNonEmpty(dbCfg?.SmtpSecurity);
        var options = NormalizeSecurityForPort(ParseSecurity(securityRaw, port, legacyUseSsl, legacyStartTls), port);
        var helo = BuildHeloDomain(FirstNonEmpty(fromEmail, appProfile.FromEmail), dbCfg?.SmtpHeloDomain);

        return new SmtpProfile(host, port, options, timeout, helo, user, pass, fromEmail, fromName);
    }

    private static MimeMessage BuildMessage(
        string fromName,
        string fromEmail,
        string toEmail,
        string subject,
        string htmlBody,
        byte[]? attachmentBytes,
        string? attachmentName,
        string? attachmentContentType)
    {
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
        return message;
    }

    private async Task SendWithRetryAsync(SmtpProfile profile, MimeMessage message, string toEmail)
    {
        try
        {
            await SendOnceAsync(profile.Host, profile.Port, profile.Options, profile.TimeoutMs, profile.HeloDomain, profile.User, profile.Pass, message);
        }
        catch (OperationCanceledException ex)
        {
            var fallbackOptions = NormalizeSecurityForPort(SecureSocketOptions.Auto, profile.Port);
            var fallbackTimeout = Math.Max(profile.TimeoutMs, 60000);
            try
            {
                await SendOnceAsync(profile.Host, profile.Port, fallbackOptions, fallbackTimeout, profile.HeloDomain, profile.User, profile.Pass, message);
            }
            catch (Exception retryEx)
            {
                _log.LogError(retryEx,
                    "Error SMTP (retry) a {To} via {Host}:{Port} ({Options}). HELO={Helo}",
                    toEmail, profile.Host, profile.Port, fallbackOptions, profile.HeloDomain);
                throw new InvalidOperationException("No se pudo conectar al servidor SMTP (timeout/reintento). Revisa Host/Puerto/Seguridad.", ex);
            }
        }
    }

    private static async Task SendOnceAsync(
        string host,
        int port,
        SecureSocketOptions options,
        int timeoutMs,
        string heloDomain,
        string user,
        string pass,
        MimeMessage message)
    {
        using var client = new SmtpClient();
        client.Timeout = timeoutMs;
        client.LocalDomain = heloDomain;
        using var cts = new CancellationTokenSource(timeoutMs);

        try
        {
            await client.ConnectAsync(host, port, options, cts.Token);

            if (!string.IsNullOrWhiteSpace(user))
                await client.AuthenticateAsync(user, pass, cts.Token);

            await client.SendAsync(message, cts.Token);
        }
        finally
        {
            try { await client.DisconnectAsync(true); } catch { }
        }
    }

    private string BuildHeloDomain(string fromEmail, string? configuredHeloDomain)
    {
        var explicitHelo = (configuredHeloDomain ?? _cfg["Smtp:HeloDomain"] ?? _cfg["Smtp:LocalDomain"] ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(explicitHelo))
            return SanitizeFqdn(explicitHelo);

        var hostName = Dns.GetHostName();
        if (!string.IsNullOrWhiteSpace(hostName) && hostName.Contains('.'))
            return SanitizeFqdn(hostName);

        var fromDomain = fromEmail.Split('@').LastOrDefault()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(fromDomain))
            return "hncontrol.localdomain";

        var fqdn = fromDomain.Contains('.') ? fromDomain : $"hncontrol.{fromDomain}";
        return SanitizeFqdn(fqdn);
    }

    private static string SanitizeFqdn(string value)
    {
        value = (value ?? "").Trim().Trim('.');
        if (string.IsNullOrWhiteSpace(value))
            return "hncontrol.localdomain";

        var chars = value.Select(ch =>
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '-' || ch == '.')
                return ch;
            return '-';
        }).ToArray();

        var cleaned = new string(chars);
        cleaned = string.Join('.', cleaned
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(label => label.Trim('-'))
            .Where(label => !string.IsNullOrWhiteSpace(label)));

        if (!cleaned.Contains('.'))
            cleaned = $"hncontrol.{cleaned}";

        return cleaned;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return string.Empty;
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

        if (legacyUseSsl)
        {
            if (port == 465) return SecureSocketOptions.SslOnConnect;
            return SecureSocketOptions.StartTls;
        }

        if (port == 465) return SecureSocketOptions.SslOnConnect;
        return legacyStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.Auto;
    }

    private static SecureSocketOptions NormalizeSecurityForPort(SecureSocketOptions selected, int port)
    {
        if (port == 465 && (selected == SecureSocketOptions.StartTls || selected == SecureSocketOptions.StartTlsWhenAvailable))
            return SecureSocketOptions.SslOnConnect;

        if ((port == 587 || port == 25) && selected == SecureSocketOptions.SslOnConnect)
            return SecureSocketOptions.StartTlsWhenAvailable;

        return selected;
    }

    private static void AddIfValid(List<SmtpProfile> profiles, SmtpProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Host) || string.IsNullOrWhiteSpace(profile.FromEmail))
            return;

        if (profiles.Any(p =>
            string.Equals(p.Host, profile.Host, StringComparison.OrdinalIgnoreCase)
            && p.Port == profile.Port
            && p.Options == profile.Options
            && string.Equals(p.FromEmail, profile.FromEmail, StringComparison.OrdinalIgnoreCase)))
            return;

        profiles.Add(profile);
    }

    private sealed record SmtpProfile(
        string Host,
        int Port,
        SecureSocketOptions Options,
        int TimeoutMs,
        string HeloDomain,
        string User,
        string Pass,
        string FromEmail,
        string FromName);
}

