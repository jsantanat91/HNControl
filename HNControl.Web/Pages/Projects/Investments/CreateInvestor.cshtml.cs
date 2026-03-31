using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Pages.Projects.Investments;

[Authorize(Roles = AppRoles.Admin)]
public class CreateInvestorModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateInvestorModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, MaxLength(200)]
        public string FullName { get; set; } = "";

        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = "";

        [MaxLength(40)]
        public string Phone { get; set; } = "";

        [MaxLength(1200)]
        public string Notes { get; set; } = "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var now = DateTime.UtcNow;
        _db.InvestmentInvestors.Add(new InvestmentInvestor
        {
            FullName = Input.FullName.Trim(),
            Email = Input.Email.Trim(),
            Phone = Input.Phone?.Trim() ?? "",
            Notes = Input.Notes?.Trim() ?? "",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _db.SaveChangesAsync();
        return RedirectToPage("/Projects/Investments/Index");
    }
}
