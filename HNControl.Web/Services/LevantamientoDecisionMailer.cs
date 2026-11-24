using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

/// <summary>Sends the feasibility decision to the customer (2.6.3), with the survey PDF and internal document copies.</summary>
public class LevantamientoDecisionMailer(ApplicationDbContext db, IEmailSender email, DocumentNotificationRecipients recipients)
{
    /// <summary>Returns null when sent; otherwise the reason. The decision itself is never rolled back.</summary>
    public async Task<string?> SendAsync(Guid surveyId, string? to, CancellationToken ct = default)
    {
        var survey = await db.LevantamientoSurveys.FirstOrDefaultAsync(x => x.Id == surveyId, ct);
        if (survey == null) return "No se encontró el levantamiento.";
        if (survey.DecidedAt == null || survey.Status is not (LevantamientoSurveyStatus.Feasible or LevantamientoSurveyStatus.NotFeasible))
            return "Primero emite el dictamen de viabilidad.";
        var address = (string.IsNullOrWhiteSpace(to) ? survey.Email : to).Trim();
        if (!ClientCommunicationTemplates.IsEmail(address)) return "El correo del cliente no es válido.";

        IReadOnlyCollection<string> copies;
        try { copies = await recipients.GetAsync(); }
        catch (ArgumentException) { copies = []; }
        var sites = LevantamientoSurveyService.ReadSites(survey.SitesFileName, survey.SitesBytes).Count;
        var hasPdf = survey.PdfBytes is { Length: > 0 };
        try
        {
            await email.SendAsync(address, LevantamientoReview.EmailSubject(survey),
                LevantamientoReview.EmailBody(survey, sites, hasPdf),
                hasPdf ? survey.PdfBytes : null,
                hasPdf ? $"Levantamiento_{Safe(survey.Company)}.pdf" : null,
                hasPdf ? "application/pdf" : null,
                copies.Where(x => !x.Equals(address, StringComparison.OrdinalIgnoreCase)).ToArray());
        }
        catch (Exception ex)
        {
            return "No se pudo enviar el correo: " + ex.Message;
        }
        survey.DecisionEmailTo = LevantamientoSurveyService.Clip(address, 256);
        survey.DecisionEmailSentAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return null;
    }

    private static string Safe(string value)
    {
        var clean = new string(value.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
        return clean.Length == 0 ? "cliente" : clean.Length > 60 ? clean[..60] : clean;
    }
}
