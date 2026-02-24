using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Exams;

[Authorize(Policy = "EmployeeOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;

    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr)
    {
        _db = db;
        _userMgr = userMgr;
    }

    public List<ExamAssignment> Items { get; set; } = new();

    public string StatusLabelsJson { get; set; } = "[]";
    public string StatusValuesJson { get; set; } = "[]";

    public async Task OnGetAsync()
    {
        var userId = _userMgr.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return;

        Items = await _db.ExamAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Include(a => a.Exam)
            .OrderByDescending(a => a.AssignedAt)
            .Take(50)
            .ToListAsync();

        var grouped = Items
            .GroupBy(x => x.Status)
            .OrderBy(g => g.Key)
            .Select(g => new { Status = g.Key.ToString(), Cnt = g.Count() })
            .ToList();

        StatusLabelsJson = JsonSerializer.Serialize(grouped.Select(x => x.Status));
        StatusValuesJson = JsonSerializer.Serialize(grouped.Select(x => x.Cnt));
    }
}
