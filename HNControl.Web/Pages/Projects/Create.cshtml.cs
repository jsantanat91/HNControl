using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
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
    [BindProperty]
    [ValidateNever]
    public NewClientModel NewClient { get; set; } = new();

    public string? Info { get; set; }
    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public Guid ClientId { get; set; }
        [Required] public string ResponsibleUserId { get; set; } = "";

        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

        [DataType(DataType.Date)]
        public DateTime? EstimatedEndDate { get; set; } = DateTime.UtcNow.Date.AddDays(7);

        [MaxLength(400)] public string Objective { get; set; } = "";
        [MaxLength(1200)] public string Scope { get; set; } = "";

        public string Description { get; set; } = "";
        public string AccessNotes { get; set; } = "";
        public string Comments { get; set; } = "";
    }

    public class NewClientModel
    {
        public ClientType Type { get; set; } = ClientType.Moral;

        [Required(ErrorMessage = "El nombre es requerido."), MaxLength(200)]
        public string Name { get; set; } = "";

        [MaxLength(13)] public string? Rfc { get; set; }
        [MaxLength(256)] public string? Email { get; set; }
        [MaxLength(40)] public string? Phone { get; set; }
        [MaxLength(400)] public string? Address { get; set; }
    }

    public async Task OnGetAsync(Guid? clientId = null)
    {
        await LoadListsAsync();
        if (clientId.HasValue) Input.ClientId = clientId.Value;
    }

    // ✅ Guardar proyecto
    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();

        // ✅ El modal de “Nuevo cliente” comparte PageModel. Si llega vacío, NO debe bloquear el post del proyecto.
        //    (La validación real del cliente la hace el handler AJAX.)
        foreach (var k in ModelState.Keys
                     .Where(k => k.Equals("NewClient", StringComparison.OrdinalIgnoreCase)
                              || k.StartsWith("NewClient.", StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            ModelState.Remove(k);
        }

        // Revalida solo Input para evitar “fantasmas” del modal.
        ModelState.ClearValidationState(nameof(Input));
        TryValidateModel(Input, nameof(Input));

        if (string.IsNullOrWhiteSpace(Input.Title))
            ModelState.AddModelError("Input.Title", "El nombre del proyecto es requerido.");

        if (!ModelState.IsValid)
            return Page();

        var end = Input.EstimatedEndDate ?? Input.StartDate.AddDays(7);

        var startUtc = TimeUtil.UtcDate(Input.StartDate);
        var endUtc = TimeUtil.UtcDate(end);

        if (endUtc < startUtc)
        {
            Error = "La fecha estimada no puede ser menor al inicio.";
            return Page();
        }

        var p = new Project
        {
            ClientId = Input.ClientId,
            AssignedUserId = Input.ResponsibleUserId,
            Title = (Input.Title ?? "").Trim(),

            StartDate = startUtc,
            EstimatedEndDate = endUtc,

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

    // ✅ Handler AJAX que tu Create.cshtml ya está llamando: asp-page-handler="CreateClientAjax"
    public async Task<IActionResult> OnPostCreateClientAjaxAsync()
    {
        // valida solo el modelo NewClient
        ModelState.Clear();
        if (!TryValidateModel(NewClient, nameof(NewClient)))
        {
            var errors = ModelState
                .Where(k => k.Value?.Errors.Count > 0)
                .ToDictionary(
                    k => k.Key,
                    k => k.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            return new JsonResult(new { ok = false, errors });
        }

        var client = new Client
        {
            Type = NewClient.Type,
            Name = NewClient.Name.Trim(),
            Rfc = (NewClient.Rfc ?? "").Trim().ToUpperInvariant(),
            Email = (NewClient.Email ?? "").Trim(),
            Phone = (NewClient.Phone ?? "").Trim(),
            Address = (NewClient.Address ?? "").Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync();

        return new JsonResult(new { ok = true, id = client.Id, name = client.Name });
    }

    private async Task LoadListsAsync(Guid? selectedClientId = null)
    {
        var clients = await _db.Clients.OrderBy(c => c.Name).ToListAsync();
        ClientItems = new SelectList(
            clients,
            "Id",
            "Name",
            selectedClientId ?? (Input.ClientId == Guid.Empty ? null : Input.ClientId));

        var emps = await _db.EmployeeProfiles.OrderBy(e => e.FullName).ToListAsync();
        EmployeeItems = new SelectList(
            emps,
            "UserId",
            "FullName",
            string.IsNullOrWhiteSpace(Input.ResponsibleUserId) ? null : Input.ResponsibleUserId);
    }
}
