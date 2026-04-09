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
    [BindProperty(SupportsGet = true)] public string Month { get; set; } = DateTime.UtcNow.ToString("yyyy-MM");
    [BindProperty] public LeadInput Lead { get; set; } = new();
    [BindProperty] public EditLeadInput EditLead { get; set; } = new();
    [TempData] public string? UiMessage { get; set; }

    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool CanViewAll { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanConvert { get; set; }
    public bool IsSuperAdmin { get; set; }

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

    public class EditLeadInput : LeadInput
    {
        public Guid Id { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public record ProspectQuoteVm(
        Guid Id,
        string Folio,
        string Status,
        decimal Total,
        DateTime CreatedAtUtc);

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
            return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize, Month });

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
            existing.OwnerUserId ??= userId;
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
                OwnerUserId = userId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize, Month });
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id)
    {
        if (!await EnsurePermissionsAsync() || !CanEdit)
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var lead = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id && x.IsTemporaryLead);
        if (lead == null)
            return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize, Month });
        if (!CanViewAll && !string.Equals(lead.CreatedByUserId, userId, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        lead.IsActive = !lead.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize, Month });
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        if (!await EnsurePermissionsAsync() || !CanEdit)
            return Forbid();

        if (EditLead.Id == Guid.Empty)
            return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize, Month });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var lead = await _db.Clients.FirstOrDefaultAsync(x => x.Id == EditLead.Id && x.IsTemporaryLead);
        if (lead == null)
            return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize, Month });
        if (!CanViewAll && !string.Equals(lead.CreatedByUserId, userId, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var contactName = (EditLead.ContactName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(contactName))
            return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize, Month });

        var company = string.IsNullOrWhiteSpace(EditLead.CompanyName) ? contactName : EditLead.CompanyName!.Trim();
        lead.Name = company;
        lead.ContactName = contactName;
        lead.Email = (EditLead.Email ?? "").Trim().ToLowerInvariant();
        lead.Phone = (EditLead.Phone ?? "").Trim();
        lead.Address = (EditLead.Location ?? "").Trim();
        lead.IsActive = EditLead.IsActive;
        lead.CreatedByUserId ??= userId;
        await _db.SaveChangesAsync();

        return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize, Month });
    }

    public async Task<IActionResult> OnPostConvertAsync(Guid id)
    {
        if (!await EnsurePermissionsAsync() || !IsSuperAdmin)
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var lead = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id && x.IsTemporaryLead);
        if (lead == null)
            return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize, Month });
        if (!CanViewAll && !string.Equals(lead.CreatedByUserId, userId, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        lead.IsTemporaryLead = false;
        lead.IsActive = true;
        lead.ConvertedToFormalAt = DateTime.UtcNow;
        lead.ClientCode = await NextFormalClientCodeAsync();
        lead.OwnerUserId ??= lead.CreatedByUserId;

        await _db.SaveChangesAsync();
        return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize, Month });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        if (!await EnsurePermissionsAsync() || !CanEdit)
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var lead = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id && x.IsTemporaryLead);
        if (lead == null)
            return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize, Month });
        if (!CanViewAll && !string.Equals(lead.CreatedByUserId, userId, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        try
        {
            _db.Clients.Remove(lead);
            await _db.SaveChangesAsync();
            UiMessage = "Prospecto eliminado.";
        }
        catch (DbUpdateException)
        {
            // Si está ligado a cotizaciones/tickets u otras referencias, no tronamos:
            // lo dejamos inactivo para evitar uso operativo.
            lead.IsActive = false;
            await _db.SaveChangesAsync();
            UiMessage = "El prospecto tiene datos ligados; se desactivó en lugar de eliminarse.";
        }

        return RedirectToPage("/Sales/Prospects", new { Name, Page, PageSize, Month });
    }

    public async Task<IActionResult> OnGetProspectQuotesAsync(Guid prospectId, string? month, int page = 1)
    {
        if (!await EnsurePermissionsAsync())
            return new JsonResult(new { ok = false, message = "Sin permiso." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var lead = await _db.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == prospectId && x.IsTemporaryLead);
        if (lead == null)
            return new JsonResult(new { ok = false, message = "Prospecto no encontrado." });
        if (!CanViewAll && !string.Equals(lead.CreatedByUserId, userId, StringComparison.OrdinalIgnoreCase))
            return new JsonResult(new { ok = false, message = "Sin acceso al prospecto." });

        var (fromUtc, toUtc) = ResolveMonthRange(month);
        const int modalPageSize = 20;
        page = page < 1 ? 1 : page;

        var quotesQuery = _db.QuoteRequests
            .AsNoTracking()
            .Where(x =>
                x.ClientId == prospectId
                && x.CreatedAt >= fromUtc
                && x.CreatedAt < toUtc
                && (x.Status == QuoteRequestStatus.New
                    || x.Status == QuoteRequestStatus.Emailed
                    || x.Status == QuoteRequestStatus.EmailError));

        var total = await quotesQuery.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)modalPageSize));
        if (page > totalPages) page = totalPages;

        var items = await quotesQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * modalPageSize)
            .Take(modalPageSize)
            .Select(x => new ProspectQuoteVm(
                x.Id,
                x.Folio,
                x.Status.ToString(),
                x.EstimatedTotal ?? x.SubtotalAuto,
                x.CreatedAt))
            .ToListAsync();

        return new JsonResult(new
        {
            ok = true,
            prospect = new { id = lead.Id, name = lead.Name, code = lead.ClientCode },
            month = fromUtc.ToString("yyyy-MM"),
            pagination = new { page, pageSize = modalPageSize, total, totalPages },
            quotes = items.Select(x => new
            {
                id = x.Id,
                folio = x.Folio,
                status = x.Status,
                statusLabel = QuoteStatusLabel(x.Status),
                total = x.Total,
                createdAt = x.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            })
        });
    }

    private async Task<bool> EnsurePermissionsAsync()
    {
        IsSuperAdmin = AppRoles.IsGlobalAdmin(User);
        CanViewAll = IsSuperAdmin;
        var canView = CanViewAll || await _actions.HasActionAsync(User, AppActions.SalesProspectsView);
        if (!canView)
            return false;

        CanCreate = CanViewAll || await _actions.HasActionAsync(User, AppActions.SalesProspectsCreate);
        CanEdit = CanViewAll || await _actions.HasActionAsync(User, AppActions.SalesProspectsEdit);
        // Convertir a cliente: exclusivo super admin.
        CanConvert = IsSuperAdmin;
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

    private static (DateTime fromUtc, DateTime toUtc) ResolveMonthRange(string? month)
    {
        if (!DateTime.TryParse($"{month}-01", out var parsed))
            parsed = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var from = DateTime.SpecifyKind(new DateTime(parsed.Year, parsed.Month, 1), DateTimeKind.Utc);
        return (from, from.AddMonths(1));
    }

    private static string QuoteStatusLabel(string status) => status switch
    {
        nameof(QuoteRequestStatus.New) => "Activa",
        nameof(QuoteRequestStatus.Emailed) => "Activa",
        nameof(QuoteRequestStatus.EmailError) => "Activa (error envío)",
        nameof(QuoteRequestStatus.Accepted) => "Aceptada",
        nameof(QuoteRequestStatus.Rejected) => "Rechazada",
        _ => status
    };
}
