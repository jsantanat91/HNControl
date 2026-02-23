using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
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

    public string? Info { get; set; }
    public string? Error { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Cliente es requerido.")]
        public Guid ClientId { get; set; }

        [Required(ErrorMessage = "Responsable es requerido.")]
        public string ResponsibleUserId { get; set; } = "";

        [Required(ErrorMessage = "El nombre del proyecto es requerido.")]
        [MaxLength(200)]
        public string Title { get; set; } = "";

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

        [DataType(DataType.Date)]
        public DateTime? EstimatedEndDate { get; set; } = DateTime.UtcNow.Date.AddDays(7);

        [MaxLength(400)]
        public string Objective { get; set; } = "";

        [MaxLength(1200)]
        public string Scope { get; set; } = "";

        public string Description { get; set; } = "";
        public string AccessNotes { get; set; } = "";
        public string Comments { get; set; } = "";
    }

    public async Task OnGetAsync(Guid? clientId = null)
    {
        await LoadListsAsync();
        if (clientId.HasValue)
            Input.ClientId = clientId.Value;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();

        if (string.IsNullOrWhiteSpace(Input.Title))
            ModelState.AddModelError("Input.Title", "El nombre del proyecto es requerido.");

        if (!ModelState.IsValid)
        {
            // Diagnóstico útil para que NO adivines: te dice qué campo falló.
            Error = "Validación falló: " + string.Join(" | ",
                ModelState.Where(x => x.Value?.Errors.Count > 0)
                          .Select(x => $"{x.Key} => {string.Join(", ", x.Value!.Errors.Select(e => e.ErrorMessage))}")
            );
            return Page();
        }

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