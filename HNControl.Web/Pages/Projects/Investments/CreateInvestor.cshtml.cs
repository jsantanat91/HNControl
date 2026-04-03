using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects.Investments;

[Authorize(Policy = "EmployeeOnly")]
public class CreateInvestorModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateInvestorModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new();
    public List<SelectListItem> EmployeeItems { get; set; } = new();
    public List<EmployeeOption> EmployeeOptions { get; set; } = new();

    public record EmployeeOption(string UserId, string FullName, string Email, string Phone);

    public class InputModel
    {
        public InvestmentInvestorType InvestorType { get; set; } = InvestmentInvestorType.External;
        public string? EmployeeUserId { get; set; }

        [MaxLength(200)]
        public string FullName { get; set; } = "";

        [EmailAddress, MaxLength(256)]
        public string Email { get; set; } = "";

        [MaxLength(40)]
        public string Phone { get; set; } = "";

        [MaxLength(1200)]
        public string Notes { get; set; } = "";
    }

    public async Task OnGetAsync()
    {
        await LoadEmployeesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadEmployeesAsync();
        if (!ModelState.IsValid) return Page();

        var now = DateTime.UtcNow;
        var fullName = Input.FullName?.Trim() ?? "";
        var email = Input.Email?.Trim() ?? "";
        var phone = Input.Phone?.Trim() ?? "";
        string? employeeUserId = null;

        if (Input.InvestorType == InvestmentInvestorType.Employee)
        {
            if (string.IsNullOrWhiteSpace(Input.EmployeeUserId))
            {
                ModelState.AddModelError("", "Selecciona un empleado.");
                return Page();
            }

            var emp = await _db.EmployeeProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == Input.EmployeeUserId);
            if (emp == null)
            {
                ModelState.AddModelError("", "Empleado no encontrado.");
                return Page();
            }

            employeeUserId = emp.UserId;
            fullName = emp.FullName?.Trim() ?? "";
            email = string.IsNullOrWhiteSpace(Input.Email) ? (emp.Email ?? "").Trim() : email;
            phone = string.IsNullOrWhiteSpace(Input.Phone) ? (emp.Phone ?? "").Trim() : phone;
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            ModelState.AddModelError("", "Nombre completo es obligatorio.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError("", "Correo es obligatorio para envio de estado de cuenta.");
            return Page();
        }

        _db.InvestmentInvestors.Add(new InvestmentInvestor
        {
            InvestorType = Input.InvestorType,
            EmployeeUserId = employeeUserId,
            FullName = fullName,
            Email = email,
            Phone = phone,
            Notes = Input.Notes?.Trim() ?? "",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _db.SaveChangesAsync();
        return RedirectToPage("/Projects/Investments/Index");
    }

    private async Task LoadEmployeesAsync()
    {
        EmployeeOptions = await _db.EmployeeProfiles
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new EmployeeOption(
                x.UserId,
                x.FullName,
                x.Email ?? "",
                x.Phone ?? ""))
            .ToListAsync();

        EmployeeItems = EmployeeOptions
            .Select(x => new SelectListItem(x.FullName, x.UserId))
            .ToList();
    }
}

