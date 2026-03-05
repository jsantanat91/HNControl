using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Carriers;

[Authorize(Policy = "EmployeeOnly")]
public class ClientModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public ClientModel(ApplicationDbContext db) => _db = db;

    public Client? Client { get; set; }
    public List<ServiceVm> Services { get; set; } = new();
    public bool IsAdmin => User.IsInRole(AppRoles.Admin);

    [BindProperty]
    public AddNoteInput Input { get; set; } = new();

    public class ServiceVm
    {
        public ClientCarrierService Service { get; set; } = default!;
        public string CarrierName { get; set; } = "";
        public string CarrierExecutive { get; set; } = "";
        public string? CarrierLogoUrl { get; set; }
        public string SupportPhone { get; set; } = "";
        public string SupportEmail { get; set; } = "";
        public string SupportPortalUrl { get; set; } = "";
        public List<ClientCarrierNote> RecentNotes { get; set; } = new();
    }

    public class AddNoteInput
    {
        [Required]
        public Guid ServiceId { get; set; }

        public CarrierNoteType NoteType { get; set; } = CarrierNoteType.Info;

        [MaxLength(120)]
        public string? TicketNumber { get; set; }

        [Required, MaxLength(3000)]
        public string Message { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        await LoadAsync(id);
        if (Client == null) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAddNoteAsync(Guid id)
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync(id);
            return Page();
        }

        // Asegura que el servicio pertenece a este cliente
        var svc = await _db.ClientCarrierServices
            .Include(s => s.Carrier)
            .FirstOrDefaultAsync(s => s.Id == Input.ServiceId && s.ClientId == id);

        if (svc == null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var prof = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);

        _db.ClientCarrierNotes.Add(new ClientCarrierNote
        {
            ServiceId = svc.Id,
            NoteType = Input.NoteType,
            TicketNumber = (Input.TicketNumber ?? "").Trim(),
            Message = Input.Message.Trim(),
            CreatedByUserId = userId,
            CreatedByName = prof?.FullName ?? (User.Identity?.Name ?? ""),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return RedirectToPage("./Client", new { id });
    }

    private async Task LoadAsync(Guid clientId)
    {
        Client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId);
        if (Client == null) return;

        var services = await _db.ClientCarrierServices
            .AsNoTracking()
            .Include(s => s.Carrier)
            .Include(s => s.ClientServiceContract)
            .Where(s => s.ClientId == clientId && s.IsActive)
            .OrderBy(s => s.ServiceLabel)
            .ToListAsync();

        var serviceIds = services.Select(s => s.Id).ToList();
        var notes = await _db.ClientCarrierNotes
            .AsNoTracking()
            .Where(n => serviceIds.Contains(n.ServiceId))
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        var groupedNotes = notes
            .GroupBy(n => n.ServiceId)
            .ToDictionary(g => g.Key, g => g.Take(8).ToList());

        Services = services.Select(s =>
        {
            groupedNotes.TryGetValue(s.Id, out var recent);

            var carrierName = s.Carrier?.Name ?? "(Sin carrier)";
            var supportPhone = !string.IsNullOrWhiteSpace(s.SupportPhoneOverride)
                ? s.SupportPhoneOverride
                : (s.Carrier?.SupportPhone ?? "");

            return new ServiceVm
            {
                Service = s,
                CarrierName = carrierName,
                CarrierExecutive = s.Carrier?.ExecutiveName ?? "",
                CarrierLogoUrl = !string.IsNullOrWhiteSpace(s.Carrier?.LogoStoragePath)
                    ? Url.Page("/Carriers/Logo", new { id = s.Carrier!.Id })
                    : null,
                SupportPhone = supportPhone,
                SupportEmail = s.Carrier?.SupportEmail ?? "",
                SupportPortalUrl = s.Carrier?.SupportPortalUrl ?? "",
                RecentNotes = recent ?? new List<ClientCarrierNote>()
            };
        }).ToList();
    }
}
