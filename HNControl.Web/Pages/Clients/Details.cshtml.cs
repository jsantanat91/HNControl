using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients;

[Authorize(Roles = AppRoles.Admin)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _cfg;
    public DetailsModel(ApplicationDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    public Client? Client { get; set; }
    public List<ClientContactRow> Contacts { get; set; } = new();

    [BindProperty]
    public NewContactInput ContactInput { get; set; } = new();

    public record ContractRow(
        Guid Id,
        string ServiceType,
        string Label,
        string Provider,
        string AccountNumber,
        string ContractNumber,
        string Branch,
        string BranchAddress,
        string ContractEndDateText,
        string StatusText,
        string StatusBadgeClass,
        bool HasPortalAccess,
        bool HasContractFile,
        string ProjectTitle
    );

    public List<ContractRow> Contracts { get; set; } = new();

    public record ProjectRow(Guid Id, string Title, string StartDate, string EstEnd, string Status);
    public List<ProjectRow> Projects { get; set; } = new();
    public record QuoteRow(Guid Id, string Folio, string CreatedAt, string Segment, decimal Total, int ManualItems, bool HasPdf);
    public List<QuoteRow> Quotes { get; set; } = new();
    public record TicketRow(Guid Id, string Number, string Title, string Status, string Priority, string CreatedAt, string AssignedTo);
    public List<TicketRow> Tickets { get; set; } = new();
    public string PublicQuoteUrl { get; set; } = string.Empty;
    public string PublicTicketUrl { get; set; } = string.Empty;
    public record ClientContactRow(Guid Id, string Name, string Email, string Phone, string Role, bool IsPrimary, DateTime UpdatedAt);

    public class NewContactInput
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsPrimary { get; set; }
    }

    public async Task OnGetAsync(Guid id)
    {
        Client = await _db.Clients
            .Include(c => c.Contracts)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (Client == null) return;

        if (string.IsNullOrWhiteSpace(Client.ClientCode))
        {
            Client.ClientCode = await NextClientCodeAsync();
            await _db.SaveChangesAsync();
        }

        if (string.IsNullOrWhiteSpace(Client.PublicQuoteToken))
        {
            Client.PublicQuoteToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
            await _db.SaveChangesAsync();
        }

        var baseUrl = (_cfg["PublicLinks:BaseUrl"] ?? "").Trim().TrimEnd('/');
        PublicQuoteUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? $"/cotizar/{Client.PublicQuoteToken}"
            : $"{baseUrl}/cotizar/{Client.PublicQuoteToken}";
        PublicTicketUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? "/ticket-publico"
            : $"{baseUrl}/ticket-publico";

        var projMap = await _db.Projects
            .Where(p => p.ClientId == id)
            .Select(p => new { p.Id, p.Title })
            .ToDictionaryAsync(x => x.Id, x => x.Title);

        var today = DateTime.Today;
        var soon = today.AddDays(30);

        Contracts = Client.Contracts
            .OrderBy(x => x.ServiceType)
            .ThenBy(x => x.Label)
            .Select(x =>
            {
                var end = x.ContractEndDate?.Date;
                var status = "Activo";
                var badge = "text-bg-success";

                if (end.HasValue && end.Value < today)
                {
                    status = "Vencido";
                    badge = "text-bg-danger";
                }
                else if (end.HasValue && end.Value <= soon)
                {
                    status = "Por vencer";
                    badge = "text-bg-warning";
                }

                var endText = end.HasValue ? end.Value.ToString("yyyy-MM-dd") : "—";

                var projTitle = (x.ProjectId.HasValue && projMap.TryGetValue(x.ProjectId.Value, out var t))
                    ? t
                    : "—";

                return new ContractRow(
                    x.Id,
                    x.ServiceType.ToString(),
                    x.Label,
                    x.Provider,
                    x.AccountNumber,
                    x.ContractNumber,
                    x.Branch,
                    x.BranchAddress,
                    endText,
                    status,
                    badge,
                    !string.IsNullOrWhiteSpace(x.PortalUrl) || !string.IsNullOrWhiteSpace(x.PortalUsername),
                    !string.IsNullOrWhiteSpace(x.SignedContractStoragePath),
                    projTitle
                );
            })
            .ToList();

        var projs = await _db.Projects
            .Include(p => p.AssignedEmployee)
            .Where(p => p.ClientId == id)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();

        Projects = projs.Select(p => new ProjectRow(
            p.Id,
            p.Title,
            p.StartDate.ToString("yyyy-MM-dd"),
            p.EstimatedEndDate.ToString("yyyy-MM-dd"),
            p.Status.ToString()
        )).Take(200).ToList();

        Quotes = await _db.QuoteRequests
            .AsNoTracking()
            .Where(x => x.ClientId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new QuoteRow(
                x.Id,
                x.Folio,
                x.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                x.Segment == QuoteSegment.Business ? "Empresarial" : "Residencial",
                x.EstimatedTotal ?? x.SubtotalAuto,
                x.ManualItemsCount,
                !string.IsNullOrWhiteSpace(x.PdfStoragePath)
            ))
            .ToListAsync();

        Tickets = await _db.Tickets
            .AsNoTracking()
            .Where(x => x.ClientId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new TicketRow(
                x.Id,
                x.TicketNumber,
                x.Title,
                x.Status == TicketStatus.New ? "Nuevo" :
                x.Status == TicketStatus.Assigned ? "Asignado" :
                x.Status == TicketStatus.InProgress ? "En proceso" :
                x.Status == TicketStatus.PendingCustomer ? "Pendiente cliente" :
                x.Status == TicketStatus.Resolved ? "Resuelto" :
                x.Status == TicketStatus.Closed ? "Cerrado" :
                x.Status == TicketStatus.Cancelled ? "Cancelado" : "-",
                x.Priority == TicketPriority.Low ? "Baja" :
                x.Priority == TicketPriority.Medium ? "Intermedia" :
                x.Priority == TicketPriority.High ? "Alta" :
                x.Priority == TicketPriority.Critical ? "Urge" : "-",
                x.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                string.IsNullOrWhiteSpace(x.AssignedToName) ? "Sin asignar" : x.AssignedToName
            ))
            .ToListAsync();

        await LoadContactsAsync(id);
    }

    public async Task<IActionResult> OnPostAddContactAsync(Guid id)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id);
        if (client == null) return NotFound();

        var name = (ContactInput.Name ?? "").Trim();
        var email = (ContactInput.Email ?? "").Trim();
        var phone = (ContactInput.Phone ?? "").Trim();
        var role = (ContactInput.Role ?? "").Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["ClientDetailsInfo"] = "El nombre del contacto es obligatorio.";
            TempData["ClientDetailsInfoType"] = "danger";
            return RedirectToPage(new { id });
        }

        bool exists;
        try
        {
            exists = await _db.ClientContacts.AnyAsync(c =>
                c.ClientId == id
                && c.Name.ToLower() == name.ToLower()
                && c.Email.ToLower() == email.ToLower());
        }
        catch
        {
            TempData["ClientDetailsInfo"] = "Falta aplicar migración de base de datos para contactos.";
            TempData["ClientDetailsInfoType"] = "danger";
            return RedirectToPage(new { id });
        }

        if (exists)
        {
            TempData["ClientDetailsInfo"] = "Ese contacto ya existe en el cliente.";
            TempData["ClientDetailsInfoType"] = "warning";
            return RedirectToPage(new { id });
        }

        if (ContactInput.IsPrimary)
        {
            var primaries = await _db.ClientContacts.Where(c => c.ClientId == id && c.IsPrimary).ToListAsync();
            foreach (var p in primaries)
                p.IsPrimary = false;
        }

        _db.ClientContacts.Add(new ClientContact
        {
            ClientId = id,
            Name = name,
            Email = email,
            Phone = phone,
            Role = role,
            IsPrimary = ContactInput.IsPrimary,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        TempData["ClientDetailsInfo"] = "Contacto agregado.";
        TempData["ClientDetailsInfoType"] = "success";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteContactAsync(Guid id, Guid contactId)
    {
        ClientContact? contact;
        try
        {
            contact = await _db.ClientContacts.FirstOrDefaultAsync(c => c.Id == contactId && c.ClientId == id);
        }
        catch
        {
            TempData["ClientDetailsInfo"] = "Falta aplicar migración de base de datos para contactos.";
            TempData["ClientDetailsInfoType"] = "danger";
            return RedirectToPage(new { id });
        }
        if (contact == null)
        {
            TempData["ClientDetailsInfo"] = "Contacto no encontrado.";
            TempData["ClientDetailsInfoType"] = "warning";
            return RedirectToPage(new { id });
        }

        _db.ClientContacts.Remove(contact);
        await _db.SaveChangesAsync();
        TempData["ClientDetailsInfo"] = "Contacto eliminado.";
        TempData["ClientDetailsInfoType"] = "success";
        return RedirectToPage(new { id });
    }

    private async Task LoadContactsAsync(Guid clientId)
    {
        try
        {
            Contacts = await _db.ClientContacts
                .AsNoTracking()
                .Where(x => x.ClientId == clientId)
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.Name)
                .Select(x => new ClientContactRow(
                    x.Id,
                    x.Name,
                    x.Email,
                    x.Phone,
                    x.Role,
                    x.IsPrimary,
                    x.UpdatedAt))
                .ToListAsync();
        }
        catch
        {
            Contacts = new();
        }
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
