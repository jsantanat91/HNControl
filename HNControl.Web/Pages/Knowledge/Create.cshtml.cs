using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Pages.Knowledge;

[Authorize(Roles = AppRoles.Admin)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [MaxLength(100)] public string Category { get; set; } = "General";
        [Required, MaxLength(200)] public string Title { get; set; } = "";
        [Required, MaxLength(600)] public string Url { get; set; } = "";
        [MaxLength(600)] public string Description { get; set; } = "";
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        _db.KnowledgeLinks.Add(new KnowledgeLink
        {
            Category = (Input.Category ?? "General").Trim(),
            Title = Input.Title.Trim(),
            Url = Input.Url.Trim(),
            Description = (Input.Description ?? "").Trim(),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return RedirectToPage("/Knowledge/Index");
    }
}
