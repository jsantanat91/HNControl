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

        public class InputModel
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public bool IsDefault { get; set; }
            public bool IsActive { get; set; } = true;

            // keys posteadas del checklist
            public List<string> SelectedModules { get; set; } = new();
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

            Input = new InputModel
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsDefault = role.IsDefault,
                IsActive = role.IsActive,
                SelectedModules = roleModules
            };

            LoadModuleOptions();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                LoadModuleOptions();
                return Page();
            }

            // 1) Recarga tracked (esto elimina el “grafo posteado” y mata el concurrency)
            var role = await _db.PermissionRoles
                .FirstOrDefaultAsync(x => x.Id == Input.Id);

            if (role == null)
                return NotFound(); // ya no existe

            // 2) Actualiza campos simples
            role.Name = (Input.Name ?? "").Trim();
            role.Description = (Input.Description ?? "").Trim();
            role.IsDefault = Input.IsDefault;
            role.IsActive = Input.IsActive;
            role.UpdatedAt = DateTime.UtcNow;

            // 3) Si lo marcaron default, baja el default de los demás
            if (role.IsDefault)
            {
                await _db.PermissionRoles
                    .Where(x => x.Id != role.Id && x.IsDefault)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsDefault, false)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
            }

            // 4) Reemplaza módulos de forma segura (NO Update(graph))
            var wanted = (Input.SelectedModules ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existing = await _db.PermissionRoleModules
                .Where(x => x.PermissionRoleId == role.Id)
                .ToListAsync();

            _db.PermissionRoleModules.RemoveRange(existing);

            foreach (var key in wanted)
            {
                _db.PermissionRoleModules.Add(new PermissionRoleModule
                {
                    Id = Guid.NewGuid(),
                    PermissionRoleId = role.Id,
                    ModuleKey = key
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
                // Si alguien lo tocó al mismo tiempo o había tracking raro, recarga y avisa
                TempData["Error"] = "El rol cambió mientras lo editabas. Refresca y vuelve a intentar.";
                return RedirectToPage("./Edit", new { id = Input.Id });
            }
        }

        private void LoadModuleOptions()
        {
            // Ajusta a tu fuente real de módulos si ya la tienes en un helper.
            // Aquí asumo que tienes AppModules.All o algo parecido; si no, deja hardcode.
            var all = AppModules.All; // List<(string Key, string Label)>
            ModuleOptions = all.Select(m => new SelectListItem(m.Label, m.Key)).ToList();
        }
    }
}