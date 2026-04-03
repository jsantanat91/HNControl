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

    [MaxLength(1400)]
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
    public const string SalesViewOwn = "Sales.ViewOwn";
    public const string SalesViewAll = "Sales.ViewAll";
    public const string SalesManage = "Sales.Manage";
    public const string SalesWorkflowMove = "Sales.Workflow.Move";
    public const string SalesWorkflowAssign = "Sales.Workflow.Assign";
    public const string BillingViewOwn = "Billing.ViewOwn";
    public const string BillingViewAll = "Billing.ViewAll";
    public const string BillingManage = "Billing.Manage";
    public const string BillingSend = "Billing.Send";
    public const string TemplatesManage = "Templates.Manage";

    public static readonly string[] AllKnown =
    [
        SalesViewOwn,
        SalesViewAll,
        SalesManage,
        SalesWorkflowMove,
        SalesWorkflowAssign,
        BillingViewOwn,
        BillingViewAll,
        BillingManage,
        BillingSend,
        TemplatesManage
    ];

    public static readonly string[] EmployeeDefaults =
    [
        SalesViewOwn,
        SalesWorkflowMove,
        BillingViewOwn
    ];

    public static string Label(string key) => key switch
    {
        SalesViewOwn => "Ventas: ver solo lo mio",
        SalesViewAll => "Ventas: ver todo",
        SalesManage => "Ventas: gestion comercial",
        SalesWorkflowMove => "Workflow: mover etapas",
        SalesWorkflowAssign => "Workflow: asignar propietario",
        BillingViewOwn => "Facturacion: ver propia",
        BillingViewAll => "Facturacion: ver todo",
        BillingManage => "Facturacion: crear/editar planes",
        BillingSend => "Facturacion: enviar comprobantes",
        TemplatesManage => "Plantillas: administrar",
        _ => key
    };
}
