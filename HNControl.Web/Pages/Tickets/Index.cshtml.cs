using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Tickets;

[Authorize(Policy = "EmployeeOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ITicketFlowService _flow;

    public IndexModel(ApplicationDbContext db, ITicketFlowService flow)
    {
        _db = db;
        _flow = flow;
    }

    public bool IsAdmin => AppRoles.IsGlobalAdmin(User);
    public string StatusFilter { get; set; } = "open";
    public string Search { get; set; } = "";

    public int OpenCount { get; set; }
    public int MineCount { get; set; }
    public int BreachCount { get; set; }
    public int AutoCount { get; set; }
    public double AvgFirstResponseMinutes { get; set; }
    public double AvgResolutionHours { get; set; }
    public double SlaCompliancePercent { get; set; }

    public List<TicketVm> Items { get; set; } = new();
    public List<ClientGroupVm> Groups { get; set; } = new();
    public List<ClientPickVm> ClientOptions { get; set; } = new();
    public List<ContractPickVm> ContractOptions { get; set; } = new();
    public List<EmployeePickVm> EmployeeOptions { get; set; } = new();

    [BindProperty]
    public CreateManualInput CreateInput { get; set; } = new();

    public async Task OnGetAsync(string? status = null, string? q = null)
    {
        StatusFilter = string.IsNullOrWhiteSpace(status) ? "open" : status.Trim().ToLowerInvariant();
        Search = (q ?? "").Trim();
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostTakeAsync(Guid id)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var uname = User.Identity?.Name ?? "Usuario";
        await _flow.TryTakeAsync(id, uid, uname, IsAdmin);
        return RedirectToPage(new { status = StatusFilter, q = Search });
    }

    public async Task<IActionResult> OnPostStartAsync(Guid id)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var uname = User.Identity?.Name ?? "Usuario";
        await _flow.TryStartAsync(id, uid, uname, IsAdmin);
        return RedirectToPage(new { status = StatusFilter, q = Search });
    }

    public async Task<IActionResult> OnPostResolveAsync(Guid id, string? summary)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var uname = User.Identity?.Name ?? "Usuario";
        await _flow.TryResolveAsync(id, uid, uname, summary ?? "", IsAdmin);
        return RedirectToPage(new { status = StatusFilter, q = Search });
    }

    public async Task<IActionResult> OnPostCloseAsync(Guid id)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var uname = User.Identity?.Name ?? "Usuario";
        await _flow.TryCloseAsync(id, uid, uname, IsAdmin);
        return RedirectToPage(new { status = StatusFilter, q = Search });
    }

    public async Task<IActionResult> OnPostCreateManualAsync()
    {
        if (!IsAdmin)
            return Forbid();

        if (CreateInput.ClientId == Guid.Empty
            || string.IsNullOrWhiteSpace(CreateInput.Title)
            || string.IsNullOrWhiteSpace(CreateInput.RequesterName)
            || string.IsNullOrWhiteSpace(CreateInput.RequesterEmail)
            || string.IsNullOrWhiteSpace(CreateInput.Description))
        {
            return RedirectToPage(new { status = StatusFilter, q = Search });
        }

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var uname = User.Identity?.Name ?? "Admin";
        await _flow.CreateInternalAsync(
            CreateInput.ClientId,
            CreateInput.ContractId,
            CreateInput.RequesterName,
            CreateInput.RequesterEmail,
            CreateInput.RequesterPhone ?? "",
            CreateInput.RequesterLocation ?? "",
            CreateInput.Title,
            CreateInput.Description,
            CreateInput.Priority,
            uid,
            uname,
            string.IsNullOrWhiteSpace(CreateInput.AssignedToUserId) ? null : CreateInput.AssignedToUserId,
            string.IsNullOrWhiteSpace(CreateInput.AssignedToName) ? null : CreateInput.AssignedToName
        );

        return RedirectToPage(new { status = "open" });
    }

    public async Task<IActionResult> OnPostNoteAsync(Guid id, string? noteText, IFormFile? noteFile)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var uname = User.Identity?.Name ?? "Usuario";
        await _flow.AddNoteWithEvidenceAsync(
            id,
            uid,
            uname,
            noteText ?? "",
            noteFile,
            IsAdmin);
        return RedirectToPage(new { status = StatusFilter, q = Search });
    }

    private async Task LoadAsync()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var now = DateTime.UtcNow;

        var baseQ = _db.Tickets
            .AsNoTracking()
            .Include(t => t.Client)
            .Include(t => t.ClientServiceContract)
            .OrderByDescending(t => t.CreatedAt)
            .AsQueryable();

        OpenCount = await baseQ.CountAsync(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled);
        MineCount = await baseQ.CountAsync(t => t.AssignedToUserId == uid && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled);
        BreachCount = await baseQ.CountAsync(t => (t.SlaBreachedResponse || t.SlaBreachedResolution) && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled);
        AutoCount = await baseQ.CountAsync(t => t.Source == TicketSource.MonitoringAuto && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled);

        var from = now.AddDays(-30);
        var kpiQ = _db.Tickets.AsNoTracking()
            .Where(t => t.CreatedAt >= from && t.Status != TicketStatus.Cancelled);
        var kpiRows = await kpiQ
            .Select(t => new
            {
                t.CreatedAt,
                t.FirstResponseAt,
                t.ResolvedAt,
                t.SlaResponseDueAt,
                t.SlaResolutionDueAt
            })
            .ToListAsync();

        var withFirst = kpiRows.Where(x => x.FirstResponseAt != null).ToList();
        AvgFirstResponseMinutes = withFirst.Any()
            ? Math.Round(withFirst.Average(x => (x.FirstResponseAt!.Value - x.CreatedAt).TotalMinutes), 1)
            : 0;

        var withResolved = kpiRows.Where(x => x.ResolvedAt != null).ToList();
        AvgResolutionHours = withResolved.Any()
            ? Math.Round(withResolved.Average(x => (x.ResolvedAt!.Value - x.CreatedAt).TotalHours), 2)
            : 0;

        var closedOrResolved = kpiRows.Where(x => x.ResolvedAt != null || x.FirstResponseAt != null).ToList();
        if (closedOrResolved.Any())
        {
            var okCount = closedOrResolved.Count(x =>
                (x.FirstResponseAt == null || x.FirstResponseAt <= x.SlaResponseDueAt) &&
                (x.ResolvedAt == null || x.ResolvedAt <= x.SlaResolutionDueAt));
            SlaCompliancePercent = Math.Round((okCount * 100d) / closedOrResolved.Count, 1);
        }
        else
        {
            SlaCompliancePercent = 0;
        }

        if (!IsAdmin)
        {
            baseQ = baseQ.Where(t => t.AssignedToUserId == uid || string.IsNullOrWhiteSpace(t.AssignedToUserId) || t.CreatedByUserId == uid);
        }

        if (StatusFilter == "open")
            baseQ = baseQ.Where(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled);
        else if (StatusFilter == "mine")
            baseQ = baseQ.Where(t => t.AssignedToUserId == uid);
        else if (StatusFilter == "closed")
            baseQ = baseQ.Where(t => t.Status == TicketStatus.Closed);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.ToLowerInvariant();
            baseQ = baseQ.Where(t =>
                t.TicketNumber.ToLower().Contains(s) ||
                t.Title.ToLower().Contains(s) ||
                t.Client!.Name.ToLower().Contains(s));
        }

        Items = await baseQ.Take(250).Select(t => new TicketVm
        {
            Id = t.Id,
            TicketNumber = t.TicketNumber,
            ContractId = t.ClientServiceContractId,
            Client = t.Client != null ? t.Client.Name : "-",
            Contract = t.ClientServiceContract != null ? t.ClientServiceContract.Label : "-",
            Branch = t.ClientServiceContract != null
                ? (string.IsNullOrWhiteSpace(t.ClientServiceContract.Branch) ? "-" : t.ClientServiceContract.Branch)
                : "-",
            Title = t.Title,
            Status = t.Status,
            StatusLabel = ToStatusLabel(t.Status),
            Source = t.Source,
            Priority = t.Priority,
            PriorityLabel = ToPriorityLabel(t.Priority),
            AssignedTo = string.IsNullOrWhiteSpace(t.AssignedToName) ? "Sin asignar" : t.AssignedToName,
            CreatedAt = t.CreatedAt,
            DueResponseAt = t.SlaResponseDueAt,
            DueResolutionAt = t.SlaResolutionDueAt,
            Breach = t.SlaBreachedResponse || t.SlaBreachedResolution || (t.FirstResponseAt == null && now > t.SlaResponseDueAt) || (t.ResolvedAt == null && now > t.SlaResolutionDueAt),
            CanTake = string.IsNullOrWhiteSpace(t.AssignedToUserId) || t.AssignedToUserId == uid || IsAdmin,
            IsMine = t.AssignedToUserId == uid,
            AttachmentCount = _db.TicketAttachments.Count(a => a.TicketId == t.Id)
        }).ToListAsync();

        foreach (var item in Items.Where(x => x.ContractId.HasValue))
        {
            var contractId = item.ContractId;
            if (!contractId.HasValue) continue;
            var svc = await _db.ClientCarrierServices
                .AsNoTracking()
                .Include(s => s.Carrier)
                .Where(s => s.ClientServiceContractId == contractId.Value)
                .OrderBy(s => s.ServiceLabel)
                .Select(s => new
                {
                    Carrier = s.Carrier != null ? s.Carrier.Name : "-",
                    s.ServiceLabel,
                    s.AccountNumber,
                    s.CircuitId,
                    s.IpInfo
                })
                .FirstOrDefaultAsync();

            if (svc != null)
            {
                item.CarrierName = svc.Carrier;
                item.CarrierServiceLabel = svc.ServiceLabel ?? "";
                item.CarrierAccount = svc.AccountNumber ?? "";
                item.CarrierCircuit = svc.CircuitId ?? "";
                item.CarrierIp = svc.IpInfo ?? "";
            }
        }

        Groups = Items
            .GroupBy(x => x.Client)
            .OrderBy(g => g.Key)
            .Select(g => new ClientGroupVm
            {
                Client = g.Key,
                OpenCount = g.Count(x => x.Status != TicketStatus.Closed && x.Status != TicketStatus.Cancelled),
                BreachCount = g.Count(x => x.Breach),
                Tickets = g.OrderByDescending(x => x.CreatedAt).ToList()
            })
            .ToList();

        if (IsAdmin)
        {
            ClientOptions = await _db.Clients
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new ClientPickVm { Id = c.Id, Name = c.Name, Code = c.ClientCode })
                .ToListAsync();

            ContractOptions = await _db.ClientServiceContracts
                .AsNoTracking()
                .Include(c => c.Client)
                .OrderBy(c => c.Client!.Name)
                .ThenBy(c => c.Label)
                .Select(c => new ContractPickVm
                {
                    Id = c.Id,
                    ClientId = c.ClientId,
                    Label = (string.IsNullOrWhiteSpace(c.Branch) ? "Sin sucursal" : c.Branch) + " - " + c.Label,
                    Address = c.BranchAddress,
                    CarrierSummary = _db.ClientCarrierServices
                        .Where(s => s.ClientServiceContractId == c.Id)
                        .OrderBy(s => s.ServiceLabel)
                        .Select(s => (s.Carrier != null ? s.Carrier.Name : "-")
                                     + " | Cuenta: " + (string.IsNullOrWhiteSpace(s.AccountNumber) ? "-" : s.AccountNumber)
                                     + " | Circuito: " + (string.IsNullOrWhiteSpace(s.CircuitId) ? "-" : s.CircuitId)
                                     + " | IP: " + (string.IsNullOrWhiteSpace(s.IpInfo) ? "-" : s.IpInfo))
                        .FirstOrDefault() ?? ""
                })
                .ToListAsync();

            EmployeeOptions = await _db.EmployeeProfiles
                .AsNoTracking()
                .OrderBy(e => e.FullName)
                .Select(e => new EmployeePickVm
                {
                    UserId = e.UserId,
                    Name = e.FullName
                })
                .ToListAsync();
        }
    }

    private static string ToStatusLabel(TicketStatus s) => s switch
    {
        TicketStatus.New => "Nuevo",
        TicketStatus.Assigned => "Asignado",
        TicketStatus.InProgress => "En proceso",
        TicketStatus.PendingCustomer => "Pendiente cliente",
        TicketStatus.Resolved => "Resuelto",
        TicketStatus.Closed => "Cerrado",
        TicketStatus.Cancelled => "Cancelado",
        _ => "-"
    };

    private static string ToPriorityLabel(TicketPriority p) => p switch
    {
        TicketPriority.Low => "Baja",
        TicketPriority.Medium => "Intermedia",
        TicketPriority.High => "Alta",
        TicketPriority.Critical => "Urge",
        _ => "-"
    };

    public class TicketVm
    {
        public Guid Id { get; set; }
        public string TicketNumber { get; set; } = "";
        public Guid? ContractId { get; set; }
        public string Client { get; set; } = "";
        public string Contract { get; set; } = "";
        public string Branch { get; set; } = "";
        public string Title { get; set; } = "";
        public TicketStatus Status { get; set; }
        public string StatusLabel { get; set; } = "";
        public TicketSource Source { get; set; }
        public TicketPriority Priority { get; set; }
        public string PriorityLabel { get; set; } = "";
        public string AssignedTo { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime DueResponseAt { get; set; }
        public DateTime DueResolutionAt { get; set; }
        public bool Breach { get; set; }
        public bool IsMine { get; set; }
        public bool CanTake { get; set; }
        public int AttachmentCount { get; set; }
        public string CarrierName { get; set; } = "";
        public string CarrierServiceLabel { get; set; } = "";
        public string CarrierAccount { get; set; } = "";
        public string CarrierCircuit { get; set; } = "";
        public string CarrierIp { get; set; } = "";
    }

    public class ClientGroupVm
    {
        public string Client { get; set; } = "";
        public int OpenCount { get; set; }
        public int BreachCount { get; set; }
        public List<TicketVm> Tickets { get; set; } = new();
    }

    public class ClientPickVm
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Code { get; set; }
    }

    public class ContractPickVm
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public string Label { get; set; } = "";
        public string? Address { get; set; }
        public string? CarrierSummary { get; set; }
    }

    public class EmployeePickVm
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class CreateManualInput
    {
        public Guid ClientId { get; set; }
        public Guid? ContractId { get; set; }
        public string? AssignedToUserId { get; set; }
        public string? AssignedToName { get; set; }
        public string RequesterName { get; set; } = "";
        public string RequesterEmail { get; set; } = "";
        public string? RequesterPhone { get; set; }
        public string? RequesterLocation { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    }
}
