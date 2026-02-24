using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Leaves;

[Authorize(Policy = "EmployeeOnly")]
public class RequestVacationModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IFileStorage _storage;

    public RequestVacationModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IFileStorage storage)
    {
        _db = db;
        _userMgr = userMgr;
        _storage = storage;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [MaxLength(1200)]
        public string Reason { get; set; } = "";

        public IFormFile[] EvidenceFiles { get; set; } = Array.Empty<IFormFile>();
    }

    public void OnGet()
    {
        // Defaults: hoy + 1
        var d = DateTime.Now.Date;
        Input.StartDate = d;
        Input.EndDate = d;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var userId = _userMgr.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        var profile = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile == null)
        {
            ModelState.AddModelError("", "No tienes perfil. Pídele al admin que lo cree.");
            return Page();
        }

        var start = Input.StartDate!.Value.Date;
        var end = Input.EndDate!.Value.Date;
        if (end < start)
        {
            ModelState.AddModelError("", "La fecha final no puede ser menor que la inicial.");
            return Page();
        }

        var days = (end - start).Days + 1;
        if (days <= 0 || days > 365)
        {
            ModelState.AddModelError("", "Rango de días inválido.");
            return Page();
        }

        var req = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = LeaveRequestType.Vacation,
            StartDate = start,
            EndDate = end,
            TotalDays = days,
            Reason = (Input.Reason ?? "").Trim(),
            Status = LeaveRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            CreatedByAdmin = false
        };

        _db.LeaveRequests.Add(req);
        await _db.SaveChangesAsync();

        // Evidencias (opcional)
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

        TempData["Success"] = "Solicitud enviada. RH ya tiene el balón.";
        return RedirectToPage("/Leaves/Index");
    }
}
