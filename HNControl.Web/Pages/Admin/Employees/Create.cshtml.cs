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

    public List<SelectListItem> PermissionRoleOptions { get; set; } = new();

    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public string FullName { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        public string Nss { get; set; } = "";
        [Required] public string Gender { get; set; } = "Hombre";
        public string Position { get; set; } = "";
        public string Phone { get; set; } = "";

        [MaxLength(18)] public string Curp { get; set; } = "";
        [MaxLength(400)] public string Address { get; set; } = "";

        [DataType(DataType.Date)] public DateTime? BirthDate { get; set; }
        [DataType(DataType.Date)] public DateTime? HireDate { get; set; }

        [Range(0, 9999999)] public decimal SalaryBase { get; set; }

        [Required]
        public string AppRole { get; set; } = AppRoles.Employee; // Admin o Employee

        public Guid? PermissionRoleId { get; set; }

        [Required] public string Password { get; set; } = "";
    }

    public async Task OnGetAsync()
    {
        Input.AppRole = AppRoles.Employee;
        await LoadPermissionRoleOptionsAsync();
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

        // Role principal (Admin/Employee)
        if (string.Equals(Input.AppRole, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
            await _userMgr.AddToRoleAsync(user, AppRoles.Admin);
        else
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
            Curp = (Input.Curp ?? "").Trim().ToUpperInvariant(),
            Address = (Input.Address ?? "").Trim(),
            BirthDate = Input.BirthDate,
            HireDate = Input.HireDate,
            SalaryBase = Input.SalaryBase,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        // Asignación de rol de permisos por módulo (solo para Employee)
        if (!string.Equals(Input.AppRole, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            var roleId = Input.PermissionRoleId
                         ?? await _db.PermissionRoles
                             .AsNoTracking()
                             .Where(r => r.IsDefault && r.IsActive)
                             .Select(r => (Guid?)r.Id)
                             .FirstOrDefaultAsync();

            if (roleId.HasValue)
            {
                _db.UserPermissionRoles.Add(new UserPermissionRole
                {
                    UserId = user.Id,
                    PermissionRoleId = roleId.Value,
                    AssignedAt = DateTime.UtcNow,
                    AssignedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
                });
                await _db.SaveChangesAsync();
            }
        }
        return RedirectToPage("/Admin/Employees/Index");
    }
}
