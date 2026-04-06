using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HNControl.Web.Data;
using HNControl.Web.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Security.Roles
{
    [Authorize(Roles = "Admin")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public EditModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<SelectListItem> ModuleOptions { get; set; } = new();
        public List<SelectListItem> ActionOptions { get; set; } = new();
        public List<PermissionGroup> ModuleGroups { get; set; } = new();
        public List<PermissionGroup> ActionGroups { get; set; } = new();
        public List<CrudMatrixRow> CrudMatrix { get; set; } = new();

        public class PermissionGroup
        {
            public string Name { get; set; } = "";
            public List<SelectListItem> Items { get; set; } = new();
        }

        public class CrudMatrixRow
        {
            public string Area { get; set; } = "";
            public string Label { get; set; } = "";
            public string? ViewKey { get; set; }
            public string? CreateKey { get; set; }
            public string? EditKey { get; set; }
            public string? ApproveKey { get; set; }
        }

        public class InputModel
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public bool IsDefault { get; set; }
            public bool IsActive { get; set; } = true;
            public List<string> SelectedModules { get; set; } = new();
            public List<string> SelectedActions { get; set; } = new();
        }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var role = await _db.PermissionRoles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (role == null) return NotFound();

            var roleModules = await _db.PermissionRoleModules
                .AsNoTracking()
                .Where(x => x.PermissionRoleId == id)
                .Select(x => x.ModuleKey)
                .ToListAsync();

            var roleActions = await _db.PermissionRoleActions
                .AsNoTracking()
                .Where(x => x.PermissionRoleId == id)
                .Select(x => x.ActionKey)
                .ToListAsync();

            Input = new InputModel
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsDefault = role.IsDefault,
                IsActive = role.IsActive,
                SelectedModules = roleModules,
                SelectedActions = roleActions
            };

            LoadOptions();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                LoadOptions();
                return Page();
            }

            var role = await _db.PermissionRoles
                .FirstOrDefaultAsync(x => x.Id == Input.Id);

            if (role == null)
                return NotFound();

            role.Name = (Input.Name ?? "").Trim();
            role.Description = (Input.Description ?? "").Trim();
            role.IsDefault = Input.IsDefault;
            role.IsActive = Input.IsActive;
            role.UpdatedAt = DateTime.UtcNow;

            if (role.IsDefault)
            {
                await _db.PermissionRoles
                    .Where(x => x.Id != role.Id && x.IsDefault)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsDefault, false)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
            }

            var wantedModules = (Input.SelectedModules ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingModules = await _db.PermissionRoleModules
                .Where(x => x.PermissionRoleId == role.Id)
                .ToListAsync();

            _db.PermissionRoleModules.RemoveRange(existingModules);

            foreach (var key in wantedModules)
            {
                _db.PermissionRoleModules.Add(new PermissionRoleModule
                {
                    Id = Guid.NewGuid(),
                    PermissionRoleId = role.Id,
                    ModuleKey = key
                });
            }

            var wantedActions = (Input.SelectedActions ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            wantedActions = AppActions.ApplyDependencies(wantedActions);

            var existingActions = await _db.PermissionRoleActions
                .Where(x => x.PermissionRoleId == role.Id)
                .ToListAsync();

            _db.PermissionRoleActions.RemoveRange(existingActions);

            foreach (var key in wantedActions)
            {
                _db.PermissionRoleActions.Add(new PermissionRoleAction
                {
                    Id = Guid.NewGuid(),
                    PermissionRoleId = role.Id,
                    ActionKey = key
                });
            }

            try
            {
                await _db.SaveChangesAsync();
                _db.PermissionAuditLogs.Add(new PermissionAuditLog
                {
                    EventType = "role.update",
                    PermissionRoleId = role.Id,
                    RoleName = role.Name,
                    ActorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    ActorName = User.Identity?.Name ?? "-",
                    Details = $"Rol actualizado. Modulos: {wantedModules.Count}, acciones: {wantedActions.Count}"
                });
                await _db.SaveChangesAsync();
                TempData["Success"] = "Rol actualizado.";
                return RedirectToPage("./Index");
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] = "El rol cambió mientras lo editabas. Refresca y vuelve a intentar.";
                return RedirectToPage("./Edit", new { id = Input.Id });
            }
        }

        private void LoadOptions()
        {
            var allModules = AppModules.All;
            ModuleOptions = allModules.Select(m => new SelectListItem(m.Label, m.Key)).ToList();

            var allActions = AppActions.AllKnown.Select(k => (Key: k, Label: AppActions.Label(k))).ToList();
            ActionOptions = allActions.Select(a => new SelectListItem(a.Label, a.Key)).ToList();

            var moduleArea = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AppModules.Clients] = "Comercial",
                [AppModules.Projects] = "Comercial",
                [AppModules.Sales] = "Comercial",
                [AppModules.Billing] = "Comercial",
                [AppModules.Carriers] = "Operación",
                [AppModules.Monitoring] = "Operación",
                [AppModules.Inventory] = "Operación",
                [AppModules.Tickets] = "Operación",
                [AppModules.ServiceOrders] = "Operación",
                [AppModules.Viaticos] = "Operación",
                [AppModules.Knowledge] = "Operación",
                [AppModules.Eval360] = "Capital Humano",
                [AppModules.Exams] = "Capital Humano",
                [AppModules.Leaves] = "Capital Humano",
                [AppModules.Performance] = "Capital Humano",
                [AppModules.Security] = "Sistema"
            };

            ModuleGroups = ModuleOptions
                .GroupBy(m => moduleArea.TryGetValue(m.Value ?? "", out var g) ? g : "Otros")
                .OrderBy(g => g.Key)
                .Select(g => new PermissionGroup
                {
                    Name = g.Key,
                    Items = g.OrderBy(x => x.Text).ToList()
                })
                .ToList();

            string ActionGroupOf(string key)
            {
                if (key.StartsWith("Projects.", StringComparison.OrdinalIgnoreCase)) return "Proyectos";
                if (key.StartsWith("Sales.", StringComparison.OrdinalIgnoreCase)) return "Ventas";
                if (key.StartsWith("Billing.", StringComparison.OrdinalIgnoreCase)) return "Facturación";
                if (key.StartsWith("Inventory.", StringComparison.OrdinalIgnoreCase)) return "Inventario";
                if (key.StartsWith("Tickets.", StringComparison.OrdinalIgnoreCase)) return "Tickets";
                if (key.StartsWith("Clients.", StringComparison.OrdinalIgnoreCase)) return "Clientes";
                if (key.StartsWith("Carriers.", StringComparison.OrdinalIgnoreCase)) return "Carriers";
                if (key.StartsWith("Monitoring.", StringComparison.OrdinalIgnoreCase)) return "Monitoreo";
                if (key.StartsWith("Templates.", StringComparison.OrdinalIgnoreCase)) return "Plantillas";
                return "Otros";
            }

            ActionGroups = ActionOptions
                .GroupBy(a => ActionGroupOf(a.Value ?? ""))
                .OrderBy(g => g.Key)
                .Select(g => new PermissionGroup
                {
                    Name = g.Key,
                    Items = g.OrderBy(x => x.Text).ToList()
                })
                .ToList();

            CrudMatrix = new List<CrudMatrixRow>
            {
                new() { Area = "Clientes", Label = "Clientes", ViewKey = AppActions.ClientsView, CreateKey = AppActions.ClientsEdit, EditKey = AppActions.ClientsEdit },
                new() { Area = "Proyectos", Label = "Proyectos base", ViewKey = AppActions.ProjectsView, CreateKey = AppActions.ProjectsEdit, EditKey = AppActions.ProjectsEdit },
                new() { Area = "Proyectos", Label = "Inversiones", ViewKey = AppActions.ProjectsInvestmentsView, CreateKey = AppActions.ProjectsInvestmentsEdit, EditKey = AppActions.ProjectsInvestmentsEdit },
                new() { Area = "Proyectos", Label = "Reseller", ViewKey = AppActions.ProjectsResellersView, CreateKey = AppActions.ProjectsResellersEdit, EditKey = AppActions.ProjectsResellersEdit },
                new() { Area = "Proyectos", Label = "Formato de entrega", ViewKey = AppActions.ProjectsDeliveryView, CreateKey = AppActions.ProjectsDeliveryEdit, EditKey = AppActions.ProjectsDeliveryEdit },
                new() { Area = "Ventas", Label = "Dashboard/Workflow", ViewKey = AppActions.SalesViewOwn, EditKey = AppActions.SalesWorkflowMove, ApproveKey = AppActions.SalesWorkflowAssign },
                new() { Area = "Ventas", Label = "Gestion comercial", ViewKey = AppActions.SalesViewAll, CreateKey = AppActions.SalesManage, EditKey = AppActions.SalesManage },
                new() { Area = "Ventas", Label = "Cotizaciones", ViewKey = AppActions.SalesQuotesView, CreateKey = AppActions.SalesQuotesManage, EditKey = AppActions.SalesQuotesManage },
                new() { Area = "Ventas", Label = "Catálogo de cotizaciones", ViewKey = AppActions.SalesCatalogView, CreateKey = AppActions.SalesCatalogManage, EditKey = AppActions.SalesCatalogManage },
                new() { Area = "Ventas", Label = "Prospectos", ViewKey = AppActions.SalesProspectsView, CreateKey = AppActions.SalesProspectsCreate, EditKey = AppActions.SalesProspectsEdit, ApproveKey = AppActions.SalesProspectsConvert },
                new() { Area = "Facturacion", Label = "Facturacion", ViewKey = AppActions.BillingViewOwn, CreateKey = AppActions.BillingManage, EditKey = AppActions.BillingManage, ApproveKey = AppActions.BillingSend },
                new() { Area = "Inventario", Label = "Stock y movimientos", ViewKey = AppActions.InventoryView, CreateKey = AppActions.InventoryManage, EditKey = AppActions.InventoryManage, ApproveKey = AppActions.InventoryApprove },
                new() { Area = "Tickets", Label = "Tickets", ViewKey = AppActions.TicketsView, CreateKey = AppActions.TicketsManage, EditKey = AppActions.TicketsManage, ApproveKey = AppActions.TicketsClose },
                new() { Area = "Carriers", Label = "Carriers", ViewKey = AppActions.CarriersView, CreateKey = AppActions.CarriersManage, EditKey = AppActions.CarriersManage },
                new() { Area = "Carriers", Label = "Monitoreo", ViewKey = AppActions.MonitoringView, CreateKey = AppActions.MonitoringManage, EditKey = AppActions.MonitoringManage }
            };
        }
    }
}
