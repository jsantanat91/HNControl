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
public partial class LevantamientoController(ApplicationDbContext db) : ControllerBase
{
    // RFC persona moral (12) o física (13): letras/&/Ñ, fecha AAMMDD y homoclave.
    [GeneratedRegex(@"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$")]
    private static partial Regex RfcPattern();

    public static string NormalizeId(string? value) =>
        new string((value ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '.').ToArray()).ToUpperInvariant();

    private static IEnumerable<string> Emails(string? value) =>
        (value ?? "").Split(new[] { ';', ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToLowerInvariant());

    // Company data needs a real RFC; contact data (personal data) also needs an email registered on that client.
    // Client codes are sequential, so they are accepted only together with a matching email.
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
                return Ok(new { found = true, contactVerified = true, client = Dto(client,
                    contact?.Name is { Length: > 0 } n ? n : client.ContactName ?? "",
                    contact?.Role ?? "",
                    contact?.Phone is { Length: > 0 } p ? p : client.Phone ?? "") });
            }
        }

        // RFC only (or email that does not match): company-level data, never contact data.
        var byRfc = isRfc ? candidates.FirstOrDefault(x => NormalizeId(x.Rfc) == id) : null;
        if (byRfc != null)
            return Ok(new { found = true, contactVerified = false, client = Dto(byRfc, "", "", "") });
        return NotFound(new { found = false });
    }

    // Explicit allowlist: no fiscal address/regime, legal representative, emails, ids or contracts.
    private static LevantamientoClientDto Dto(HNControl.Web.Models.Client client, string contactName, string contactRole, string phone) =>
        new(client.ClientCode, client.Name, client.Rfc ?? "", contactName, contactRole, phone,
            string.Join(", ", new[] { client.Municipality, client.State }.Where(x => !string.IsNullOrWhiteSpace(x))),
            client.Address ?? "", client.BusinessLine ?? "");
}

public sealed record LevantamientoClientDto(string ClientCode, string Name, string Rfc, string ContactName, string ContactRole,
    string Phone, string City, string Address, string BusinessLine);

public sealed class ClientLookupInput
{
    [Required, MaxLength(40)] public string Rfc { get; set; } = "";
    [MaxLength(256)] public string? Email { get; set; }
}
