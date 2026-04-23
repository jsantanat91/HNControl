using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using HNControl.Web.Services.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HNControl.Web.Pages.Clients;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IActionAccessService _actions;
    private readonly IClientPortalAccessService _portalAccess;
    public CreateModel(
        ApplicationDbContext db,
        IActionAccessService actions,
        IClientPortalAccessService portalAccess)
    {
        _db = db;
        _actions = actions;
        _portalAccess = portalAccess;
    }

    public SelectList KindItems =>
        new(Enum.GetValues<ClientKind>().Select(k => new { Id = k, Name = k.ToString() }), "Id", "Name");

    public SelectList StateItems =>
        new(MexicoGeoCatalog.States.Select(x => new { Id = x, Name = x }), "Id", "Name");

    public string MunicipalitiesByStateJson => JsonSerializer.Serialize(MexicoGeoCatalog.MunicipalitiesByState);
    public SelectList OwnerItems { get; set; } = new(Enumerable.Empty<object>(), "Id", "Name");

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

        [MaxLength(160)]
        public string LegalRepresentative { get; set; } = "";

        [Required, MaxLength(80)]
        public string State { get; set; } = "";

        [Required, MaxLength(120)]
        public string Municipality { get; set; } = "";

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

        [MaxLength(64)]
        public string? OwnerUserId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await CanEditAsync()) return Forbid();
        await LoadOwnerItemsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await CanEditAsync()) return Forbid();
        await LoadOwnerItemsAsync();
        if (!ModelState.IsValid) return Page();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var isGlobalAdmin = AppRoles.IsGlobalAdmin(User);
        var ownerUserId = (Input.OwnerUserId ?? string.Empty).Trim();
        if (!isGlobalAdmin || string.IsNullOrWhiteSpace(ownerUserId))
            ownerUserId = userId;

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
            State = (Input.State ?? "").Trim(),
            Municipality = (Input.Municipality ?? "").Trim(),
            BusinessLine = (Input.BusinessLine ?? "").Trim(),
            BillingEmail = (Input.BillingEmail ?? "").Trim(),
            FiscalAddress = (Input.FiscalAddress ?? "").Trim(),
            FiscalZipCode = (Input.FiscalZipCode ?? "").Trim(),
            FiscalRegimeCode = (Input.FiscalRegimeCode ?? "").Trim().ToUpperInvariant(),
            CfdiUseCodeDefault = (Input.CfdiUseCodeDefault ?? "").Trim().ToUpperInvariant(),
            OwnerUserId = ownerUserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync();
        await _portalAccess.EnsureForClientAsync(client.Id, userId, forceResetPassword: false);

        return RedirectToPage("/Clients/Details", new { id = client.Id });
    }

    private async Task<bool> CanEditAsync()
    {
        return AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsEdit);
    }

    private async Task LoadOwnerItemsAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var isGlobalAdmin = AppRoles.IsGlobalAdmin(User);
        var users = _db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (!isGlobalAdmin)
            users = users.Where(x => x.UserId == userId);

        var rows = await users
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                Id = x.UserId,
                Name = $"{x.FullName} · {x.Email}"
            })
            .ToListAsync();

        Input.OwnerUserId ??= rows.FirstOrDefault()?.Id ?? userId;
        OwnerItems = new SelectList(rows, "Id", "Name", Input.OwnerUserId);
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
