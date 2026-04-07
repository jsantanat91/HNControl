using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public class SalesAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SalesOpportunityId { get; set; }
    public SalesOpportunity? SalesOpportunity { get; set; }

    [MaxLength(80)]
    public string EventType { get; set; } = "";

    [MaxLength(64)]
    public string? UserId { get; set; }

    [MaxLength(180)]
    public string UserName { get; set; } = "";

    public SalesWorkflowStage? PreviousStage { get; set; }
    public SalesWorkflowStage? NewStage { get; set; }

    [MaxLength(2000)]
    public string Details { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class BillingAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BillingPlanId { get; set; }
    public BillingInvoicePlan? BillingPlan { get; set; }

    [MaxLength(80)]
    public string EventType { get; set; } = "";

    [MaxLength(64)]
    public string? UserId { get; set; }

    [MaxLength(180)]
    public string UserName { get; set; } = "";

    [MaxLength(1400)]
    public string Details { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class EventEmailTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(80)]
    public string EventKey { get; set; } = "";

    [MaxLength(220)]
    public string SubjectTemplate { get; set; } = "";

    [MaxLength(12000)]
    public string BodyTemplate { get; set; } = "";

    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AutomationReminderLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(80)]
    public string ReminderType { get; set; } = "";

    public DateTime LogDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public class PermissionRoleAction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PermissionRoleId { get; set; }
    public PermissionRole? PermissionRole { get; set; }

    [MaxLength(80)]
    public string ActionKey { get; set; } = "";
}

public static class AppActions
{
    public const string ClientsView = "Clients.View";
    public const string ClientsEdit = "Clients.Edit";
    public const string ProjectsView = "Projects.View";
    public const string ProjectsEdit = "Projects.Edit";
    public const string ProjectsInvestmentsView = "Projects.Investments.View";
    public const string ProjectsInvestmentsEdit = "Projects.Investments.Edit";
    public const string ProjectsResellersView = "Projects.Resellers.View";
    public const string ProjectsResellersEdit = "Projects.Resellers.Edit";
    public const string ProjectsDeliveryView = "Projects.Delivery.View";
    public const string ProjectsDeliveryEdit = "Projects.Delivery.Edit";
    public const string InventoryView = "Inventory.View";
    public const string InventoryManage = "Inventory.Manage";
    public const string InventoryApprove = "Inventory.Approve";
    public const string CarriersView = "Carriers.View";
    public const string CarriersManage = "Carriers.Manage";
    public const string MonitoringView = "Monitoring.View";
    public const string MonitoringManage = "Monitoring.Manage";
    public const string TicketsView = "Tickets.View";
    public const string TicketsManage = "Tickets.Manage";
    public const string TicketsClose = "Tickets.Close";
    public const string SalesViewOwn = "Sales.ViewOwn";
    public const string SalesViewAll = "Sales.ViewAll";
    public const string SalesManage = "Sales.Manage";
    public const string SalesWorkflowMove = "Sales.Workflow.Move";
    public const string SalesWorkflowAssign = "Sales.Workflow.Assign";
    public const string BillingViewOwn = "Billing.ViewOwn";
    public const string BillingViewAll = "Billing.ViewAll";
    public const string BillingManage = "Billing.Manage";
    public const string BillingSend = "Billing.Send";
    public const string SalesQuotesView = "Sales.Quotes.View";
    public const string SalesQuotesManage = "Sales.Quotes.Manage";
    public const string SalesCatalogView = "Sales.Catalog.View";
    public const string SalesCatalogManage = "Sales.Catalog.Manage";
    public const string SalesProspectsView = "Sales.Prospects.View";
    public const string SalesProspectsCreate = "Sales.Prospects.Create";
    public const string SalesProspectsEdit = "Sales.Prospects.Edit";
    public const string SalesProspectsConvert = "Sales.Prospects.Convert";
    public const string TemplatesManage = "Templates.Manage";
    public const string EmployeesOrgChartView = "Employees.OrgChart.View";
    public const string EmployeesOrgChartEdit = "Employees.OrgChart.Edit";
    public const string EmployeesOrgChartExport = "Employees.OrgChart.Export";

    public static readonly string[] AllKnown =
    [
        ClientsView,
        ClientsEdit,
        ProjectsView,
        ProjectsEdit,
        ProjectsInvestmentsView,
        ProjectsInvestmentsEdit,
        ProjectsResellersView,
        ProjectsResellersEdit,
        ProjectsDeliveryView,
        ProjectsDeliveryEdit,
        InventoryView,
        InventoryManage,
        InventoryApprove,
        CarriersView,
        CarriersManage,
        MonitoringView,
        MonitoringManage,
        TicketsView,
        TicketsManage,
        TicketsClose,
        SalesViewOwn,
        SalesViewAll,
        SalesManage,
        SalesWorkflowMove,
        SalesWorkflowAssign,
        BillingViewOwn,
        BillingViewAll,
        BillingManage,
        BillingSend,
        SalesQuotesView,
        SalesQuotesManage,
        SalesCatalogView,
        SalesCatalogManage,
        SalesProspectsView,
        SalesProspectsCreate,
        SalesProspectsEdit,
        SalesProspectsConvert,
        TemplatesManage,
        EmployeesOrgChartView,
        EmployeesOrgChartEdit,
        EmployeesOrgChartExport
    ];

    public static readonly string[] EmployeeDefaults =
    [
        ProjectsView,
        ProjectsInvestmentsView,
        ProjectsResellersView,
        ProjectsDeliveryView,
        CarriersView,
        MonitoringView,
        InventoryView,
        TicketsView,
        SalesViewOwn,
        SalesWorkflowMove,
        BillingViewOwn
        ,EmployeesOrgChartView
    ];

    private static readonly Dictionary<string, string[]> DependencyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [ClientsEdit] = [ClientsView],
        [ProjectsEdit] = [ProjectsView],
        [ProjectsInvestmentsEdit] = [ProjectsInvestmentsView],
        [ProjectsResellersEdit] = [ProjectsResellersView],
        [ProjectsDeliveryEdit] = [ProjectsDeliveryView],
        [SalesManage] = [SalesViewAll],
        [SalesWorkflowMove] = [SalesViewOwn],
        [SalesWorkflowAssign] = [SalesWorkflowMove, SalesViewOwn],
        [SalesQuotesManage] = [SalesQuotesView],
        [SalesCatalogManage] = [SalesCatalogView],
        [SalesProspectsCreate] = [SalesProspectsView],
        [SalesProspectsEdit] = [SalesProspectsView],
        [SalesProspectsConvert] = [SalesProspectsEdit, SalesProspectsView],
        [EmployeesOrgChartEdit] = [EmployeesOrgChartView],
        [EmployeesOrgChartExport] = [EmployeesOrgChartView],
        [BillingManage] = [BillingViewOwn],
        [BillingSend] = [BillingManage, BillingViewOwn],
        [InventoryManage] = [InventoryView],
        [InventoryApprove] = [InventoryManage, InventoryView],
        [CarriersManage] = [CarriersView],
        [MonitoringManage] = [MonitoringView],
        [TicketsManage] = [TicketsView],
        [TicketsClose] = [TicketsManage, TicketsView]
    };

    public static List<string> ApplyDependencies(IEnumerable<string> selected)
    {
        var set = new HashSet<string>(
            selected.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var key in set.ToList())
            {
                if (!DependencyMap.TryGetValue(key, out var requires))
                    continue;

                foreach (var req in requires)
                {
                    if (set.Add(req))
                        changed = true;
                }
            }
        }

        return set.ToList();
    }

    public static string Label(string key) => key switch
    {
        ClientsView => "Clientes: ver",
        ClientsEdit => "Clientes: editar",
        ProjectsView => "Proyectos: ver",
        ProjectsEdit => "Proyectos: editar",
        ProjectsInvestmentsView => "Proyectos/Inversiones: ver",
        ProjectsInvestmentsEdit => "Proyectos/Inversiones: editar",
        ProjectsResellersView => "Proyectos/Reseller: ver",
        ProjectsResellersEdit => "Proyectos/Reseller: editar",
        ProjectsDeliveryView => "Proyectos/Formato de entrega: ver",
        ProjectsDeliveryEdit => "Proyectos/Formato de entrega: editar",
        InventoryView => "Inventario: ver",
        InventoryManage => "Inventario: gestionar",
        InventoryApprove => "Inventario: aprobar",
        CarriersView => "Carriers: ver",
        CarriersManage => "Carriers: gestionar",
        MonitoringView => "Monitoreo: ver",
        MonitoringManage => "Monitoreo: gestionar",
        TicketsView => "Tickets: ver",
        TicketsManage => "Tickets: gestionar",
        TicketsClose => "Tickets: cerrar",
        SalesViewOwn => "Ventas: ver solo lo mio",
        SalesViewAll => "Ventas: ver todo",
        SalesManage => "Ventas: gestion comercial",
        SalesWorkflowMove => "Workflow: mover etapas",
        SalesWorkflowAssign => "Workflow: asignar propietario",
        BillingViewOwn => "Facturacion: ver propia",
        BillingViewAll => "Facturacion: ver todo",
        BillingManage => "Facturacion: crear/editar planes",
        BillingSend => "Facturacion: enviar comprobantes",
        SalesQuotesView => "Ventas/Cotizaciones: ver",
        SalesQuotesManage => "Ventas/Cotizaciones: gestionar",
        SalesCatalogView => "Ventas/Catálogo: ver",
        SalesCatalogManage => "Ventas/Catálogo: gestionar",
        SalesProspectsView => "Ventas/Prospectos: ver",
        SalesProspectsCreate => "Ventas/Prospectos: crear",
        SalesProspectsEdit => "Ventas/Prospectos: editar",
        SalesProspectsConvert => "Ventas/Prospectos: convertir a cliente",
        TemplatesManage => "Plantillas: administrar",
        EmployeesOrgChartView => "Empleados/Organigrama: ver",
        EmployeesOrgChartEdit => "Empleados/Organigrama: editar",
        EmployeesOrgChartExport => "Empleados/Organigrama: exportar",
        _ => key
    };
}
