using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Leaves;

[Authorize(Roles = AppRoles.Admin)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public CreateModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> EmployeeOptions { get; set; } = new();

    public class InputModel
    {
        [Required]
        public string UserId { get; set; } = "";

        [Required]
        public LeaveRequestType Type { get; set; } = LeaveRequestType.Medical;

        [Required, DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [MaxLength(1200)]
        public string Reason { get; set; } = "";

        public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Approved;

        [MaxLength(600)]
        public string AdminComment { get; set; } = "";

        public IFormFile[] EvidenceFiles { get; set; } = Array.Empty<IFormFile>();
    }

    public async Task OnGetAsync()
    {
        await LoadEmployeesAsync();
        var d = DateTime.Now.Date;
        Input.StartDate = d;
        Input.EndDate = d;
        Input.Status = LeaveRequestStatus.Approved;
        Input.Type = LeaveRequestType.Medical;
    }

    private async Task LoadEmployeesAsync(string? selectedUserId = null)
    {
        var employees = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new { e.UserId, e.FullName })
            .ToListAsync();

        EmployeeOptions = employees
            .Select(e => new SelectListItem(e.FullName, e.UserId) { Selected = e.UserId == selectedUserId })
            .ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadEmployeesAsync(Input.UserId);
            return Page();
        }

        var start = Input.StartDate!.Value.Date;
        var end = Input.EndDate!.Value.Date;
        if (end < start)
        {
            ModelState.AddModelError("", "La fecha final no puede ser menor que la inicial.");
            await LoadEmployeesAsync(Input.UserId);
            return Page();
        }

        var days = (end - start).Days + 1;
        if (days <= 0 || days > 365)
        {
            ModelState.AddModelError("", "Rango de días inválido.");
            await LoadEmployeesAsync(Input.UserId);
            return Page();
        }

        var now = DateTime.UtcNow;
        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var req = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            UserId = Input.UserId,
            Type = Input.Type,
            StartDate = start,
            EndDate = end,
            TotalDays = days,
            Reason = (Input.Reason ?? "").Trim(),
            Status = Input.Status,
            RequestedAt = now,
            ReviewedAt = (Input.Status == LeaveRequestStatus.Pending) ? null : now,
            ReviewedByUserId = (Input.Status == LeaveRequestStatus.Pending) ? null : reviewerId,
            AdminComment = (Input.AdminComment ?? "").Trim(),
            CreatedByAdmin = true
        };

        if (string.IsNullOrWhiteSpace(req.AdminComment) && req.Status != LeaveRequestStatus.Pending)
            req.AdminComment = req.Status == LeaveRequestStatus.Approved ? "Aprobado." : "Registrado.";

        _db.LeaveRequests.Add(req);
        await _db.SaveChangesAsync();

        // Evidencias
        var allowed = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".heic" };

        foreach (var f in (Input.EvidenceFiles ?? Array.Empty<IFormFile>()).Where(x => x != null && x.Length > 0))
        {
            var nameNoExt = $"leave_{req.Id}_{Guid.NewGuid():N}";
            var saved = await _storage.SaveFileAsync(f, "leaves", nameNoExt, allowed, 15 * 1024 * 1024);

            _db.LeaveEvidences.Add(new LeaveEvidence
            {
                Id = Guid.NewGuid(),
                LeaveRequestId = req.Id,
                OriginalFileName = saved.originalName,
                ContentType = saved.contentType,
                StoragePath = saved.storagePath,
                SizeBytes = saved.sizeBytes,
                UploadedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = "Capturado.";
        return RedirectToPage("/Admin/Leaves/Index");
    }
}
