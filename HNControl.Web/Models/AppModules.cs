namespace HNControl.Web.Models;

/// <summary>
/// Claves de módulos (para permisos). Mantén estas keys estables.
/// </summary>
public static class AppModules
{
    // Employee
    public const string ServiceOrders = "ServiceOrders";
    public const string Viaticos = "Viaticos";
    public const string Eval360 = "Eval360";
    public const string Projects = "Projects";
    public const string Knowledge = "Knowledge";
    public const string Carriers = "Carriers";
    public const string Inventory = "Inventory";
    public const string Monitoring = "Monitoring";
    public const string Tickets = "Tickets";
    public const string Leaves = "Leaves";   // Vacaciones e incidencias
    public const string Exams = "Exams";     // Exámenes

    // Admin (solo para UI de permisos; admin bypass en runtime)
    public const string Clients = "Clients";
    public const string Performance = "Performance";
    public const string Security = "Security";

    public static readonly string[] EmployeeDefaults =
    [
        ServiceOrders,
        Viaticos,
        Eval360,
        Projects,
        Knowledge,
        Carriers,
        Inventory,
        Monitoring,
        Tickets,
        Leaves,
        Exams
    ];

    public static readonly string[] AllKnown =
    [
        ServiceOrders,
        Viaticos,
        Eval360,
        Projects,
        Knowledge,
        Carriers,
        Inventory,
        Monitoring,
        Tickets,
        Leaves,
        Exams,
        Clients,
        Performance,
        Security
    ];

    public static string Label(string key) => key switch
    {
        ServiceOrders => "Órdenes",
        Viaticos => "Viáticos",
        Eval360 => "Evaluación 360",
        Projects => "Proyectos",
        Knowledge => "Documentos",
        Carriers => "Carriers (Internet)",
        Inventory => "Inventarios",
        Monitoring => "Monitoreo",
        Tickets => "Tickets",
        Leaves => "Vacaciones e incidencias",
        Exams => "Exámenes",
        Clients => "Clientes",
        Performance => "KPI / Nómina",
        Security => "Seguridad / Permisos",
        _ => key
    };

    /// <summary>
    /// Mapea el ViewEnginePath de Razor Pages (ej: "/Projects/Index") a una clave de módulo.
    /// Retorna null si no aplica (Account, Public, Home, etc.).
    /// </summary>
    public static string? FromPagePath(string? viewEnginePath)
    {
        if (string.IsNullOrWhiteSpace(viewEnginePath)) return null;

        // Carpetas que no deberían bloquearse por módulos.
        if (viewEnginePath.StartsWith("/Account", StringComparison.OrdinalIgnoreCase)) return null;
        if (viewEnginePath.StartsWith("/Public", StringComparison.OrdinalIgnoreCase)) return null;
        if (string.Equals(viewEnginePath, "/Index", StringComparison.OrdinalIgnoreCase)) return null;
        if (string.Equals(viewEnginePath, "/Error", StringComparison.OrdinalIgnoreCase)) return null;
        if (viewEnginePath.StartsWith("/Employees", StringComparison.OrdinalIgnoreCase)) return null;

        if (viewEnginePath.StartsWith("/ServiceOrders", StringComparison.OrdinalIgnoreCase)) return ServiceOrders;
        if (viewEnginePath.StartsWith("/Viaticos", StringComparison.OrdinalIgnoreCase)) return Viaticos;
        if (viewEnginePath.StartsWith("/Eval360", StringComparison.OrdinalIgnoreCase)) return Eval360;
        if (viewEnginePath.StartsWith("/Projects", StringComparison.OrdinalIgnoreCase)) return Projects;
        if (viewEnginePath.StartsWith("/Knowledge", StringComparison.OrdinalIgnoreCase)) return Knowledge;
        if (viewEnginePath.StartsWith("/Carriers", StringComparison.OrdinalIgnoreCase)) return Carriers;
        if (viewEnginePath.StartsWith("/Inventory", StringComparison.OrdinalIgnoreCase)) return Inventory;
        if (viewEnginePath.StartsWith("/Monitoring", StringComparison.OrdinalIgnoreCase)) return Monitoring;
        if (viewEnginePath.StartsWith("/Tickets", StringComparison.OrdinalIgnoreCase)) return Tickets;
        if (viewEnginePath.StartsWith("/Leaves", StringComparison.OrdinalIgnoreCase)) return Leaves;
        if (viewEnginePath.StartsWith("/Exams", StringComparison.OrdinalIgnoreCase)) return Exams;

        if (viewEnginePath.StartsWith("/Clients", StringComparison.OrdinalIgnoreCase)) return Clients;
        if (viewEnginePath.StartsWith("/Performance", StringComparison.OrdinalIgnoreCase)) return Performance;
        if (viewEnginePath.StartsWith("/Admin/Security", StringComparison.OrdinalIgnoreCase)) return Security;

        return null;
    }

    /// <summary>
    /// Lista para UI de roles/permisos.
    /// </summary>
    public static readonly IReadOnlyList<(string Key, string Label)> All =
        AllKnown.Select(k => (k, Label(k))).ToList();
}
