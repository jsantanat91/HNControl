using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Pages.Admin.Exams;

[Authorize(Roles = AppRoles.Admin)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public CreateModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        [MaxLength(2000)]
        public string Description { get; set; } = "";

        public int? TimeLimitMinutes { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public void OnGet()
    {
        Input.IsActive = true;
        Input.TimeLimitMinutes = null;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            Title = (Input.Title ?? "").Trim(),
            Description = (Input.Description ?? "").Trim(),
            TimeLimitMinutes = Input.TimeLimitMinutes,
            IsActive = Input.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };

        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Examen creado.";
        return RedirectToPage("/Admin/Exams/Edit", new { id = exam.Id });
    }
}
