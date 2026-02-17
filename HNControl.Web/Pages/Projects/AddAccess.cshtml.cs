using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects;

[Authorize(Roles = AppRoles.Admin)]
public class AddAccessModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ISecretProtector _protector;

    public AddAccessModel(ApplicationDbContext db, ISecretProtector protector)
    {
        _db = db;
        _protector = protector;
    }

    [BindProperty(SupportsGet = true)] public Guid ProjectId { get; set; }
    public string? Error { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, MaxLength(120)] public string Label { get; set; } = "";
        [MaxLength(300)] public string HostOrUrl { get; set; } = "";
        [MaxLength(200)] public string Username { get; set; } = "";
        [Required, MaxLength(400)] public string Password { get; set; } = "";
        [MaxLength(600)] public string Notes { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        ProjectId = id;
        var exists = await _db.Projects.AnyAsync(p => p.Id == ProjectId);
        if (!exists) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == ProjectId);
        if (project == null) return NotFound();

        var access = new ProjectAccess
        {
            ProjectId = ProjectId,
            Label = Input.Label.Trim(),
            HostOrUrl = (Input.HostOrUrl ?? "").Trim(),
            Username = (Input.Username ?? "").Trim(),
            PasswordProtected = _protector.Protect(Input.Password.Trim()),
            Notes = (Input.Notes ?? "").Trim()
        };

        _db.ProjectAccesses.Add(access);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Projects/Details", new { id = ProjectId });
    }
}
