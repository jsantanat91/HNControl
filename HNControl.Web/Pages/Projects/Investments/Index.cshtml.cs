using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects.Investments;

[Authorize(Policy = "EmployeeOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public record InvestorCard(Guid Id, string FullName, string Email, InvestmentInvestorType InvestorType, int ActivePlans, decimal PendingBalance, DateTime CreatedAt);
    public List<InvestorCard> Investors { get; set; } = new();

    public async Task OnGetAsync()
    {
        var investors = await _db.InvestmentInvestors
            .AsNoTracking()
            .Include(x => x.Plans)
            .ThenInclude(x => x.Payments)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        Investors = investors.Select(i =>
        {
            var active = i.Plans.Where(p => p.IsActive).ToList();
            var pending = active
                .SelectMany(p => p.Payments)
                .Where(x => !x.IsPaid)
                .Sum(x => x.TotalAmount);
            return new InvestorCard(i.Id, i.FullName, i.Email, i.InvestorType, active.Count, pending, i.CreatedAt);
        }).ToList();
    }
}

