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
    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public Guid ClientId { get; set; }
        [Required] public string AssignedUserId { get; set; } = "";
        [Required, MaxLength(200)] public string Title { get; set; } = "";
        [DataType(DataType.Date)] public DateTime StartDate { get; set; } = DateTime.Today;
        [DataType(DataType.Date)] public DateTime EstimatedEndDate { get; set; } = DateTime.Today.AddDays(7);

        public string Objective { get; set; } = "";
        public string Scope { get; set; } = "";
        public string ActivityDescription { get; set; } = "";
        public string AdditionalComments { get; set; } = "";
    }

    public async Task OnGetAsync()
    {
        await LoadListsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();
        if (!ModelState.IsValid) return Page();

        if (Input.EstimatedEndDate.Date < Input.StartDate.Date)
        {
            Error = "La fecha estimada no puede ser menor al inicio.";
            return Page();
        }

        var p = new Project
        {
            ClientId = Input.ClientId,
            AssignedUserId = Input.AssignedUserId,
            Title = Input.Title.Trim(),
            StartDate = Input.StartDate.Date,
            EstimatedEndDate = Input.EstimatedEndDate.Date,
            Objective = (Input.Objective ?? "").Trim(),
            Scope = (Input.Scope ?? "").Trim(),
            ActivityDescription = (Input.ActivityDescription ?? "").Trim(),
            AdditionalComments = (Input.AdditionalComments ?? "").Trim(),
            Status = ProjectStatus.Open
        };

        _db.Projects.Add(p);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Projects/Details", new { id = p.Id });
    }

    private async Task LoadListsAsync()
    {
        var clients = await _db.Clients.OrderBy(c => c.Name).ToListAsync();
        ClientItems = new SelectList(clients, "Id", "Name");

        var emps = await _db.EmployeeProfiles.OrderBy(e => e.FullName).ToListAsync();
        EmployeeItems = new SelectList(emps, "UserId", "FullName");
    }
}
