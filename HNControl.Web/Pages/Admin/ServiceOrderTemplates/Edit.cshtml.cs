using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.ServiceOrderTemplates;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Info { get; set; }

    public class ItemInput
    {
        public Guid Id { get; set; }
        public int SortOrder { get; set; }
        public string Category { get; set; } = "General";
        public string Title { get; set; } = "";
        public bool IsRequired { get; set; } = true;
        public bool Delete { get; set; } = false;
    }

    public class InputModel
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = "";
        [Required, MaxLength(120)] public string Name { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public List<ItemInput> Items { get; set; } = new();
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var tpl = await _db.ServiceOrderChecklistTemplates
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tpl == null) return NotFound();

        Input = new InputModel
        {
            Id = tpl.Id,
            Type = tpl.Type.ToString(),
            Name = tpl.Name,
            IsActive = tpl.IsActive,
            Items = tpl.Items
                .OrderBy(i => i.SortOrder)
                .Select(i => new ItemInput
                {
                    Id = i.Id,
                    SortOrder = i.SortOrder,
                    Category = i.Category,
                    Title = i.Title,
                    IsRequired = i.IsRequired
                })
                .ToList()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var tpl = await _db.ServiceOrderChecklistTemplates
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == Input.Id);

        if (tpl == null) return NotFound();

        tpl.Name = Input.Name.Trim();
        tpl.IsActive = Input.IsActive;
        tpl.UpdatedAt = DateTime.UtcNow;

        // Update/delete existing
        foreach (var it in Input.Items)
        {
            var existing = tpl.Items.FirstOrDefault(x => x.Id == it.Id);
            if (existing == null) continue;

            if (it.Delete)
            {
                _db.ServiceOrderChecklistTemplateItems.Remove(existing);
                continue;
            }

            existing.SortOrder = it.SortOrder;
            existing.Category = (it.Category ?? "General").Trim();
            existing.Title = (it.Title ?? "").Trim();
            existing.IsRequired = it.IsRequired;
        }

        await _db.SaveChangesAsync();

        Info = "Plantilla guardada.";
        return RedirectToPage(new { id = Input.Id });
    }
}
