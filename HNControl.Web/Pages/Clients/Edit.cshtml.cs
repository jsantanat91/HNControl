using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HNControl.Web.Pages.Clients;

[Authorize]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IActionAccessService _actions;
    public EditModel(ApplicationDbContext db, IActionAccessService actions)
    {
        _db = db;
        _actions = actions;
    }

    public SelectList KindItems =>
        new(Enum.GetValues<ClientKind>().Select(k => new { Id = k, Name = k.ToString() }), "Id", "Name");

    public SelectList StateItems =>
        new(MexicoGeoCatalog.States.Select(x => new { Id = x, Name = x }), "Id", "Name");

    public string MunicipalitiesByStateJson => JsonSerializer.Serialize(MexicoGeoCatalog.MunicipalitiesByState);
    public SelectList OwnerItems { get; set; } = new(Enumerable.Empty<object>(), "Id", "Name");

    [BindProperty] public InputModel? Input { get; set; }
    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public Guid Id { get; set; }
        public string ClientCode { get; set; } = "";

        [Required] public ClientKind Kind { get; set; }

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

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await CanEditAsync()) return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var canViewAll = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsView);
        var canViewOwn = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsViewOwn);

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id);
        if (client == null) return NotFound();
        if (!canViewAll && (!canViewOwn || !string.Equals(client.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase)))
            return Forbid();

        Input = new InputModel
        {
            Id = client.Id,
            ClientCode = client.ClientCode,
            Kind = client.Kind,
            Name = client.Name,
            Rfc = client.Rfc ?? "",
            Email = client.Email ?? "",
            Phone = client.Phone ?? "",
            ContactName = client.ContactName ?? "",
            Address = client.Address ?? "",
            LegalRepresentative = client.LegalRepresentative ?? "",
            State = client.State ?? "",
            Municipality = client.Municipality ?? "",
            BusinessLine = client.BusinessLine ?? "",
            BillingEmail = client.BillingEmail ?? "",
            FiscalAddress = client.FiscalAddress ?? "",
            FiscalZipCode = client.FiscalZipCode ?? "",
            FiscalRegimeCode = client.FiscalRegimeCode ?? "601",
            CfdiUseCodeDefault = client.CfdiUseCodeDefault ?? "G03",
            OwnerUserId = client.OwnerUserId
        };
        await LoadOwnerItemsAsync(Input.OwnerUserId);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input == null) return NotFound();
        if (!await CanEditAsync()) return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var canViewAll = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsView);
        var canViewOwn = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsViewOwn);

        await LoadOwnerItemsAsync(Input.OwnerUserId);
        if (!ModelState.IsValid) return Page();

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == Input.Id);
        if (client == null) return NotFound();
        if (!canViewAll && (!canViewOwn || !string.Equals(client.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase)))
            return Forbid();

        client.Type = (ClientType)Input.Kind;
        client.Name = Input.Name.Trim();
        client.Rfc = (Input.Rfc ?? "").Trim();
        client.Email = (Input.Email ?? "").Trim();
        client.Phone = (Input.Phone ?? "").Trim();
        client.ContactName = (Input.ContactName ?? "").Trim();
        client.Address = (Input.Address ?? "").Trim();
        client.LegalRepresentative = (Input.LegalRepresentative ?? "").Trim();
        client.State = (Input.State ?? "").Trim();
        client.Municipality = (Input.Municipality ?? "").Trim();
        client.BusinessLine = (Input.BusinessLine ?? "").Trim();
        client.BillingEmail = (Input.BillingEmail ?? "").Trim();
        client.FiscalAddress = (Input.FiscalAddress ?? "").Trim();
        client.FiscalZipCode = (Input.FiscalZipCode ?? "").Trim();
        client.FiscalRegimeCode = (Input.FiscalRegimeCode ?? "").Trim().ToUpperInvariant();
        client.CfdiUseCodeDefault = (Input.CfdiUseCodeDefault ?? "").Trim().ToUpperInvariant();
        client.OwnerUserId = AppRoles.IsGlobalAdmin(User)
            ? (Input.OwnerUserId ?? "").Trim()
            : userId;

        await _db.SaveChangesAsync();
        return RedirectToPage("/Clients/Details", new { id = client.Id });
    }

    private async Task<bool> CanEditAsync()
    {
        return AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsEdit);
    }

    private async Task LoadOwnerItemsAsync(string? selectedOwnerId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var isGlobalAdmin = AppRoles.IsGlobalAdmin(User);
        var users = _db.EmployeeProfiles.AsNoTracking().Where(x => x.IsActive);
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

        var selected = isGlobalAdmin ? (selectedOwnerId ?? rows.FirstOrDefault()?.Id) : userId;
        OwnerItems = new SelectList(rows, "Id", "Name", selected);
    }
}
