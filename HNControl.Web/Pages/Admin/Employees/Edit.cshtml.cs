using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Employees;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public EditModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public EmployeeProfile? Employee { get; set; }
    public string? Info { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public List<SelectListItem> PermissionRoleOptions { get; set; } = new();

    public class InputModel
    {
        [Required] public string UserId { get; set; } = "";

        [Required, MaxLength(120)] public string FullName { get; set; } = "";
        [MaxLength(80)] public string Position { get; set; } = "";

        [MaxLength(40)] public string? Phone { get; set; }
        [MaxLength(20)] public string? Sex { get; set; }
        [MaxLength(20)] public string? Nss { get; set; }

        [MaxLength(18)] public string? Curp { get; set; }
        [MaxLength(400)] public string? Address { get; set; }

        [DataType(DataType.Date)] public DateTime? BirthDate { get; set; }
        [DataType(DataType.Date)] public DateTime? HireDate { get; set; }

        [Required, EmailAddress] public string Email { get; set; } = "";
        [Range(0, 99999999)] public decimal SalaryBase { get; set; }

        [Required]
        public string AppRole { get; set; } = AppRoles.Employee; // Admin o Employee

        public Guid? PermissionRoleId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string userId)
    {
        Employee = await _db.EmployeeProfiles.FirstOrDefaultAsync(e => e.UserId == userId);
        if (Employee == null) return NotFound();

        var user = await _userManager.FindByIdAsync(userId);
        var email = user?.Email ?? Employee.Email ?? "";

        var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();
        var appRole = roles.Contains(AppRoles.Admin) ? AppRoles.Admin : AppRoles.Employee;

        var upr = await _db.UserPermissionRoles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);

        Input = new InputModel
        {
            UserId = Employee.UserId,
            FullName = Employee.FullName,
            Position = Employee.Position,
            Phone = Employee.Phone,
            Sex = Employee.Sex,
            Nss = Employee.Nss,
            Curp = Employee.Curp,
            Address = Employee.Address,
            BirthDate = Employee.BirthDate,
            HireDate = Employee.HireDate,
            SalaryBase = Employee.SalaryBase,
            Email = email,
            AppRole = appRole,
            PermissionRoleId = upr?.PermissionRoleId
        };

        await LoadPermissionRoleOptionsAsync(Input.PermissionRoleId);

        return Page();
    }

    private async Task LoadPermissionRoleOptionsAsync(Guid? selectedId = null)
    {
        var roles = await _db.PermissionRoles
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.IsDefault)
            .ThenBy(r => r.Name)
            .Select(r => new { r.Id, r.Name, r.IsDefault })
            .ToListAsync();

        PermissionRoleOptions = roles
            .Select(r => new SelectListItem
            {
                Value = r.Id.ToString(),
                Text = r.IsDefault ? $"{r.Name} (Default)" : r.Name,
                Selected = selectedId.HasValue && r.Id == selectedId.Value
            })
            .ToList();

        if (!selectedId.HasValue)
        {
            var def = roles.FirstOrDefault(x => x.IsDefault);
            if (def != null) Input.PermissionRoleId = def.Id;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadPermissionRoleOptionsAsync(Input.PermissionRoleId);
            return Page();
        }

        Employee = await _db.EmployeeProfiles.FirstOrDefaultAsync(e => e.UserId == Input.UserId);
        if (Employee == null) return NotFound();

        // Update profile
        Employee.FullName = Input.FullName.Trim();
        Employee.Position = (Input.Position ?? "").Trim();
        Employee.Phone = (Input.Phone ?? "").Trim();
        Employee.Sex = (Input.Sex ?? "").Trim();
        Employee.Nss = (Input.Nss ?? "").Trim();
        Employee.Curp = (Input.Curp ?? "").Trim().ToUpperInvariant();
        Employee.Address = (Input.Address ?? "").Trim();
        Employee.BirthDate = Input.BirthDate;
        Employee.HireDate = Input.HireDate;
        Employee.SalaryBase = Input.SalaryBase;
        Employee.Email = Input.Email.Trim();
        Employee.UpdatedAt = DateTime.UtcNow;

        // Update Identity email/username
        var user = await _userManager.FindByIdAsync(Input.UserId);
        if (user != null)
        {
            var newEmail = Input.Email.Trim();

            if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(user.UserName, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = newEmail;
                user.UserName = newEmail;
                await _userManager.UpdateAsync(user);
            }

            // Update role principal (Admin/Employee)
            var desiredRole = string.Equals(Input.AppRole, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
                ? AppRoles.Admin
                : AppRoles.Employee;

            var currentRoles = await _userManager.GetRolesAsync(user);
            var relevant = currentRoles.Where(r => r == AppRoles.Admin || r == AppRoles.Employee).ToList();
            foreach (var r in relevant)
            {
                if (!string.Equals(r, desiredRole, StringComparison.OrdinalIgnoreCase))
                    await _userManager.RemoveFromRoleAsync(user, r);
            }
            if (!currentRoles.Contains(desiredRole))
                await _userManager.AddToRoleAsync(user, desiredRole);
        }

        await _db.SaveChangesAsync();

        // Permisos por módulo
        if (string.Equals(Input.AppRole, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            // Admin no necesita PermissionRole
            var existing = await _db.UserPermissionRoles.FirstOrDefaultAsync(x => x.UserId == Input.UserId);
            if (existing != null)
            {
                _db.UserPermissionRoles.Remove(existing);
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            var roleId = Input.PermissionRoleId
                         ?? await _db.PermissionRoles.AsNoTracking()
                             .Where(r => r.IsDefault && r.IsActive)
                             .Select(r => (Guid?)r.Id)
                             .FirstOrDefaultAsync();
            if (roleId.HasValue)
            {
                var upr = await _db.UserPermissionRoles.FirstOrDefaultAsync(x => x.UserId == Input.UserId);
                if (upr == null)
                {
                    _db.UserPermissionRoles.Add(new UserPermissionRole
                    {
                        UserId = Input.UserId,
                        PermissionRoleId = roleId.Value,
                        AssignedAt = DateTime.UtcNow,
                        AssignedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
                    });
                }
                else
                {
                    upr.PermissionRoleId = roleId.Value;
                    upr.AssignedAt = DateTime.UtcNow;
                    upr.AssignedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
                }
                await _db.SaveChangesAsync();
            }
        }

        return RedirectToPage("./Details", new { userId = Input.UserId });
    }
}
