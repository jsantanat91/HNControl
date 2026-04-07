using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HNControl.Web.Pages.Sales;

[Authorize(Policy = "EmployeeOnly")]
public class ProspectsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IActionAccessService _actions;

    public ProspectsModel(ApplicationDbContext db, IActionAccessService actions)
    {
        _db = db;
        _actions = actions;
    }

    [BindProperty(SupportsGet = true)] public string? Name { get; set; }
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty] public LeadInput Lead { get; set; } = new();

    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool CanViewAll { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanConvert { get; set; }

    public record Row(Guid Id, string ClientCode, string Name, string ContactName, string Email, string Phone, string Location, DateTime CreatedAt, bool IsActive);
    public List<Row> Rows { get; set; } = [];

    public class LeadInput
    {
        public string ContactName { get; set; } = "";
        public string? CompanyName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Location { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await EnsurePermissionsAsync())
            return Forbid();
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!await EnsurePermissionsAsync() || !CanCreate)
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var contactName = (Lead.ContactName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(contactName))
            return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize });

        var email = (Lead.Email ?? "").Trim().ToLowerInvariant();
        var phone = (Lead.Phone ?? "").Trim();
        var location = (Lead.Location ?? "").Trim();
        var company = string.IsNullOrWhiteSpace(Lead.CompanyName) ? contactName : Lead.CompanyName!.Trim();

        var existing = !string.IsNullOrWhiteSpace(email)
            ? await _db.Clients.FirstOrDefaultAsync(x => x.IsTemporaryLead && x.Email != null && x.Email.ToLower() == email)
            : null;

        if (existing != null)
        {
            if (!CanViewAll && !string.Equals(existing.CreatedByUserId, userId, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            existing.Name = company;
            existing.ContactName = contactName;
            existing.Phone = phone;
            existing.Address = location;
            existing.IsActive = true;
            existing.CreatedByUserId ??= userId;
        }
        else
        {
            _db.Clients.Add(new Client
            {
                ClientCode = await NextLeadCodeAsync(),
                Name = company,
                Type = ClientType.Moral,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                Phone = phone,
                ContactName = contactName,
                Address = location,
                IsTemporaryLead = true,
                IsActive = true,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize });
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id)
    {
        if (!await EnsurePermissionsAsync() || !CanEdit)
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var lead = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id && x.IsTemporaryLead);
        if (lead == null)
            return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize });
        if (!CanViewAll && !string.Equals(lead.CreatedByUserId, userId, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        lead.IsActive = !lead.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize });
    }

    public async Task<IActionResult> OnPostConvertAsync(Guid id)
    {
        if (!await EnsurePermissionsAsync() || !CanConvert)
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var lead = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id && x.IsTemporaryLead);
        if (lead == null)
            return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize });
        if (!CanViewAll && !string.Equals(lead.CreatedByUserId, userId, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        lead.IsTemporaryLead = false;
        lead.IsActive = true;
        lead.ConvertedToFormalAt = DateTime.UtcNow;
        lead.ClientCode = await NextFormalClientCodeAsync();

        await _db.SaveChangesAsync();
        return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize });
    }

    private async Task<bool> EnsurePermissionsAsync()
    {
        CanViewAll = AppRoles.IsGlobalAdmin(User);
        var canView = CanViewAll || await _actions.HasActionAsync(User, AppActions.SalesProspectsView);
        if (!canView)
            return false;

        CanCreate = CanViewAll || await _actions.HasActionAsync(User, AppActions.SalesProspectsCreate);
        CanEdit = CanViewAll || await _actions.HasActionAsync(User, AppActions.SalesProspectsEdit);
        CanConvert = CanViewAll || await _actions.HasActionAsync(User, AppActions.SalesProspectsConvert);
        return true;
    }

    private async Task LoadAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        Page = Page < 1 ? 1 : Page;

        var q = _db.Clients.AsNoTracking().Where(c => c.IsTemporaryLead);
        if (!CanViewAll)
            q = q.Where(c => c.CreatedByUserId == userId);

        var name = (Name ?? "").Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(name))
        {
            q = q.Where(c =>
                c.Name.ToLower().Contains(name)
                || (c.ContactName ?? "").ToLower().Contains(name)
                || (c.Email ?? "").ToLower().Contains(name));
        }

        TotalCount = await q.CountAsync();
        Rows = await q
            .OrderByDescending(c => c.CreatedAt)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .Select(c => new Row(
                c.Id,
                c.ClientCode,
                c.Name,
                c.ContactName ?? "-",
                c.Email ?? "-",
                c.Phone ?? "-",
                c.Address ?? "-",
                c.CreatedAt,
                c.IsActive))
            .ToListAsync();
    }

    private async Task<string> NextLeadCodeAsync()
    {
        var codes = await _db.Clients
            .AsNoTracking()
            .Where(c => c.IsTemporaryLead && !string.IsNullOrWhiteSpace(c.ClientCode) && c.ClientCode.StartsWith("HN-VENTA-"))
            .Select(c => c.ClientCode)
            .ToListAsync();

        var max = 0;
        foreach (var code in codes)
        {
            var suffix = code["HN-VENTA-".Length..];
            if (int.TryParse(suffix, out var n) && n > max)
                max = n;
        }
        return $"HN-VENTA-{max + 1:00}";
    }

    private async Task<string> NextFormalClientCodeAsync()
    {
        var codes = await _db.Clients
            .AsNoTracking()
            .Where(c => !c.IsTemporaryLead && !string.IsNullOrWhiteSpace(c.ClientCode) && c.ClientCode.StartsWith("HN-") && !c.ClientCode.StartsWith("HN-VENTA-"))
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
