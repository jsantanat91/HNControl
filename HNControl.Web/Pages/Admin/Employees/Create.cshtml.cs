using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HNControl.Web.Services;

namespace HNControl.Web.Pages.Admin.Employees;

[Authorize(Roles = AppRoles.Admin)]
public class CreateModel : PageModel
{
    private const string EmployeeNumberPrefix = "HN-NOM-5";
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public CreateModel(UserManager<ApplicationUser> userMgr, ApplicationDbContext db, IFileStorage storage)
    {
        _userMgr = userMgr;
        _db = db;
        _storage = storage;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> PermissionRoleOptions { get; set; } = new();
    public string NextEmployeeNumberPreview { get; set; } = "";

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
        [MaxLength(13)] public string Rfc { get; set; } = "";
        [MaxLength(10)] public string PostalCode { get; set; } = "";
        [MaxLength(120)] public string EducationLevel { get; set; } = "";
        [MaxLength(3)] public string SatContractTypeCode { get; set; } = "";
        [MaxLength(3)] public string SatWorkdayTypeCode { get; set; } = "";
        [MaxLength(3)] public string SatJobRiskCode { get; set; } = "";
        [MaxLength(120)] public string BankName { get; set; } = "";
        [MaxLength(30)] public string BankAccount { get; set; } = "";
        [MaxLength(18)] public string BankClabe { get; set; } = "";
        [MaxLength(400)] public string Address { get; set; } = "";

        [DataType(DataType.Date)] public DateTime? BirthDate { get; set; }
        [DataType(DataType.Date)] public DateTime? HireDate { get; set; }

        [Range(0, 9999999)] public decimal SalaryBase { get; set; }

        [Range(0, 60)] public int VacationAllowanceDays { get; set; } = 12;

        [Required]
        public string AppRole { get; set; } = AppRoles.Employee; // Admin o Employee

        public Guid? PermissionRoleId { get; set; }

        [Required] public string Password { get; set; } = "";
        public IFormFile? PhotoFile { get; set; }
    }

    public async Task OnGetAsync()
    {
        Input.AppRole = AppRoles.Employee;
        NextEmployeeNumberPreview = await NextEmployeeNumberAsync();
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
            NextEmployeeNumberPreview = await NextEmployeeNumberAsync();
            await LoadPermissionRoleOptionsAsync(Input.PermissionRoleId);
            return Page();
        }

        var existing = await _userMgr.FindByEmailAsync(Input.Email);
        if (existing != null)
        {
            Error = "Ya existe un usuario con ese correo.";
            NextEmployeeNumberPreview = await NextEmployeeNumberAsync();
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
            NextEmployeeNumberPreview = await NextEmployeeNumberAsync();
            return Page();
        }

        // Role principal (Admin/Employee)
        if (IsGlobalRole(Input.AppRole))
            await _userMgr.AddToRoleAsync(user, AppRoles.Admin);
        else
            await _userMgr.AddToRoleAsync(user, AppRoles.Employee);

                // ✅ Vacaciones automático por LFT (según fecha de ingreso). Si no hay HireDate, usamos el valor manual.
        var vacDays = Input.HireDate.HasValue
            ? VacationPolicyMxLft.GetAnnualVacationDays(Input.HireDate, DateTime.Now.Date)
            : Input.VacationAllowanceDays;

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
            Rfc = (Input.Rfc ?? "").Trim().ToUpperInvariant(),
            PostalCode = (Input.PostalCode ?? "").Trim(),
            EducationLevel = (Input.EducationLevel ?? "").Trim(),
            EmployeeNumber = await NextEmployeeNumberAsync(),
            SatContractTypeCode = (Input.SatContractTypeCode ?? "").Trim(),
            SatWorkdayTypeCode = (Input.SatWorkdayTypeCode ?? "").Trim(),
            SatJobRiskCode = (Input.SatJobRiskCode ?? "").Trim(),
            BankName = (Input.BankName ?? "").Trim(),
            BankAccount = (Input.BankAccount ?? "").Trim(),
            BankClabe = (Input.BankClabe ?? "").Trim(),
            Address = (Input.Address ?? "").Trim(),
            BirthDate = Input.BirthDate,
            HireDate = Input.HireDate,
            SalaryBase = Input.SalaryBase,
            VacationAllowanceDays = vacDays,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        if (Input.PhotoFile is { Length: > 0 })
        {
            var (path, _, ct, originalName) = await _storage.SaveFileAsync(
                Input.PhotoFile,
                $"employees/{user.Id}/profile",
                $"photo_{DateTime.UtcNow:yyyyMMddHHmmss}",
                new[] { ".jpg", ".jpeg", ".png", ".webp" },
                5 * 1024 * 1024);

            var p = await _db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == user.Id);
            if (p != null)
            {
                p.ProfilePhotoStoragePath = path;
                p.ProfilePhotoContentType = ct;
                p.ProfilePhotoOriginalFileName = originalName;
                p.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        // Asignación de rol de permisos por módulo (solo para Employee)
        if (!IsGlobalRole(Input.AppRole))
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

    private async Task<string> NextEmployeeNumberAsync()
    {
        var numbers = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.EmployeeNumber) && EF.Functions.Like(x.EmployeeNumber!, EmployeeNumberPrefix + "%"))
            .Select(x => x.EmployeeNumber!)
            .ToListAsync();

        var max = 0;
        foreach (var employeeNumber in numbers)
        {
            if (!employeeNumber.StartsWith(EmployeeNumberPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var suffix = employeeNumber[EmployeeNumberPrefix.Length..];
            if (int.TryParse(suffix, out var n) && n > max)
                max = n;
        }

        var next = max + 1;
        if (next > 9999)
            throw new InvalidOperationException("Se alcanzó el límite de números de empleado (9999).");

        return $"{EmployeeNumberPrefix}{next:000}";
    }

    private static bool IsGlobalRole(string? role)
        => string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase);
}
