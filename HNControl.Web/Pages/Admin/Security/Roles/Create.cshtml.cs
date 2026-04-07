using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Security.Roles;

[Authorize(Roles = AppRoles.Admin)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    public List<ModuleOption> Modules { get; set; } = new();
    public List<ModuleOption> Actions { get; set; } = new();
    public List<PermissionGroup> ModuleGroups { get; set; } = new();
    public List<PermissionGroup> ActionGroups { get; set; } = new();
    public List<CrudMatrixRow> CrudMatrix { get; set; } = new();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class ModuleOption
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
    }

    public class PermissionGroup
    {
        public string Name { get; set; } = "";
        public List<ModuleOption> Items { get; set; } = new();
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
        [Required, MaxLength(80)]
        public string Name { get; set; } = "";

        [MaxLength(400)]
        public string Description { get; set; } = "";

        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;

        public List<string> SelectedModules { get; set; } = new();
        public List<string> SelectedActions { get; set; } = new();
    }

    public void OnGet()
    {
        LoadModules();
        Input.SelectedModules = AppModules.EmployeeDefaults.ToList();
        Input.SelectedActions = AppActions.EmployeeDefaults.ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        LoadModules();
        if (!ModelState.IsValid) return Page();

        var name = Input.Name.Trim();
        if (await _db.PermissionRoles.AnyAsync(r => r.Name.ToLower() == name.ToLower()))
        {
            ModelState.AddModelError(string.Empty, "Ya existe un rol con ese nombre.");
            return Page();
        }

        if (Input.IsDefault)
        {
            var others = await _db.PermissionRoles.Where(r => r.IsDefault).ToListAsync();
            foreach (var o in others) o.IsDefault = false;
        }

        var role = new PermissionRole
        {
            Name = name,
            Description = (Input.Description ?? "").Trim(),
            IsDefault = Input.IsDefault,
            IsActive = Input.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Modules = new List<PermissionRoleModule>()
        };

        var selected = Input.SelectedModules
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var key in selected)
        {
            role.Modules.Add(new PermissionRoleModule
            {
                PermissionRoleId = role.Id,
                ModuleKey = key.Trim()
            });
        }

        var selectedActions = Input.SelectedActions
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        selectedActions = AppActions.ApplyDependencies(selectedActions);

        foreach (var key in selectedActions)
        {
            _db.PermissionRoleActions.Add(new PermissionRoleAction
            {
                PermissionRoleId = role.Id,
                ActionKey = key.Trim()
            });
        }

        _db.PermissionRoles.Add(role);
        await _db.SaveChangesAsync();

        _db.PermissionAuditLogs.Add(new PermissionAuditLog
        {
            EventType = "role.create",
            PermissionRoleId = role.Id,
            RoleName = role.Name,
            ActorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorName = User.Identity?.Name ?? "-",
            Details = $"Rol creado. Modulos: {selected.Count}, acciones: {selectedActions.Count}"
        });
        await _db.SaveChangesAsync();

        return RedirectToPage("./Index");
    }

    private void LoadModules()
    {
        Modules = AppModules.AllKnown
            .Select(k => new ModuleOption { Key = k, Label = AppModules.Label(k) })
            .ToList();

        Actions = AppActions.AllKnown
            .Select(k => new ModuleOption { Key = k, Label = AppActions.Label(k) })
            .ToList();

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

        ModuleGroups = Modules
            .GroupBy(m => moduleArea.TryGetValue(m.Key, out var g) ? g : "Otros")
            .OrderBy(g => g.Key)
            .Select(g => new PermissionGroup
            {
                Name = g.Key,
                Items = g.OrderBy(x => x.Label).ToList()
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
            if (key.StartsWith("Employees.", StringComparison.OrdinalIgnoreCase)) return "Empleados";
            return "Otros";
        }

        ActionGroups = Actions
            .GroupBy(a => ActionGroupOf(a.Key))
            .OrderBy(g => g.Key)
            .Select(g => new PermissionGroup
            {
                Name = g.Key,
                Items = g.OrderBy(x => x.Label).ToList()
            })
            .ToList();

        CrudMatrix = new List<CrudMatrixRow>
        {
            new() { Area = "Clientes", Label = "Clientes", ViewKey = AppActions.ClientsView, CreateKey = AppActions.ClientsEdit, EditKey = AppActions.ClientsEdit },
            new() { Area = "Proyectos", Label = "Proyectos base", ViewKey = AppActions.ProjectsView, CreateKey = AppActions.ProjectsEdit, EditKey = AppActions.ProjectsEdit },
            new() { Area = "Proyectos", Label = "Inversiones", ViewKey = AppActions.ProjectsInvestmentsView, CreateKey = AppActions.ProjectsInvestmentsEdit, EditKey = AppActions.ProjectsInvestmentsEdit },
            new() { Area = "Proyectos", Label = "Reseller", ViewKey = AppActions.ProjectsResellersView, CreateKey = AppActions.ProjectsResellersEdit, EditKey = AppActions.ProjectsResellersEdit },
            new() { Area = "Proyectos", Label = "Formato de entrega", ViewKey = AppActions.ProjectsDeliveryView, CreateKey = AppActions.ProjectsDeliveryEdit, EditKey = AppActions.ProjectsDeliveryEdit },
            new() { Area = "Ventas", Label = "Dashboard", ViewKey = AppActions.SalesViewOwn, ApproveKey = AppActions.SalesViewAll },
            new() { Area = "Ventas", Label = "Workflow (arrastre)", ViewKey = AppActions.SalesViewOwn, EditKey = AppActions.SalesWorkflowMove, ApproveKey = AppActions.SalesWorkflowAssign },
            new() { Area = "Ventas", Label = "Gestion comercial", ViewKey = AppActions.SalesViewOwn, CreateKey = AppActions.SalesManage, EditKey = AppActions.SalesManage, ApproveKey = AppActions.SalesWorkflowAssign },
            new() { Area = "Ventas", Label = "Cotizacion", ViewKey = AppActions.SalesQuotesView, CreateKey = AppActions.SalesQuotesManage, EditKey = AppActions.SalesQuotesManage },
            new() { Area = "Ventas", Label = "Llamadas (WebPhone)", ViewKey = AppActions.SalesCallsView, CreateKey = AppActions.SalesCallsUse, EditKey = AppActions.SalesCallsUse },
            new() { Area = "Ventas", Label = "Prospectos", ViewKey = AppActions.SalesProspectsView, CreateKey = AppActions.SalesProspectsCreate, EditKey = AppActions.SalesProspectsEdit, ApproveKey = AppActions.SalesProspectsConvert },
            new() { Area = "Ventas", Label = "Catalogo", ViewKey = AppActions.SalesCatalogView, CreateKey = AppActions.SalesCatalogManage, EditKey = AppActions.SalesCatalogManage },
            new() { Area = "Facturacion", Label = "Facturacion", ViewKey = AppActions.BillingViewOwn, CreateKey = AppActions.BillingManage, EditKey = AppActions.BillingManage, ApproveKey = AppActions.BillingSend },
            new() { Area = "Capital Humano", Label = "Organigrama", ViewKey = AppActions.EmployeesOrgChartView, EditKey = AppActions.EmployeesOrgChartEdit, ApproveKey = AppActions.EmployeesOrgChartExport },
            new() { Area = "Inventario", Label = "Stock y movimientos", ViewKey = AppActions.InventoryView, CreateKey = AppActions.InventoryManage, EditKey = AppActions.InventoryManage, ApproveKey = AppActions.InventoryApprove },
            new() { Area = "Tickets", Label = "Tickets", ViewKey = AppActions.TicketsView, CreateKey = AppActions.TicketsManage, EditKey = AppActions.TicketsManage, ApproveKey = AppActions.TicketsClose },
            new() { Area = "Carriers", Label = "Carriers", ViewKey = AppActions.CarriersView, CreateKey = AppActions.CarriersManage, EditKey = AppActions.CarriersManage },
            new() { Area = "Carriers", Label = "Monitoreo", ViewKey = AppActions.MonitoringView, CreateKey = AppActions.MonitoringManage, EditKey = AppActions.MonitoringManage }
        };
    }
}
