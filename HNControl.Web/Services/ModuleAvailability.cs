using HNControl.Web.Models;

namespace HNControl.Web.Services;

/// <summary>Deployment switches: retain code/data while suspending complete modules.</summary>
public sealed class ModuleAvailability(IConfiguration configuration)
{
    public bool DocumentsEnabled => configuration.GetValue<bool>("Features:DocumentsEnabled");
    public bool MonitoringEnabled => configuration.GetValue<bool>("Features:MonitoringEnabled");
    public bool ExamsEnabled => configuration.GetValue<bool>("Features:ExamsEnabled");
    public bool SalesCallsEnabled => configuration.GetValue<bool>("Features:SalesCallsEnabled");

    public bool IsEnabled(string module) => (module ?? "").ToLowerInvariant() switch
    {
        "knowledge" => DocumentsEnabled,
        "monitoring" => MonitoringEnabled,
        "exams" => ExamsEnabled,
        "salescalls" => SalesCallsEnabled,
        _ => true
    };

    public bool IsActionEnabled(string action) => action switch
    {
        AppActions.SalesCallsView or AppActions.SalesCallsUse => SalesCallsEnabled,
        _ => true
    };

    public bool IsPathEnabled(string? value)
    {
        var path = new PathString((value ?? "").Split('?', '#')[0].TrimEnd('/'));
        bool Under(string prefix) => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase);
        if (Under("/Knowledge") || Under("/Admin/Knowledge") || Under("/api/mobile/modules/knowledge")) return DocumentsEnabled;
        if (Under("/Monitoring") || Under("/Admin/Monitoring") || Under("/api/mobile/modules/monitoring")) return MonitoringEnabled;
        if (Under("/Exams") || Under("/Admin/Exams") || Under("/api/mobile/modules/exams")) return ExamsEnabled;
        if (Under("/Sales/Calls") || Under("/Ventas/Llamadas")) return SalesCallsEnabled;
        return true;
    }
}
