using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Security.Roles;

[Authorize(Roles = AppRoles.Admin)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    public List<ModuleOption> Modules { get; set; } = new();
    public List<ModuleOption> Actions { get; set; } = new();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class ModuleOption
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
    }

    public class InputModel
    {
        [Required, MaxLength(80)]
        public string Name { get; set; } = "";

        [MaxLength(400)]
        public string Description { get; set; } = "";

        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;

        public List<string> SelectedModules { get; set; } = new();
        public List<string> SelectedActions { get; set; } = new();
    }

    public void OnGet()
    {
        LoadModules();
        Input.SelectedModules = AppModules.EmployeeDefaults.ToList();
        Input.SelectedActions = AppActions.EmployeeDefaults.ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        LoadModules();
        if (!ModelState.IsValid) return Page();

        var name = Input.Name.Trim();
        if (await _db.PermissionRoles.AnyAsync(r => r.Name.ToLower() == name.ToLower()))
        {
            ModelState.AddModelError(string.Empty, "Ya existe un rol con ese nombre.");
            return Page();
        }

        if (Input.IsDefault)
        {
            var others = await _db.PermissionRoles.Where(r => r.IsDefault).ToListAsync();
            foreach (var o in others) o.IsDefault = false;
        }

        var role = new PermissionRole
        {
            Name = name,
            Description = (Input.Description ?? "").Trim(),
            IsDefault = Input.IsDefault,
            IsActive = Input.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Modules = new List<PermissionRoleModule>()
        };

        var selected = Input.SelectedModules
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var key in selected)
        {
            role.Modules.Add(new PermissionRoleModule
            {
                PermissionRoleId = role.Id,
                ModuleKey = key.Trim()
            });
        }

        var selectedActions = Input.SelectedActions
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var key in selectedActions)
        {
            _db.PermissionRoleActions.Add(new PermissionRoleAction
            {
                PermissionRoleId = role.Id,
                ActionKey = key.Trim()
            });
        }

        _db.PermissionRoles.Add(role);
        await _db.SaveChangesAsync();

        return RedirectToPage("./Index");
    }

    private void LoadModules()
    {
        Modules = AppModules.AllKnown
            .Select(k => new ModuleOption { Key = k, Label = AppModules.Label(k) })
            .ToList();

        Actions = AppActions.AllKnown
            .Select(k => new ModuleOption { Key = k, Label = AppActions.Label(k) })
            .ToList();
    }
}
