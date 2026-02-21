using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Security.Roles;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    public List<ModuleOption> Modules { get; set; } = new();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class ModuleOption
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
    }

    public class InputModel
    {
        public Guid Id { get; set; }

        [Required, MaxLength(80)]
        public string Name { get; set; } = "";

        [MaxLength(400)]
        public string Description { get; set; } = "";

        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;

        public List<string> SelectedModules { get; set; } = new();
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        LoadModules();

        var role = await _db.PermissionRoles
            .Include(r => r.Modules)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null) return NotFound();

        Input = new InputModel
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsDefault = role.IsDefault,
            IsActive = role.IsActive,
            SelectedModules = role.Modules.Select(m => m.ModuleKey).ToList()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        LoadModules();
        if (!ModelState.IsValid) return Page();

        var role = await _db.PermissionRoles
            .Include(r => r.Modules)
            .FirstOrDefaultAsync(r => r.Id == Input.Id);

        if (role == null) return NotFound();

        var name = Input.Name.Trim();
        if (await _db.PermissionRoles.AnyAsync(r => r.Id != role.Id && r.Name.ToLower() == name.ToLower()))
        {
            ModelState.AddModelError(string.Empty, "Ya existe un rol con ese nombre.");
            return Page();
        }

        if (Input.IsDefault)
        {
            var others = await _db.PermissionRoles.Where(r => r.IsDefault && r.Id != role.Id).ToListAsync();
            foreach (var o in others) o.IsDefault = false;
        }

        role.Name = name;
        role.Description = (Input.Description ?? "").Trim();
        role.IsDefault = Input.IsDefault;
        role.IsActive = Input.IsActive;
        role.UpdatedAt = DateTime.UtcNow;

        var selected = Input.SelectedModules
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Sync modules
        var toRemove = role.Modules.Where(m => !selected.Contains(m.ModuleKey, StringComparer.OrdinalIgnoreCase)).ToList();
        foreach (var m in toRemove) role.Modules.Remove(m);

        var existing = role.Modules.Select(m => m.ModuleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in selected)
        {
            if (existing.Contains(key)) continue;
            role.Modules.Add(new PermissionRoleModule
            {
                PermissionRoleId = role.Id,
                ModuleKey = key.Trim()
            });
        }

        await _db.SaveChangesAsync();

        return RedirectToPage("./Index");
    }

    private void LoadModules()
    {
        Modules = AppModules.AllKnown
            .Select(k => new ModuleOption { Key = k, Label = AppModules.Label(k) })
            .ToList();
    }
}
