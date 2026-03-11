using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Eval360.Catalog;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<Eval360Competency> Competencies { get; set; } = new();

    public async Task OnGetAsync()
    {
        Competencies = await _db.Eval360Competencies
            .AsNoTracking()
            .Include(c => c.Questions.Where(q => q.IsActive))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAddCompetencyAsync(string name, int sortOrder = 0)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "El nombre de la competencia es requerido.";
            return RedirectToPage();
        }

        _db.Eval360Competencies.Add(new Eval360Competency
        {
            Name = name,
            SortOrder = sortOrder,
            IsActive = true
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Competencia agregada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateCompetencyAsync(Guid id, string name, int sortOrder = 0, bool isActive = true)
    {
        var item = await _db.Eval360Competencies.FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();

        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "El nombre no puede ir vacio.";
            return RedirectToPage();
        }

        item.Name = name;
        item.SortOrder = sortOrder;
        item.IsActive = isActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Competencia actualizada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteCompetencyAsync(Guid id)
    {
        var item = await _db.Eval360Competencies.FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();

        _db.Eval360Competencies.Remove(item);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Competencia eliminada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddQuestionAsync(Guid competencyId, string text, int sortOrder = 0)
    {
        text = (text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            TempData["Error"] = "El texto de la pregunta es requerido.";
            return RedirectToPage();
        }

        var cmp = await _db.Eval360Competencies.FirstOrDefaultAsync(x => x.Id == competencyId);
        if (cmp == null) return NotFound();

        _db.Eval360Questions.Add(new Eval360Question
        {
            CompetencyId = competencyId,
            Text = text,
            SortOrder = sortOrder,
            IsActive = true
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Pregunta agregada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateQuestionAsync(Guid id, string text, int sortOrder = 0, bool isActive = true)
    {
        var q = await _db.Eval360Questions.FirstOrDefaultAsync(x => x.Id == id);
        if (q == null) return NotFound();

        text = (text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            TempData["Error"] = "La pregunta no puede ir vacia.";
            return RedirectToPage();
        }

        q.Text = text;
        q.SortOrder = sortOrder;
        q.IsActive = isActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Pregunta actualizada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteQuestionAsync(Guid id)
    {
        var q = await _db.Eval360Questions.FirstOrDefaultAsync(x => x.Id == id);
        if (q == null) return NotFound();

        _db.Eval360Questions.Remove(q);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Pregunta eliminada.";
        return RedirectToPage();
    }
}
