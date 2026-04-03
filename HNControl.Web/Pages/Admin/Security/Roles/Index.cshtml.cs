using HNControl.Web.Data;
using HNControl.Web.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Security.Roles;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<Row> Roles { get; set; } = new();
    public List<AuditRow> Audit { get; set; } = new();

    public class AuditRow
    {
        public DateTime CreatedAt { get; set; }
        public string EventType { get; set; } = "";
        public string RoleName { get; set; } = "";
        public string ActorName { get; set; } = "";
        public string Details { get; set; } = "";
    }

    public class Row
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public int ModulesCount { get; set; }
    }

    public async Task OnGetAsync()
    {
        Roles = await _db.PermissionRoles
            .AsNoTracking()
            .Select(r => new Row
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                IsDefault = r.IsDefault,
                IsActive = r.IsActive,
                ModulesCount = r.Modules.Count
            })
            .OrderByDescending(r => r.IsDefault)
            .ThenByDescending(r => r.IsActive)
            .ThenBy(r => r.Name)
            .ToListAsync();

        Audit = await _db.PermissionAuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(40)
            .Select(x => new AuditRow
            {
                CreatedAt = x.CreatedAt,
                EventType = x.EventType,
                RoleName = x.RoleName,
                ActorName = x.ActorName,
                Details = x.Details
            })
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostCloneTemplateAsync(string templateKey)
    {
        var preset = GetPreset(templateKey);
        if (preset == null)
            return RedirectToPage();

        var baseName = preset.Value.Name;
        var name = baseName;
        var i = 2;
        while (await _db.PermissionRoles.AnyAsync(r => r.Name == name))
        {
            name = $"{baseName} ({i++})";
        }

        var role = new PermissionRole
        {
            Name = name,
            Description = preset.Value.Description,
            IsDefault = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.PermissionRoles.Add(role);
        foreach (var m in preset.Value.Modules.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _db.PermissionRoleModules.Add(new PermissionRoleModule
            {
                PermissionRoleId = role.Id,
                ModuleKey = m
            });
        }
        foreach (var a in preset.Value.Actions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _db.PermissionRoleActions.Add(new PermissionRoleAction
            {
                PermissionRoleId = role.Id,
                ActionKey = a
            });
        }

        _db.PermissionAuditLogs.Add(new PermissionAuditLog
        {
            EventType = "role.clone.template",
            PermissionRoleId = role.Id,
            RoleName = role.Name,
            ActorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorName = User.Identity?.Name ?? "-",
            Details = $"Rol clonado desde plantilla: {preset.Value.Name}"
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Plantilla '{preset.Value.Name}' clonada como '{role.Name}'.";
        return RedirectToPage();
    }

    private static (string Name, string Description, string[] Modules, string[] Actions)? GetPreset(string key)
    {
        return key?.Trim().ToLowerInvariant() switch
        {
            "tecnico" => (
                "Tecnico",
                "Perfil operativo de campo con acceso de consulta y ejecucion basica.",
                new[]
                {
                    AppModules.ServiceOrders, AppModules.Viaticos, AppModules.Tickets, AppModules.Inventory,
                    AppModules.Carriers, AppModules.Monitoring, AppModules.Projects
                },
                new[]
                {
                    AppActions.ProjectsView, AppActions.TicketsView, AppActions.TicketsManage, AppActions.InventoryView,
                    AppActions.CarriersView, AppActions.MonitoringView, AppActions.SalesQuotesView
                }),
            "supervisor" => (
                "Supervisor",
                "Perfil de supervision con aprobaciones y vista global operativa.",
                new[]
                {
                    AppModules.ServiceOrders, AppModules.Viaticos, AppModules.Tickets, AppModules.Inventory,
                    AppModules.Carriers, AppModules.Monitoring, AppModules.Projects, AppModules.Clients
                },
                new[]
                {
                    AppActions.ClientsView, AppActions.ProjectsView, AppActions.ProjectsEdit,
                    AppActions.TicketsView, AppActions.TicketsManage, AppActions.TicketsClose,
                    AppActions.InventoryView, AppActions.InventoryManage, AppActions.InventoryApprove,
                    AppActions.CarriersView, AppActions.CarriersManage, AppActions.MonitoringView, AppActions.MonitoringManage
                }),
            "vendedor" => (
                "Vendedor",
                "Perfil comercial de cotizacion, pipeline y seguimiento de ventas.",
                new[]
                {
                    AppModules.Sales, AppModules.Clients, AppModules.Projects
                },
                new[]
                {
                    AppActions.ClientsView,
                    AppActions.SalesViewOwn, AppActions.SalesWorkflowMove, AppActions.SalesQuotesView, AppActions.SalesQuotesManage,
                    AppActions.ProjectsView
                }),
            "almacen" => (
                "Almacen",
                "Perfil de almacen con gestion y aprobacion de inventario.",
                new[]
                {
                    AppModules.Inventory, AppModules.Tickets
                },
                new[]
                {
                    AppActions.InventoryView, AppActions.InventoryManage, AppActions.InventoryApprove,
                    AppActions.TicketsView
                }),
            _ => null
        };
    }
}
