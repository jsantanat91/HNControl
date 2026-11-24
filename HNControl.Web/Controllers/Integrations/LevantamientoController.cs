using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using HNControl.Web.Data;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Controllers.Integrations;

[ApiController, AllowAnonymous, ServiceFilter(typeof(LevantamientoIntegrationAuth))]
[Route("api/integrations/levantamiento"), EnableRateLimiting("levantamiento-integration")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public partial class LevantamientoController(ApplicationDbContext db, LevantamientoSurveyService surveys) : ControllerBase
{
    /// <summary>Receives a submitted survey (idempotent by externalId) for the Factibilidades module.</summary>
    [HttpPost("surveys"), RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Survey(LevantamientoSurveyInput input, CancellationToken ct)
    {
        var (survey, reused, error) = await surveys.IngestAsync(input, ct);
        if (survey == null) return BadRequest(new { message = error });
        var body = new { id = survey.Id, reused, clientLinked = survey.ClientId != null };
        return reused ? Ok(body) : StatusCode(201, body);
    }

    // RFC persona moral (12) o física (13): letras/&/Ñ, fecha AAMMDD y homoclave.
    [GeneratedRegex(@"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$")]
    private static partial Regex RfcPattern();

    public static string NormalizeId(string? value) =>
        new string((value ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '.').ToArray()).ToUpperInvariant();

    private static IEnumerable<string> Emails(string? value) =>
        (value ?? "").Split(new[] { ';', ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToLowerInvariant());

    // A real RFC returns the company with its legal representative, main phone and main email (business decision, 2.6.0).
    // An email registered on a contact returns that contact instead. Client codes are sequential, so they need a matching email.
    [HttpPost("clients/lookup")]
    public async Task<IActionResult> Lookup(ClientLookupInput input)
    {
        var id = NormalizeId(input.Rfc);
        var email = (input.Email ?? "").Trim().ToLowerInvariant();
        var hasEmail = email.Length > 0;
        if (id.Length is < 3 or > 20 || email.Length > 256 || (hasEmail && !new EmailAddressAttribute().IsValid(email)))
            return BadRequest(new { message = "RFC o correo inválido." });
        var isRfc = RfcPattern().IsMatch(id);
        if (!isRfc && !hasEmail)
            return BadRequest(new { message = "Escriba un RFC válido, o su número de cliente junto con su correo registrado." });

        var candidates = await db.Clients.AsNoTracking().Include(x => x.Contacts)
            .Where(x => x.IsActive && !x.IsTemporaryLead
                && ((x.Rfc ?? "").Replace(" ", "").Replace("-", "").Replace(".", "").ToUpper() == id
                    || (hasEmail && x.ClientCode.Replace(" ", "").Replace("-", "").Replace(".", "").ToUpper() == id)))
            .OrderBy(x => x.Name).Take(5).ToListAsync();

        if (hasEmail)
        {
            foreach (var client in candidates)
            {
                var contact = client.Contacts.FirstOrDefault(c => Emails(c.Email).Contains(email));
                var ownEmail = Emails(client.Email).Concat(Emails(client.BillingEmail)).Concat(Emails(client.LegalEmail)).Contains(email);
                if (contact == null && !ownEmail) continue;
                if (contact != null)
                    return Ok(new { found = true, contactVerified = true, client = Dto(client,
                        contact.Name is { Length: > 0 } n ? n : LegalContact(client), contact.Role ?? "",
                        contact.Phone is { Length: > 0 } p ? p : client.Phone ?? "", email) });
                return Ok(new { found = true, contactVerified = true, client = Dto(client,
                    LegalContact(client), client.LegalPosition ?? "", client.Phone ?? "", email) });
            }
        }

        var byRfc = isRfc ? candidates.FirstOrDefault(x => NormalizeId(x.Rfc) == id) : null;
        if (byRfc != null)
            return Ok(new { found = true, contactVerified = false, client = Dto(byRfc,
                LegalContact(byRfc), byRfc.LegalPosition ?? "", byRfc.Phone ?? "", Emails(byRfc.Email).FirstOrDefault() ?? "") });
        return NotFound(new { found = false });
    }

    private static string LegalContact(HNControl.Web.Models.Client c) =>
        !string.IsNullOrWhiteSpace(c.LegalRepresentative) ? c.LegalRepresentative.Trim() : (c.ContactName ?? "").Trim();

    // Explicit allowlist: no fiscal address/regime, billing or legal emails, ids or contracts.
    private static LevantamientoClientDto Dto(HNControl.Web.Models.Client client, string contactName, string contactRole, string phone, string email) =>
        new(client.ClientCode, client.Name, client.Rfc ?? "", contactName, contactRole, phone, email,
            string.Join(", ", new[] { client.Municipality, client.State }.Where(x => !string.IsNullOrWhiteSpace(x))),
            client.Address ?? "", client.BusinessLine ?? "");
}

public sealed record LevantamientoClientDto(string ClientCode, string Name, string Rfc, string ContactName, string ContactRole,
    string Phone, string Email, string City, string Address, string BusinessLine);

public sealed class ClientLookupInput
{
    [Required, MaxLength(40)] public string Rfc { get; set; } = "";
    [MaxLength(256)] public string? Email { get; set; }
}
