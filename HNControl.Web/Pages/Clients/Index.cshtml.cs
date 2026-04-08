using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IActionAccessService _actions;
    public IndexModel(ApplicationDbContext db, IActionAccessService actions)
    {
        _db = db;
        _actions = actions;
    }

    [BindProperty(SupportsGet = true)] public string? Name { get; set; }
    [BindProperty(SupportsGet = true)] public string View { get; set; } = "normal";
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty] public LeadInput Lead { get; set; } = new();

    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool CanEdit { get; set; }
    public bool CanCreateLead { get; set; }
    public bool CanDelete { get; set; }
    public bool IsSuperAdmin { get; set; }

    public record Row(
        Guid Id,
        string ClientCode,
        string Name,
        string Rfc,
        string Kind,
        string Email,
        string ContractsSummary,
        DateTime CreatedAt,
        bool IsActive,
        bool IsTemporaryLead,
        bool IsConvertedFromLead);
    public List<Row> Rows { get; set; } = new();

    public class LeadInput
    {
        public string ContactName { get; set; } = "";
        public string? CompanyName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Location { get; set; }
        public string? Notes { get; set; }
    }

    public async Task OnGetAsync()
    {
        IsSuperAdmin = AppRoles.IsGlobalAdmin(User);
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        var showLeads = string.Equals(View, "leads", StringComparison.OrdinalIgnoreCase);
        if (showLeads)
        {
            CanEdit = IsSuperAdmin || await _actions.HasActionAsync(User, AppActions.SalesProspectsEdit);
            CanCreateLead = IsSuperAdmin || await _actions.HasActionAsync(User, AppActions.SalesProspectsCreate);
            CanDelete = CanEdit;
        }
        else
        {
            CanEdit = IsSuperAdmin || await _actions.HasActionAsync(User, AppActions.ClientsEdit);
            CanCreateLead = false;
            CanDelete = CanEdit;
        }

        await EnsureClientCodesAsync();

        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        Page = Page < 1 ? 1 : Page;

        var today = DateTime.Today;
        var soon = today.AddDays(30);

        var q = _db.Clients
            .Include(c => c.Contracts)
            .AsQueryable();

        q = q.Where(c => c.IsTemporaryLead == showLeads);
        if (showLeads && !AppRoles.IsGlobalAdmin(User))
            q = q.Where(c => c.CreatedByUserId == userId);

        var name = (Name ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(c => c.Name.ToLower().Contains(name.ToLower()));

        TotalCount = await q.CountAsync();

        var clients = await q
            .OrderByDescending(c => c.CreatedAt)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        Rows = clients.Select(c =>
        {
            var total = c.Contracts.Count;
            var expSoon = c.Contracts.Count(x => x.ContractEndDate.HasValue && x.ContractEndDate.Value.Date <= soon);

            var top = c.Contracts
                .GroupBy(x => x.ServiceType)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.Key.ToString())
                .ToList();

            var summary = total == 0
                ? "-"
                : $"{total} contrato(s)" + (expSoon > 0 ? $" · {expSoon} por vencer" : "") + (top.Any() ? $" · {string.Join(", ", top)}" : "");

            return new Row(
                c.Id,
                c.ClientCode,
                c.Name,
                c.Rfc ?? "",
                c.Kind.ToString(),
                c.Email ?? "",
                summary,
                c.CreatedAt,
                c.IsActive,
                c.IsTemporaryLead,
                c.ConvertedToFormalAt.HasValue
            );
        }).ToList();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(Guid id, string? name, string? view, int page = 1, int pageSize = 20)
    {
        var leads = string.Equals(view, "leads", StringComparison.OrdinalIgnoreCase);
        var action = leads ? AppActions.SalesProspectsEdit : AppActions.ClientsEdit;
        var canEdit = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, action);
        if (!canEdit) return Forbid();

        var client = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id);
        if (client == null) return RedirectToPage("/Clients/Index", new { Name = name, View = view, Page = page, PageSize = pageSize });
        client.IsActive = !client.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToPage("/Clients/Index", new { Name = name, View = view, Page = page, PageSize = pageSize });
    }

    public async Task<IActionResult> OnPostCreateLeadAsync(string? name, string? view, int page = 1, int pageSize = 20)
    {
        var canCreate = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.SalesProspectsCreate);
        if (!canCreate) return Forbid();
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

        var contactName = (Lead.ContactName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(contactName))
            return RedirectToPage("/Clients/Index", new { Name = name, View = "leads", Page = page, PageSize = pageSize });

        var email = (Lead.Email ?? "").Trim().ToLowerInvariant();
        var phone = (Lead.Phone ?? "").Trim();
        var location = (Lead.Location ?? "").Trim();
        var company = string.IsNullOrWhiteSpace(Lead.CompanyName) ? contactName : Lead.CompanyName.Trim();

        var existing = !string.IsNullOrWhiteSpace(email)
            ? await _db.Clients.FirstOrDefaultAsync(x => x.IsTemporaryLead && x.Email != null && x.Email.ToLower() == email)
            : null;

        if (existing != null)
        {
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
        return RedirectToPage("/Clients/Index", new { Name = name, View = "leads", Page = page, PageSize = pageSize });
    }

    public async Task<IActionResult> OnPostConvertLeadToFormalAsync(Guid id, string? name, string? view, int page = 1, int pageSize = 20)
    {
        // Convertir prospecto a cliente: exclusivo super admin.
        if (!AppRoles.IsGlobalAdmin(User)) return Forbid();

        var lead = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id);
        if (lead == null)
            return RedirectToPage("/Clients/Index", new { Name = name, View = view, Page = page, PageSize = pageSize });
        if (!lead.IsTemporaryLead)
            return RedirectToPage("/Clients/Index", new { Name = name, View = view, Page = page, PageSize = pageSize });

        lead.IsTemporaryLead = false;
        lead.IsActive = true;
        lead.ConvertedToFormalAt = DateTime.UtcNow;
        lead.ClientCode = await NextFormalClientCodeAsync();

        await _db.SaveChangesAsync();
        return RedirectToPage("/Clients/Index", new { Name = name, View = "leads", Page = page, PageSize = pageSize });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, string? name, string? view, int page = 1, int pageSize = 20)
    {
        var leads = string.Equals(view, "leads", StringComparison.OrdinalIgnoreCase);
        var action = leads ? AppActions.SalesProspectsEdit : AppActions.ClientsEdit;
        var canDelete = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, action);
        if (!canDelete) return Forbid();

        var client = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id);
        if (client == null)
            return RedirectToPage("/Clients/Index", new { Name = name, View = view, Page = page, PageSize = pageSize });

        // Evitar borrado si ya tiene operación histórica ligada.
        var hasBlockingRelations =
            await _db.ClientServiceContracts.AnyAsync(x => x.ClientId == id) ||
            await _db.Projects.AnyAsync(x => x.ClientId == id) ||
            await _db.Tickets.AnyAsync(x => x.ClientId == id) ||
            await _db.ServiceOrders.AnyAsync(x => x.ClientId == id) ||
            await _db.QuoteRequests.AnyAsync(x => x.ClientId == id) ||
            await _db.MonitorTargets.AnyAsync(x => x.ClientId == id) ||
            await _db.BillingInvoicePlans.AnyAsync(x => x.ClientId == id) ||
            await _db.ClientCarrierServices.AnyAsync(x => x.ClientId == id) ||
            await _db.ProjectDeliveryFormats.AnyAsync(x => x.ClientId == id);

        if (hasBlockingRelations)
        {
            TempData["Error"] = "No se puede eliminar el cliente porque tiene contratos/proyectos/tickets u operación ligada.";
            return RedirectToPage("/Clients/Index", new { Name = name, View = view, Page = page, PageSize = pageSize });
        }

        _db.Clients.Remove(client);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Cliente eliminado.";
        return RedirectToPage("/Clients/Index", new { Name = name, View = view, Page = page, PageSize = pageSize });
    }

    private async Task EnsureClientCodesAsync()
    {
        var clients = await _db.Clients.OrderBy(c => c.CreatedAt).ToListAsync();
        var used = new HashSet<int>();
        var max = 0;

        foreach (var c in clients)
        {
            if (!string.IsNullOrWhiteSpace(c.ClientCode) &&
                c.ClientCode.StartsWith("HN-") &&
                !c.ClientCode.StartsWith("HN-VENTA-") &&
                int.TryParse(c.ClientCode.AsSpan(3), out var n))
            {
                used.Add(n);
                if (n > max) max = n;
            }
        }

        var changed = false;
        foreach (var c in clients.Where(c => string.IsNullOrWhiteSpace(c.ClientCode) && !c.IsTemporaryLead))
        {
            do { max++; } while (used.Contains(max));
            c.ClientCode = $"HN-{max:0000}";
            used.Add(max);
            changed = true;
        }

        if (changed)
            await _db.SaveChangesAsync();
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


