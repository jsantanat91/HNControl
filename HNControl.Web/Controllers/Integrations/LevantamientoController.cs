using System.ComponentModel.DataAnnotations;
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
public class LevantamientoController(ApplicationDbContext db) : ControllerBase
{
    public static string NormalizeId(string? value) =>
        new string((value ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '.').ToArray()).ToUpperInvariant();

    private static IEnumerable<string> Emails(string? value) =>
        (value ?? "").Split(new[] { ';', ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToLowerInvariant());

    // The caller must prove RFC/code AND an email registered on the same client; mismatches are indistinguishable.
    [HttpPost("clients/lookup")]
    public async Task<IActionResult> Lookup(ClientLookupInput input)
    {
        var id = NormalizeId(input.Rfc);
        var email = (input.Email ?? "").Trim().ToLowerInvariant();
        if (id.Length is < 3 or > 20 || email.Length > 256 || !new EmailAddressAttribute().IsValid(email))
            return BadRequest(new { message = "RFC o correo inválido." });

        var candidates = await db.Clients.AsNoTracking().Include(x => x.Contacts)
            .Where(x => x.IsActive && !x.IsTemporaryLead
                && ((x.Rfc ?? "").Replace(" ", "").Replace("-", "").Replace(".", "").ToUpper() == id
                    || x.ClientCode.Replace(" ", "").Replace("-", "").Replace(".", "").ToUpper() == id))
            .OrderBy(x => x.Name).Take(5).ToListAsync();

        foreach (var client in candidates)
        {
            var contact = client.Contacts.FirstOrDefault(c => Emails(c.Email).Contains(email));
            var ownEmail = Emails(client.Email).Concat(Emails(client.BillingEmail)).Concat(Emails(client.LegalEmail)).Contains(email);
            if (contact == null && !ownEmail) continue;

            // Explicit allowlist: no fiscal data, legal representative, other emails or contracts.
            var city = string.Join(", ", new[] { client.Municipality, client.State }.Where(x => !string.IsNullOrWhiteSpace(x)));
            return Ok(new {
                found = true,
                client = new LevantamientoClientDto(
                    client.ClientCode, client.Name, client.Rfc ?? "",
                    contact?.Name is { Length: > 0 } n ? n : client.ContactName ?? "",
                    contact?.Role ?? "",
                    contact?.Phone is { Length: > 0 } p ? p : client.Phone ?? "",
                    city, client.Address ?? "", client.BusinessLine ?? "")
            });
        }
        return NotFound(new { found = false });
    }
}

public sealed record LevantamientoClientDto(string ClientCode, string Name, string Rfc, string ContactName, string ContactRole,
    string Phone, string City, string Address, string BusinessLine);

public sealed class ClientLookupInput
{
    [Required, MaxLength(40)] public string Rfc { get; set; } = "";
    [Required, MaxLength(256)] public string Email { get; set; } = "";
}
