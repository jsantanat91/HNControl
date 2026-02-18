using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Pages.Admin.Eval360.Campaigns;

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
        public string Title { get; set; } = "Evaluación 360";

        [MaxLength(800)]
        public string Description { get; set; } = "Evaluación por competencias (autoevaluación vs evaluadores).";

        [DataType(DataType.Date)]
        public DateTime? PeriodStart { get; set; }

        [DataType(DataType.Date)]
        public DateTime? PeriodEnd { get; set; }

        public bool AllowSelf { get; set; } = true;
        public bool ResultsVisibleToEmployee { get; set; } = true;
    }

    public void OnGet()
    {
        // Defaults: mes actual
        var now = DateTime.Now;
        Input.PeriodStart = new DateTime(now.Year, now.Month, 1);
        Input.PeriodEnd = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var c = new Eval360Campaign
        {
            Title = Input.Title.Trim(),
            Description = (Input.Description ?? "").Trim(),
            PeriodStart = Input.PeriodStart?.Date,
            PeriodEnd = Input.PeriodEnd?.Date,
            AllowSelf = Input.AllowSelf,
            ResultsVisibleToEmployee = Input.ResultsVisibleToEmployee,
            Status = Eval360CampaignStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Eval360Campaigns.Add(c);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Admin/Eval360/Campaigns/Assignments", new { id = c.Id });
    }
}
