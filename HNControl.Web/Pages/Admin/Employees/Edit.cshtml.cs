using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Employees;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public EditModel(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public EmployeeProfile? Employee { get; set; }
    public string? Info { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required] public string UserId { get; set; } = "";

        [Required, MaxLength(120)] public string FullName { get; set; } = "";
        [MaxLength(80)] public string Position { get; set; } = "";

        [MaxLength(40)] public string? Phone { get; set; }
        [MaxLength(1)] public string? Sex { get; set; }
        [MaxLength(20)] public string? Nss { get; set; }

        [Required, EmailAddress] public string Email { get; set; } = "";
        [Range(0, 99999999)] public decimal SalaryBase { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string userId)
    {
        Employee = await _db.EmployeeProfiles.FirstOrDefaultAsync(e => e.UserId == userId);
        if (Employee == null) return NotFound();

        var user = await _userManager.FindByIdAsync(userId);
        var email = user?.Email ?? "";

        Input = new InputModel
        {
            UserId = Employee.UserId,
            FullName = Employee.FullName,
            Position = Employee.Position,
            Phone = Employee.Phone,
            Sex = Employee.Sex,
            Nss = Employee.Nss,
            SalaryBase = Employee.SalaryBase,
            Email = email
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        Employee = await _db.EmployeeProfiles.FirstOrDefaultAsync(e => e.UserId == Input.UserId);
        if (Employee == null) return NotFound();

        // Update profile
        Employee.FullName = Input.FullName.Trim();
        Employee.Position = (Input.Position ?? "").Trim();
        Employee.Phone = (Input.Phone ?? "").Trim();
        Employee.Sex = (Input.Sex ?? "").Trim();
        Employee.Nss = (Input.Nss ?? "").Trim();
        Employee.SalaryBase = Input.SalaryBase;

        // Update Identity email
        var user = await _userManager.FindByIdAsync(Input.UserId);
        if (user != null)
        {
            if (!string.Equals(user.Email, Input.Email, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = Input.Email.Trim();
                user.UserName = Input.Email.Trim();
                await _userManager.UpdateAsync(user);
            }
        }

        await _db.SaveChangesAsync();

        Info = "Empleado actualizado.";
        return RedirectToPage("/Admin/Employees/Details", new { userId = Input.UserId });
    }
}
