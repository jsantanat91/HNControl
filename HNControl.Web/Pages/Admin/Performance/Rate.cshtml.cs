using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Performance;

[Authorize(Roles = AppRoles.Admin)]
public class RateModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public RateModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public SelectList EmployeeItems { get; set; } = new(Array.Empty<object>(), "Value", "Text");

    public string? Info { get; set; }
    public string? Error { get; set; }

    // Preview pago quincenal 80/20 (asume SalaryBase mensual)
    public decimal SalaryBase { get; set; }
    public decimal BaseQuincenal => SalaryBase / 2m;
    public decimal VariableMax20 => BaseQuincenal * 0.20m;
    public decimal Fixed80 => BaseQuincenal * 0.80m;
    public decimal VariableMoney => VariableMax20 * (Input.VariablePercentPreview ?? 0m);
    public decimal TotalQuincena => Fixed80 + VariableMoney;

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required] public string UserId { get; set; } = "";

        [DataType(DataType.Date)] public DateTime PeriodStart { get; set; }
        [DataType(DataType.Date)] public DateTime PeriodEnd { get; set; }

        [Range(1, 5)] public int PersonalPerformance { get; set; } = 3;
        [Range(1, 5)] public int Teamwork { get; set; } = 3;
        [Range(1, 5)] public int PunctualityAttendance { get; set; } = 3;
        [Range(1, 5)] public int ProjectExecution { get; set; } = 3;
        [Range(1, 5)] public int OrderCleanliness { get; set; } = 3;
        [Range(1, 5)] public int TechnicalSkills { get; set; } = 3;

        [MaxLength(600)] public string Notes { get; set; } = "";

        // Solo UI
        public decimal? VariablePercentPreview { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string? userId, DateTime? start = null, DateTime? end = null)
    {
        // 1) Periodo (quincena UTC)
        (var ps, var pe) = ResolvePeriodUtc(start, end);
        Input.PeriodStart = ps;
        Input.PeriodEnd = pe;

        // 2) Empleado seleccionado (si viene por query)
        if (!string.IsNullOrWhiteSpace(userId))
            Input.UserId = userId;

        // 3) Cargar empleados + asegurar selección
        var emps = await LoadEmployeesAsync();
        if (emps.Count == 0)
        {
            Error = "No hay empleados (perfiles) registrados. Crea al menos un empleado con su ficha.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Input.UserId))
            Input.UserId = emps[0].UserId;

        // reconstruye SelectList con selected correcto
        BuildEmployeeSelect(emps);

        // 4) Precargar evaluación existente (para que se vea que sí guardó)
        var existing = await _db.PerformanceReviews
            .AsNoTracking()
            .Where(r => r.UserId == Input.UserId
                        && r.PeriodStart >= ps && r.PeriodStart < ps.AddDays(1)
                        && r.PeriodEnd >= pe && r.PeriodEnd < pe.AddDays(1))
            .OrderByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            Input.PersonalPerformance = existing.PersonalPerformance;
            Input.Teamwork = existing.Teamwork;
            Input.PunctualityAttendance = existing.PunctualityAttendance;
            Input.ProjectExecution = existing.ProjectExecution;
            Input.OrderCleanliness = existing.OrderCleanliness;
            Input.TechnicalSkills = existing.TechnicalSkills;
            Input.Notes = existing.Notes;
            Info = "Ya existe evaluación en este periodo: se cargó para editar.";
        }

        // 5) Salary + preview
        await LoadSalaryAsync();
        Input.VariablePercentPreview = CalcVariablePercent(Input);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var emps = await LoadEmployeesAsync();
        BuildEmployeeSelect(emps);

        if (emps.Count == 0)
        {
            Error = "No hay empleados (perfiles) registrados.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Input.UserId))
        {
            Error = "Selecciona un empleado.";
            await LoadSalaryAsync();
            Input.VariablePercentPreview = CalcVariablePercent(Input);
            return Page();
        }

        // Snap a quincena UTC (para que Dashboard la encuentre)
        (var ps, var pe) = NormalizeToQuincenaUtc(Input.PeriodStart);
        Input.PeriodStart = ps;
        Input.PeriodEnd = pe;

        if (!ModelState.IsValid)
        {
            await LoadSalaryAsync();
            Input.VariablePercentPreview = CalcVariablePercent(Input);
            return Page();
        }

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        var r = await _db.PerformanceReviews
            .Where(x => x.UserId == Input.UserId
                        && x.PeriodStart >= ps && x.PeriodStart < ps.AddDays(1)
                        && x.PeriodEnd >= pe && x.PeriodEnd < pe.AddDays(1))
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync();

        if (r == null)
        {
            r = new PerformanceReview
            {
                UserId = Input.UserId,
                PeriodStart = ps,
                PeriodEnd = pe,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.PerformanceReviews.Add(r);
        }

        r.PeriodStart = ps;
        r.PeriodEnd = pe;

        r.PersonalPerformance = Input.PersonalPerformance;
        r.Teamwork = Input.Teamwork;
        r.PunctualityAttendance = Input.PunctualityAttendance;
        r.ProjectExecution = Input.ProjectExecution;
        r.OrderCleanliness = Input.OrderCleanliness;
        r.TechnicalSkills = Input.TechnicalSkills;
        r.Notes = (Input.Notes ?? "").Trim();

        r.RatedByUserId = adminId;
        r.RatedAt = DateTime.UtcNow;
        r.UpdatedAt = DateTime.UtcNow;

        // ✅ Esto alimenta el 20%
        r.Recalc();

        await _db.SaveChangesAsync();

        // ✅ regresa a la misma pantalla para que se vea que sí guardó
        return RedirectToPage("/Admin/Performance/Rate", new { userId = Input.UserId, start = ps.ToString("yyyy-MM-dd"), end = pe.ToString("yyyy-MM-dd") });
    }

    private async Task<List<EmployeeProfile>> LoadEmployeesAsync()
    {
        return await _db.EmployeeProfiles
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .ToListAsync();
    }

    private void BuildEmployeeSelect(List<EmployeeProfile> emps)
    {
        var items = emps.Select(e => new
        {
            e.UserId,
            Display = string.IsNullOrWhiteSpace(e.Position)
                ? e.FullName
                : $"{e.FullName} · {e.Position}"
        }).ToList();

        EmployeeItems = new SelectList(items, "UserId", "Display", Input.UserId);
    }

    private async Task LoadSalaryAsync()
    {
        SalaryBase = 0m;
        if (string.IsNullOrWhiteSpace(Input.UserId)) return;

        var emp = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == Input.UserId);
        if (emp != null) SalaryBase = emp.SalaryBase;
    }

    private static (DateTime ps, DateTime pe) ResolvePeriodUtc(DateTime? start, DateTime? end)
    {
        if (start.HasValue && end.HasValue)
            return (TimeUtil.UtcDate(start.Value), TimeUtil.UtcDate(end.Value));

        return NormalizeToQuincenaUtc(DateTime.Now);
    }

    private static (DateTime ps, DateTime pe) NormalizeToQuincenaUtc(DateTime anyDate)
    {
        var d = anyDate.Date;
        if (d.Day <= 15)
            return (TimeUtil.UtcDate(new DateTime(d.Year, d.Month, 1)),
                    TimeUtil.UtcDate(new DateTime(d.Year, d.Month, 15)));

        return (TimeUtil.UtcDate(new DateTime(d.Year, d.Month, 16)),
                TimeUtil.UtcDate(new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month))));
    }

    private static decimal CalcVariablePercent(InputModel input)
    {
        var avg = (input.PersonalPerformance + input.Teamwork + input.PunctualityAttendance +
                   input.ProjectExecution + input.OrderCleanliness + input.TechnicalSkills) / 6m;
        return Math.Round(avg / 5m, 4);
    }
}
