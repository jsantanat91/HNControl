using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Public;

[AllowAnonymous]
public class TicketsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ITicketFlowService _flow;

    public TicketsModel(ApplicationDbContext db, ITicketFlowService flow)
    {
        _db = db;
        _flow = flow;
    }

    [BindProperty]
    public PublicTicketInput Input { get; set; } = new();

    public Client? Client { get; set; }
    public List<ClientServiceContract> Contracts { get; set; } = new();
    public List<HistoryTicketRow> History { get; set; } = new();
    public string? Message { get; set; }
    public bool Success { get; set; }
    [BindProperty]
    public DateTime? HistoryFrom { get; set; }
    [BindProperty]
    public DateTime? HistoryTo { get; set; }

    public async Task OnGetAsync(string? clientCode = null)
    {
        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            Input.ClientCode = clientCode.Trim().ToUpperInvariant();
            await LoadClientAsync();
            await LoadHistoryAsync();
        }
    }

    public async Task<IActionResult> OnPostLookupAsync()
    {
        await LoadClientAsync();
        Input.Mode = "new";
        if (Client == null)
            Message = "No encontramos ese numero de cliente.";
        return Page();
    }

    public async Task<IActionResult> OnPostHistoryAsync()
    {
        await LoadClientAsync();
        Input.Mode = "history";
        if (Client == null)
        {
            Message = "No encontramos ese ID de cliente.";
            return Page();
        }

        await LoadHistoryAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        await LoadClientAsync();
        if (Client == null)
        {
            Message = "Cliente invalido.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Input.RequesterName)
            || string.IsNullOrWhiteSpace(Input.RequesterEmail)
            || string.IsNullOrWhiteSpace(Input.Title)
            || string.IsNullOrWhiteSpace(Input.Description))
        {
            Message = "Completa los datos de contacto obligatorios.";
            return Page();
        }

        var ticket = await _flow.CreatePublicAsync(
            Input.ClientCode,
            Input.ContractId,
            Input.RequesterName,
            Input.RequesterEmail,
            Input.RequesterPhone ?? "",
            Input.RequesterLocation ?? "",
            Input.Title,
            Input.Description,
            Input.Priority switch
            {
                PublicPriority.Low => TicketPriority.Low,
                PublicPriority.Medium => TicketPriority.Medium,
                PublicPriority.Urge => TicketPriority.Critical,
                _ => TicketPriority.Medium
            });

        Success = true;
        Message = $"Ticket generado: {ticket.TicketNumber}. Te contactaremos pronto.";
        var code = Client.ClientCode;
        Input = new PublicTicketInput
        {
            ClientCode = code,
            Mode = "history",
            Priority = PublicPriority.Medium
        };
        await LoadClientAsync();
        await LoadHistoryAsync();
        return Page();
    }

    private async Task LoadClientAsync()
    {
        var code = (Input.ClientCode ?? "").Trim().ToUpperInvariant();
        Client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.ClientCode == code);
        Contracts = new();
        if (Client == null) return;

        Contracts = await _db.ClientServiceContracts
            .AsNoTracking()
            .Where(x => x.ClientId == Client.Id)
            .OrderBy(x => x.ServiceType)
            .ThenBy(x => x.Label)
            .ToListAsync();
    }

    private async Task LoadHistoryAsync()
    {
        History = new();
        if (Client == null) return;

        var from = HistoryFrom?.Date;
        var to = HistoryTo?.Date.AddDays(1);

        var q = _db.Tickets
            .AsNoTracking()
            .Where(t => t.ClientId == Client.Id)
            .Include(t => t.ClientServiceContract)
            .OrderByDescending(t => t.CreatedAt)
            .AsQueryable();

        if (from.HasValue)
            q = q.Where(t => t.CreatedAt >= from.Value);
        if (to.HasValue)
            q = q.Where(t => t.CreatedAt < to.Value);

        History = await q.Take(200).Select(t => new HistoryTicketRow
        {
            TicketNumber = t.TicketNumber,
            Title = t.Title,
            Contract = t.ClientServiceContract != null ? t.ClientServiceContract.Label : "-",
            CreatedAt = t.CreatedAt.ToLocalTime(),
            Status = ToStatusEs(t.Status),
            Priority = ToPriorityEs(t.Priority)
        }).ToListAsync();
    }

    private static string ToStatusEs(TicketStatus s) => s switch
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

    private static string ToPriorityEs(TicketPriority p) => p switch
    {
        TicketPriority.Low => "Baja",
        TicketPriority.Medium => "Intermedia",
        TicketPriority.High => "Urge",
        TicketPriority.Critical => "Urge",
        _ => "-"
    };

    public class PublicTicketInput
    {
        public string ClientCode { get; set; } = "";
        public Guid? ContractId { get; set; }
        public PublicPriority Priority { get; set; } = PublicPriority.Medium;
        public string Mode { get; set; } = "new";
        public string RequesterName { get; set; } = "";
        public string RequesterEmail { get; set; } = "";
        public string? RequesterPhone { get; set; }
        public string? RequesterLocation { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public enum PublicPriority
    {
        Low = 1,
        Medium = 2,
        Urge = 3
    }

    public class HistoryTicketRow
    {
        public string TicketNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public string Contract { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "";
        public string Priority { get; set; } = "";
    }
}
