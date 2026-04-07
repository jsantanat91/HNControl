using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    private readonly IFileStorage _storage;

    public EditModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IFileStorage storage)
    {
        _db = db;
        _userManager = userManager;
        _storage = storage;
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
        [MaxLength(13)] public string? Rfc { get; set; }
        [MaxLength(10)] public string? PostalCode { get; set; }
        [MaxLength(120)] public string? EducationLevel { get; set; }
        [MaxLength(3)] public string? SatContractTypeCode { get; set; }
        [MaxLength(3)] public string? SatWorkdayTypeCode { get; set; }
        [MaxLength(3)] public string? SatJobRiskCode { get; set; }
        [MaxLength(120)] public string? BankName { get; set; }
        [MaxLength(30)] public string? BankAccount { get; set; }
        [MaxLength(18)] public string? BankClabe { get; set; }
        [MaxLength(400)] public string? Address { get; set; }

        [DataType(DataType.Date)] public DateTime? BirthDate { get; set; }
        [DataType(DataType.Date)] public DateTime? HireDate { get; set; }

        [Required, EmailAddress] public string Email { get; set; } = "";
        [Range(0, 99999999)] public decimal SalaryBase { get; set; }
        [Range(0, 60)] public int VacationAllowanceDays { get; set; } = 12;

        [Required]
        public string AppRole { get; set; } = AppRoles.Employee; // Admin o Employee

        public Guid? PermissionRoleId { get; set; }
        public bool IsInventoryManager { get; set; }
        public IFormFile? PhotoFile { get; set; }
        public bool RemovePhoto { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string userId)
    {
        Employee = await _db.EmployeeProfiles.FirstOrDefaultAsync(e => e.UserId == userId);
        if (Employee == null) return NotFound();

        var user = await _userManager.FindByIdAsync(userId);
        var email = user?.Email ?? Employee.Email ?? "";

        var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();
        var appRole = roles.Contains(AppRoles.Admin) || roles.Contains(AppRoles.SuperAdmin) ? AppRoles.Admin : AppRoles.Employee;

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
            Rfc = Employee.Rfc,
            PostalCode = Employee.PostalCode,
            EducationLevel = Employee.EducationLevel,
            SatContractTypeCode = Employee.SatContractTypeCode,
            SatWorkdayTypeCode = Employee.SatWorkdayTypeCode,
            SatJobRiskCode = Employee.SatJobRiskCode,
            BankName = Employee.BankName,
            BankAccount = Employee.BankAccount,
            BankClabe = Employee.BankClabe,
            Address = Employee.Address,
            BirthDate = Employee.BirthDate,
            HireDate = Employee.HireDate,
            SalaryBase = Employee.SalaryBase,
            VacationAllowanceDays = Employee.VacationAllowanceDays,
            Email = email,
            AppRole = appRole,
            PermissionRoleId = upr?.PermissionRoleId,
            IsInventoryManager = roles.Contains(AppRoles.InventoryManager)
        };

        // ✅ Vacaciones automático por LFT (según fecha de ingreso)
        if (Employee.HireDate != null)
        {
            var vacDays = VacationPolicyMxLft.GetAnnualVacationDays(Employee.HireDate, DateTime.Now.Date);
            Input.VacationAllowanceDays = vacDays;
        }

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
            // ✅ Vacaciones automático por LFT (según fecha de ingreso)
        if (Employee.HireDate != null)
        {
            var vacDays = VacationPolicyMxLft.GetAnnualVacationDays(Employee.HireDate, DateTime.Now.Date);
            Input.VacationAllowanceDays = vacDays;
        }

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
        Employee.Rfc = (Input.Rfc ?? "").Trim().ToUpperInvariant();
        Employee.PostalCode = (Input.PostalCode ?? "").Trim();
        Employee.EducationLevel = (Input.EducationLevel ?? "").Trim();
        Employee.SatContractTypeCode = (Input.SatContractTypeCode ?? "").Trim();
        Employee.SatWorkdayTypeCode = (Input.SatWorkdayTypeCode ?? "").Trim();
        Employee.SatJobRiskCode = (Input.SatJobRiskCode ?? "").Trim();
        Employee.BankName = (Input.BankName ?? "").Trim();
        Employee.BankAccount = (Input.BankAccount ?? "").Trim();
        Employee.BankClabe = (Input.BankClabe ?? "").Trim();
        Employee.Address = (Input.Address ?? "").Trim();
        Employee.BirthDate = Input.BirthDate;
        Employee.HireDate = Input.HireDate;
        Employee.SalaryBase = Input.SalaryBase;
        // ✅ Vacaciones automático por LFT (según fecha de ingreso)
        var vacDays2 = (Input.HireDate != null)
            ? VacationPolicyMxLft.GetAnnualVacationDays(Input.HireDate, DateTime.Now.Date)
            : Input.VacationAllowanceDays;
        Employee.VacationAllowanceDays = vacDays2;
        Employee.Email = Input.Email.Trim();
        Employee.UpdatedAt = DateTime.UtcNow;

        if (Input.RemovePhoto)
        {
            await _storage.DeleteIfExistsAsync(Employee.ProfilePhotoStoragePath);
            Employee.ProfilePhotoStoragePath = "";
            Employee.ProfilePhotoContentType = "";
            Employee.ProfilePhotoOriginalFileName = "";
        }
        else if (Input.PhotoFile is { Length: > 0 })
        {
            if (!string.IsNullOrWhiteSpace(Employee.ProfilePhotoStoragePath))
                await _storage.DeleteIfExistsAsync(Employee.ProfilePhotoStoragePath);

            var (path, _, ct, originalName) = await _storage.SaveFileAsync(
                Input.PhotoFile,
                $"employees/{Employee.UserId}/profile",
                $"photo_{DateTime.UtcNow:yyyyMMddHHmmss}",
                new[] { ".jpg", ".jpeg", ".png", ".webp" },
                5 * 1024 * 1024);

            Employee.ProfilePhotoStoragePath = path;
            Employee.ProfilePhotoContentType = ct;
            Employee.ProfilePhotoOriginalFileName = originalName;
        }

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
            var desiredRole = IsGlobalRole(Input.AppRole)
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

            // Encargado de inventario: puede operar /Admin/Inventory sin ser admin global.
            var hasInvMgr = currentRoles.Contains(AppRoles.InventoryManager);
            if (Input.IsInventoryManager && !hasInvMgr)
                await _userManager.AddToRoleAsync(user, AppRoles.InventoryManager);
            if (!Input.IsInventoryManager && hasInvMgr)
                await _userManager.RemoveFromRoleAsync(user, AppRoles.InventoryManager);
        }

        await _db.SaveChangesAsync();

        // Permisos por módulo
        if (IsGlobalRole(Input.AppRole))
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

    private static bool IsGlobalRole(string? role)
        => string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase);
}
