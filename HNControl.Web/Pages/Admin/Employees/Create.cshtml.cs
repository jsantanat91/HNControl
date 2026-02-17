using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Pages.Employees;

public class CreateModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly ApplicationDbContext _db;

    public CreateModel(UserManager<ApplicationUser> userMgr, ApplicationDbContext db)
    {
        _userMgr = userMgr;
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public string FullName { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        public string Nss { get; set; } = "";
        public string Gender { get; set; } = "N/A";
        public string Position { get; set; } = "";
        public string Phone { get; set; } = "";
        [Range(0, 9999999)] public decimal SalaryBase { get; set; }
        [Required] public string Password { get; set; } = "";
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var existing = await _userMgr.FindByEmailAsync(Input.Email);
        if (existing != null)
        {
            Error = "Ya existe un usuario con ese correo.";
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            EmailConfirmed = true
        };

        var created = await _userMgr.CreateAsync(user, Input.Password);
        if (!created.Succeeded)
        {
            Error = string.Join("; ", created.Errors.Select(e => e.Description));
            return Page();
        }

        await _userMgr.AddToRoleAsync(user, AppRoles.Employee);

        _db.EmployeeProfiles.Add(new EmployeeProfile
        {
            UserId = user.Id,
            FullName = Input.FullName,
            Email = Input.Email,
            Nss = Input.Nss,
            Gender = Input.Gender,
            Position = Input.Position,
            Phone = Input.Phone,
            SalaryBase = Input.SalaryBase,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return RedirectToPage("/Employees/Index");
    }
}
