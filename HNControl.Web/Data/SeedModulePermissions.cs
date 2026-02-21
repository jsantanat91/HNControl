using HNControl.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Data;

public static class SeedModulePermissions
{
    public static async Task EnsureAsync(ApplicationDbContext db, UserManager<ApplicationUser> userMgr)
    {
        // 1) Default PermissionRole
        var defaultRole = await db.PermissionRoles
            .Include(r => r.Modules)
            .FirstOrDefaultAsync(r => r.IsDefault && r.IsActive);

        if (defaultRole == null)
        {
            defaultRole = new PermissionRole
            {
                Name = "Empleado básico",
                Description = "Acceso estándar para empleados.",
                IsDefault = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Modules = AppModules.EmployeeDefaults
                    .Select(k => new PermissionRoleModule { ModuleKey = k })
                    .ToList()
            };

            db.PermissionRoles.Add(defaultRole);
            await db.SaveChangesAsync();
        }
        else
        {
            // asegura módulos mínimos (si agregas módulos nuevos en código)
            var existing = defaultRole.Modules.Select(m => m.ModuleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = AppModules.EmployeeDefaults.Where(k => !existing.Contains(k)).ToList();
            if (missing.Count > 0)
            {
                foreach (var k in missing)
                    db.PermissionRoleModules.Add(new PermissionRoleModule { PermissionRoleId = defaultRole.Id, ModuleKey = k });

                defaultRole.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        // 2) Asigna default a empleados sin rol de módulos
        // (Admin bypass en runtime, pero igual evitamos asignarle)
        var employees = await userMgr.GetUsersInRoleAsync(AppRoles.Employee);
        foreach (var u in employees)
        {
            if (await userMgr.IsInRoleAsync(u, AppRoles.Admin))
                continue;

            var exists = await db.UserPermissionRoles.AnyAsync(x => x.UserId == u.Id);
            if (!exists)
            {
                db.UserPermissionRoles.Add(new UserPermissionRole
                {
                    UserId = u.Id,
                    PermissionRoleId = defaultRole.Id,
                    AssignedAt = DateTime.UtcNow,
                    AssignedByUserId = null
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
