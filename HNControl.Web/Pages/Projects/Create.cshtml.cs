using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects;

[Authorize(Roles = AppRoles.Admin)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    public SelectList ClientItems { get; set; } = default!;
    public SelectList EmployeeItems { get; set; } = default!;

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public NewClientModel NewClient { get; set; } = new();

    public string? Info { get; set; }
    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public Guid ClientId { get; set; }

        [Required] public string ResponsibleUserId { get; set; } = "";

        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        // ✅ si llega null en el post, no truena: usamos fallback
        [DataType(DataType.Date)]
        public DateTime? EstimatedEndDate { get; set; } = DateTime.Today.AddDays(7);

        [MaxLength(400)]
        public string Objective { get; set; } = "";

        [MaxLength(1200)]
        public string Scope { get; set; } = "";

        public string Description { get; set; } = "";
        public string AccessNotes { get; set; } = "";
        public string Comments { get; set; } = "";
    }

    public class NewClientModel
    {
        public ClientType Type { get; set; } = ClientType.Moral;

        [Required, MaxLength(200)]
        public string Name { get; set; } = "";

        [MaxLength(13)]
        public string? Rfc { get; set; }

        [MaxLength(256)]
        public string? Email { get; set; }

        [MaxLength(40)]
        public string? Phone { get; set; }

        [MaxLength(400)]
        public string? Address { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadListsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();

        if (!ModelState.IsValid)
            return Page();

        var end = Input.EstimatedEndDate ?? Input.StartDate.AddDays(7);

        if (end.Date < Input.StartDate.Date)
        {
            Error = "La fecha estimada no puede ser menor al inicio.";
            return Page();
        }

        var p = new Project
        {
            ClientId = Input.ClientId,
            AssignedUserId = Input.ResponsibleUserId,
            Title = Input.Title.Trim(),
            StartDate = Input.StartDate.Date,
            EstimatedEndDate = end.Date,
            Objective = (Input.Objective ?? "").Trim(),
            Scope = (Input.Scope ?? "").Trim(),
            ActivityDescription = (Input.Description ?? "").Trim(),
            AdditionalComments = (Input.Comments ?? "").Trim(),
            AccessNotes = (Input.AccessNotes ?? "").Trim(),
            Status = ProjectStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Projects.Add(p);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Projects/Details", new { id = p.Id });
    }

    public async Task<IActionResult> OnPostCreateClientAsync()
    {
        // Nota: este handler solo valida el form derecho (cliente rápido)
        if (!TryValidateModel(NewClient, nameof(NewClient)))
        {
            await LoadListsAsync();
            Error = "Revisa los campos del nuevo cliente.";
            return Page();
        }

        var client = new Client
        {
            Type = NewClient.Type,
            Name = NewClient.Name.Trim(),
            Rfc = (NewClient.Rfc ?? "").Trim(),
            Email = (NewClient.Email ?? "").Trim(),
            Phone = (NewClient.Phone ?? "").Trim(),
            Address = (NewClient.Address ?? "").Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync();

        // Seleccionarlo en el dropdown del proyecto
        Input.ClientId = client.Id;
        Info = $"Cliente creado: {client.Name}";

        await LoadListsAsync(selectedClientId: client.Id);
        return Page();
    }

    private async Task LoadListsAsync(Guid? selectedClientId = null)
    {
        var clients = await _db.Clients.OrderBy(c => c.Name).ToListAsync();
        ClientItems = new SelectList(clients, "Id", "Name", selectedClientId ?? (Input.ClientId == Guid.Empty ? null : Input.ClientId));

        var emps = await _db.EmployeeProfiles.OrderBy(e => e.FullName).ToListAsync();
        EmployeeItems = new SelectList(emps, "UserId", "FullName", string.IsNullOrWhiteSpace(Input.ResponsibleUserId) ? null : Input.ResponsibleUserId);
    }
}
