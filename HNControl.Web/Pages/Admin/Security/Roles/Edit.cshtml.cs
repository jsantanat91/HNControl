using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Security.Roles
{
    [Authorize(Roles = "Admin")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public EditModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<SelectListItem> ModuleOptions { get; set; } = new();
        public List<SelectListItem> ActionOptions { get; set; } = new();

        public class InputModel
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public bool IsDefault { get; set; }
            public bool IsActive { get; set; } = true;
            public List<string> SelectedModules { get; set; } = new();
            public List<string> SelectedActions { get; set; } = new();
        }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var role = await _db.PermissionRoles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (role == null) return NotFound();

            var roleModules = await _db.PermissionRoleModules
                .AsNoTracking()
                .Where(x => x.PermissionRoleId == id)
                .Select(x => x.ModuleKey)
                .ToListAsync();

            var roleActions = await _db.PermissionRoleActions
                .AsNoTracking()
                .Where(x => x.PermissionRoleId == id)
                .Select(x => x.ActionKey)
                .ToListAsync();

            Input = new InputModel
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsDefault = role.IsDefault,
                IsActive = role.IsActive,
                SelectedModules = roleModules,
                SelectedActions = roleActions
            };

            LoadOptions();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                LoadOptions();
                return Page();
            }

            var role = await _db.PermissionRoles
                .FirstOrDefaultAsync(x => x.Id == Input.Id);

            if (role == null)
                return NotFound();

            role.Name = (Input.Name ?? "").Trim();
            role.Description = (Input.Description ?? "").Trim();
            role.IsDefault = Input.IsDefault;
            role.IsActive = Input.IsActive;
            role.UpdatedAt = DateTime.UtcNow;

            if (role.IsDefault)
            {
                await _db.PermissionRoles
                    .Where(x => x.Id != role.Id && x.IsDefault)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsDefault, false)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
            }

            var wantedModules = (Input.SelectedModules ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingModules = await _db.PermissionRoleModules
                .Where(x => x.PermissionRoleId == role.Id)
                .ToListAsync();

            _db.PermissionRoleModules.RemoveRange(existingModules);

            foreach (var key in wantedModules)
            {
                _db.PermissionRoleModules.Add(new PermissionRoleModule
                {
                    Id = Guid.NewGuid(),
                    PermissionRoleId = role.Id,
                    ModuleKey = key
                });
            }

            var wantedActions = (Input.SelectedActions ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingActions = await _db.PermissionRoleActions
                .Where(x => x.PermissionRoleId == role.Id)
                .ToListAsync();

            _db.PermissionRoleActions.RemoveRange(existingActions);

            foreach (var key in wantedActions)
            {
                _db.PermissionRoleActions.Add(new PermissionRoleAction
                {
                    Id = Guid.NewGuid(),
                    PermissionRoleId = role.Id,
                    ActionKey = key
                });
            }

            try
            {
                await _db.SaveChangesAsync();
                TempData["Success"] = "Rol actualizado.";
                return RedirectToPage("./Index");
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] = "El rol cambió mientras lo editabas. Refresca y vuelve a intentar.";
                return RedirectToPage("./Edit", new { id = Input.Id });
            }
        }

        private void LoadOptions()
        {
            var allModules = AppModules.All;
            ModuleOptions = allModules.Select(m => new SelectListItem(m.Label, m.Key)).ToList();

            var allActions = AppActions.AllKnown.Select(k => (Key: k, Label: AppActions.Label(k))).ToList();
            ActionOptions = allActions.Select(a => new SelectListItem(a.Label, a.Key)).ToList();
        }
    }
}
