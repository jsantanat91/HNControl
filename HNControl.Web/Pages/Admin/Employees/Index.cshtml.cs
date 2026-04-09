using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HNControl.Web.Pages.Admin.Employees;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public record Row(string UserId, string FullName, string Email, string Position, string EmployeeNumber, decimal SalaryBase, bool IsInventoryManager, bool HasPhoto, bool IsActive);
    public List<Row> Rows { get; set; } = new();
    public bool CanResetPasswords { get; set; }

    [TempData]
    public string? FlashOk { get; set; }

    [TempData]
    public string? FlashError { get; set; }

    public async Task OnGetAsync(bool includeInactive = false)
    {
        CanResetPasswords = AppRoles.IsGlobalAdmin(User);
        var inventoryRoleId = await _db.Roles
            .Where(r => r.Name == AppRoles.InventoryManager)
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var managerUserIds = string.IsNullOrWhiteSpace(inventoryRoleId)
            ? new HashSet<string>()
            : (await _db.UserRoles
                .Where(ur => ur.RoleId == inventoryRoleId)
                .Select(ur => ur.UserId)
                .ToListAsync()).ToHashSet();

        var employees = await _db.EmployeeProfiles
            .Where(e => includeInactive || e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync();

        Rows = employees
            .Select(e => new Row(
                e.UserId,
                e.FullName,
                e.Email,
                e.Position,
                e.EmployeeNumber ?? "",
                e.SalaryBase,
                managerUserIds.Contains(e.UserId),
                !string.IsNullOrWhiteSpace(e.ProfilePhotoStoragePath),
                e.IsActive))
            .ToList();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(
        string userId,
        string? newPassword,
        string? confirmPassword,
        bool includeInactive = false,
        string? view = null)
    {
        if (!AppRoles.IsGlobalAdmin(User))
            return Forbid();

        if (string.IsNullOrWhiteSpace(userId))
        {
            FlashError = "Usuario inválido.";
            return RedirectToPage(new { includeInactive, view });
        }

        newPassword = (newPassword ?? "").Trim();
        confirmPassword = (confirmPassword ?? "").Trim();
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            FlashError = "La nueva contraseña es obligatoria.";
            return RedirectToPage(new { includeInactive, view });
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            FlashError = "La confirmación de contraseña no coincide.";
            return RedirectToPage(new { includeInactive, view });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            FlashError = "No se encontró el usuario del empleado.";
            return RedirectToPage(new { includeInactive, view });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            FlashError = "No se pudo actualizar la contraseña: " + string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToPage(new { includeInactive, view });
        }

        await _userManager.UpdateSecurityStampAsync(user);
        FlashOk = $"Contraseña actualizada para {user.Email ?? user.UserName ?? user.Id}.";
        return RedirectToPage(new { includeInactive, view });
    }

    public async Task<IActionResult> OnPostDeactivateAsync(string userId, bool includeInactive = false)
    {
        var profile = await _db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile == null)
        {
            FlashError = "No se encontró el empleado.";
            return RedirectToPage(new { includeInactive });
        }

        var myUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        if (string.Equals(myUserId, userId, StringComparison.Ordinal))
        {
            FlashError = "No puedes darte de baja a ti mismo.";
            return RedirectToPage(new { includeInactive = true });
        }

        profile.IsActive = false;
        profile.UpdatedAt = DateTime.UtcNow;

        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            await _userManager.UpdateAsync(user);
        }

        await _db.SaveChangesAsync();

        FlashOk = $"Empleado desactivado: {profile.FullName}";
        return RedirectToPage(new { includeInactive = true });
    }

    public async Task<IActionResult> OnPostActivateAsync(string userId, bool includeInactive = true)
    {
        var profile = await _db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile == null)
        {
            FlashError = "No se encontró el empleado.";
            return RedirectToPage(new { includeInactive });
        }

        profile.IsActive = true;
        profile.UpdatedAt = DateTime.UtcNow;

        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            user.LockoutEnd = null;
            await _userManager.UpdateAsync(user);
        }

        await _db.SaveChangesAsync();

        FlashOk = $"Empleado reactivado: {profile.FullName}";
        return RedirectToPage(new { includeInactive = true });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string userId, bool includeInactive = true)
    {
        var profile = await _db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile == null)
        {
            FlashError = "No se encontró el empleado.";
            return RedirectToPage(new { includeInactive });
        }

        var myUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        if (string.Equals(myUserId, userId, StringComparison.Ordinal))
        {
            FlashError = "No puedes eliminar tu propio usuario.";
            return RedirectToPage(new { includeInactive = true });
        }

        if (profile.IsActive)
        {
            FlashError = "Primero da de baja (desactiva) al empleado antes de eliminar.";
            return RedirectToPage(new { includeInactive = true });
        }

        // Si tiene historial operativo, no permitimos eliminación física.
        var hasHistory =
            await _db.PerformanceReviews.AnyAsync(x => x.UserId == userId) ||
            await _db.ViaticWeeks.AnyAsync(x => x.UserId == userId) ||
            await _db.EmployeeDeductions.AnyAsync(x => x.UserId == userId) ||
            await _db.LeaveRequests.AnyAsync(x => x.UserId == userId) ||
            await _db.ExamAssignments.AnyAsync(x => x.UserId == userId) ||
            await _db.ServiceOrders.AnyAsync(x => x.AssignedUserId == userId || x.ClaimedByUserId == userId) ||
            await _db.Tickets.AnyAsync(x => x.CreatedByUserId == userId || x.AssignedToUserId == userId) ||
            await _db.SalesSellerProfiles.AnyAsync(x => x.EmployeeUserId == userId);

        if (hasHistory)
        {
            FlashError = "No se puede eliminar porque tiene historial. Usa solo baja (desactivado).";
            return RedirectToPage(new { includeInactive = true });
        }

        // Limpieza de tablas de seguridad/vistas auxiliares.
        var userPermissionRows = await _db.UserPermissionRoles.Where(x => x.UserId == userId).ToListAsync();
        if (userPermissionRows.Count > 0) _db.UserPermissionRoles.RemoveRange(userPermissionRows);

        var orgRows = await _db.EmployeeOrgChartNodes
            .Where(x => x.UserId == userId || x.ReportsToUserId == userId)
            .ToListAsync();
        if (orgRows.Count > 0) _db.EmployeeOrgChartNodes.RemoveRange(orgRows);

        _db.EmployeeProfiles.Remove(profile);
        await _db.SaveChangesAsync();

        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                FlashError = "Perfil eliminado, pero no se pudo eliminar el usuario de acceso: " +
                             string.Join("; ", result.Errors.Select(e => e.Description));
                return RedirectToPage(new { includeInactive = true });
            }
        }

        FlashOk = "Empleado eliminado correctamente.";
        return RedirectToPage(new { includeInactive = true });
    }
}
