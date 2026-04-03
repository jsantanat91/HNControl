using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    public SelectList KindItems =>
        new(Enum.GetValues<ClientKind>().Select(k => new { Id = k, Name = k.ToString() }), "Id", "Name");

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public ClientKind Kind { get; set; } = ClientKind.Moral;

        [Required, MaxLength(200)]
        public string Name { get; set; } = "";

        [MaxLength(13)]
        public string Rfc { get; set; } = "";

        [EmailAddress, MaxLength(256)]
        public string Email { get; set; } = "";

        [MaxLength(40)]
        public string Phone { get; set; } = "";

        [MaxLength(120)]
        public string ContactName { get; set; } = "";

        [MaxLength(400)]
        public string Address { get; set; } = "";

        [Required, MaxLength(160)]
        public string LegalRepresentative { get; set; } = "";

        [Required, EmailAddress, MaxLength(256)]
        public string LegalEmail { get; set; } = "";

        [MaxLength(120)]
        public string LegalPosition { get; set; } = "";

        [Required, MaxLength(180)]
        public string BusinessLine { get; set; } = "";

        [Required, EmailAddress, MaxLength(256)]
        public string BillingEmail { get; set; } = "";

        [Required, MaxLength(400)]
        public string FiscalAddress { get; set; } = "";

        [MaxLength(10)]
        public string FiscalZipCode { get; set; } = "";

        [MaxLength(4)]
        public string FiscalRegimeCode { get; set; } = "601";

        [MaxLength(4)]
        public string CfdiUseCodeDefault { get; set; } = "G03";
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var client = new Client
        {
            ClientCode = await NextClientCodeAsync(),
            Kind = Input.Kind,
            Name = Input.Name.Trim(),
            Rfc = (Input.Rfc ?? "").Trim(),
            Email = (Input.Email ?? "").Trim(),
            Phone = (Input.Phone ?? "").Trim(),
            ContactName = (Input.ContactName ?? "").Trim(),
            Address = (Input.Address ?? "").Trim(),
            LegalRepresentative = (Input.LegalRepresentative ?? "").Trim(),
            LegalEmail = (Input.LegalEmail ?? "").Trim(),
            LegalPosition = (Input.LegalPosition ?? "").Trim(),
            BusinessLine = (Input.BusinessLine ?? "").Trim(),
            BillingEmail = (Input.BillingEmail ?? "").Trim(),
            FiscalAddress = (Input.FiscalAddress ?? "").Trim(),
            FiscalZipCode = (Input.FiscalZipCode ?? "").Trim(),
            FiscalRegimeCode = (Input.FiscalRegimeCode ?? "").Trim().ToUpperInvariant(),
            CfdiUseCodeDefault = (Input.CfdiUseCodeDefault ?? "").Trim().ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Clients/Details", new { id = client.Id });
    }

    private async Task<string> NextClientCodeAsync()
    {
        var codes = await _db.Clients
            .AsNoTracking()
            .Where(c => !string.IsNullOrWhiteSpace(c.ClientCode) && c.ClientCode.StartsWith("HN-"))
            .Select(c => c.ClientCode)
            .ToListAsync();

        var max = 0;
        foreach (var code in codes)
        {
            if (int.TryParse(code.AsSpan(3), out var n) && n > max)
                max = n;
        }

        return $"HN-{max + 1:0000}";
    }
}


