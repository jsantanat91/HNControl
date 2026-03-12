using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HNControl.Web.Services;

namespace HNControl.Web.Pages.Viaticos;

[Authorize(Roles = AppRoles.Employee + "," + AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;

    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr)
    {
        _db = db;
        _userMgr = userMgr;
    }

    [BindProperty]
    public DateTime AnyDayInWeek { get; set; } = DateTime.Today;

    [BindProperty]
    public DateTime TravelAnyDayInWeek { get; set; } = DateTime.Today;

    [BindProperty(SupportsGet = true)]
    public Guid? TravelClientId { get; set; }

    [BindProperty]
    public Guid? RelatedServiceOrderId { get; set; }

    [BindProperty]
    public string TripDestination { get; set; } = "";

    [BindProperty]
    public string TripPurpose { get; set; } = "";

    [BindProperty]
    public decimal RequestedAdvanceAmount { get; set; }

    [BindProperty(SupportsGet = true)] public DateTime? DateFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? DateTo { get; set; }
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;

    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public string? Error { get; set; }

    public List<SelectListItem> ServiceOrderOptions { get; set; } = new();
    public List<SelectListItem> ClientOptions { get; set; } = new();

    public List<WeekRow> Weeks { get; set; } = new();

    public record WeekRow(Guid Id, DateTime WeekStartDate, ViaticWeekStatus Status, ViaticFlowType FlowType, decimal Total);

    public async Task OnGetAsync()
    {
        var userId = _userMgr.GetUserId(User)!;

        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        Page = Page < 1 ? 1 : Page;

        await LoadClientAndOrderOptionsAsync(userId, TravelClientId);

        var q = _db.ViaticWeeks.Where(w => w.UserId == userId);

        if (DateFrom.HasValue)
        {
            var from = DateFrom.Value.Date;
            q = q.Where(w => w.WeekStartDate.Date >= from);
        }

        if (DateTo.HasValue)
        {
            var to = DateTo.Value.Date;
            q = q.Where(w => w.WeekStartDate.Date <= to);
        }

        TotalCount = await q.CountAsync();

        Weeks = await q
            .OrderByDescending(w => w.CreatedAt)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .Select(w => new WeekRow(
                w.Id,
                w.WeekStartDate,
                w.Status,
                w.FlowType,
                w.Entries.Sum(e => e.Amount)
            ))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _userMgr.GetUserId(User)!;

        var monday = ToMonday(AnyDayInWeek);
        var exists = await _db.ViaticWeeks
            .FirstOrDefaultAsync(w => w.UserId == userId && w.FlowType == ViaticFlowType.Weekly && w.WeekStartDate == monday);

        if (exists == null)
        {
            exists = new ViaticWeek
            {
                UserId = userId,
                WeekStartDate = monday,
                FlowType = ViaticFlowType.Weekly,
                Status = ViaticWeekStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ViaticWeeks.Add(exists);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage("/Viaticos/Week", new { id = exists.Id });
    }

    public async Task<IActionResult> OnPostCreateTravelAdvanceAsync()
    {
        var userId = _userMgr.GetUserId(User)!;

        if (RequestedAdvanceAmount <= 0m)
        {
            Error = "El monto solicitado debe ser mayor a 0.";
            await LoadPageDataForPostAsync(userId);
            return Page();
        }

        if (string.IsNullOrWhiteSpace(TripDestination) || string.IsNullOrWhiteSpace(TripPurpose))
        {
            Error = "Destino y motivo del viaje son obligatorios.";
            await LoadPageDataForPostAsync(userId);
            return Page();
        }

        if (TravelClientId.HasValue)
        {
            var validClient = await _db.Clients.AnyAsync(c => c.Id == TravelClientId.Value);
            if (!validClient)
            {
                Error = "El cliente seleccionado no existe.";
                await LoadPageDataForPostAsync(userId);
                return Page();
            }
        }

        if (RelatedServiceOrderId.HasValue)
        {
            var validOrder = await _db.ServiceOrders
                .Where(o => o.Id == RelatedServiceOrderId.Value)
                .Where(o => !TravelClientId.HasValue || o.ClientId == TravelClientId.Value)
                .AnyAsync();
            if (!validOrder)
            {
                Error = "La orden seleccionada no existe para el cliente elegido.";
                await LoadPageDataForPostAsync(userId);
                return Page();
            }
        }

        var monday = ToMonday(TravelAnyDayInWeek);

        var travelWeek = new ViaticWeek
        {
            UserId = userId,
            WeekStartDate = monday,
            FlowType = ViaticFlowType.TravelAdvance,
            Status = ViaticWeekStatus.Draft,
            RelatedServiceOrderId = RelatedServiceOrderId,
            TripDestination = (TripDestination ?? "").Trim(),
            TripPurpose = (TripPurpose ?? "").Trim(),
            RequestedAdvanceAmount = RequestedAdvanceAmount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ViaticWeeks.Add(travelWeek);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Viaticos/Week", new { id = travelWeek.Id });
    }

    private async Task LoadClientAndOrderOptionsAsync(string userId, Guid? selectedClientId)
    {
        var clientsQ = _db.Clients.AsQueryable();

        var clients = await clientsQ
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.ClientCode })
            .ToListAsync();

        ClientOptions = new List<SelectListItem>
        {
            new("Sin cliente", "")
        };
        ClientOptions.AddRange(clients.Select(c =>
            new SelectListItem($"{c.Name} ({c.ClientCode})", c.Id.ToString())));

        ServiceOrderOptions = new List<SelectListItem>
        {
            new("Sin orden", "")
        };

        if (!selectedClientId.HasValue)
        {
            return;
        }

        var ordersQ = _db.ServiceOrders
            .Include(o => o.Client)
            .Where(o => o.ClientId == selectedClientId.Value)
            .AsQueryable();

        var orders = await ordersQ
            .OrderByDescending(o => o.CreatedAt)
            .Take(200)
            .Select(o => new
            {
                o.Id,
                o.Title,
                ClientName = o.Client != null ? o.Client.Name : "Cliente",
                o.Status
            })
            .ToListAsync();

        ServiceOrderOptions.AddRange(orders.Select(o =>
            new SelectListItem($"{o.Title} · {o.ClientName} · {o.Status.GetDisplayName()}", o.Id.ToString())));
    }

    private async Task LoadPageDataForPostAsync(string userId)
    {
        PageSize = PageSize is 10 or 20 or 50 or 100 ? PageSize : 20;
        Page = Page < 1 ? 1 : Page;

        await LoadClientAndOrderOptionsAsync(userId, TravelClientId);

        var q = _db.ViaticWeeks.Where(w => w.UserId == userId);

        if (DateFrom.HasValue)
        {
            var from = DateFrom.Value.Date;
            q = q.Where(w => w.WeekStartDate.Date >= from);
        }

        if (DateTo.HasValue)
        {
            var to = DateTo.Value.Date;
            q = q.Where(w => w.WeekStartDate.Date <= to);
        }

        TotalCount = await q.CountAsync();

        Weeks = await q
            .OrderByDescending(w => w.CreatedAt)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .Select(w => new WeekRow(
                w.Id,
                w.WeekStartDate,
                w.Status,
                w.FlowType,
                w.Entries.Sum(e => e.Amount)
            ))
            .ToListAsync();
    }

    private static DateTime ToMonday(DateTime anyDay)
    {
        var d = anyDay.Date;
        var diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return d.AddDays(-diff);
    }
}
