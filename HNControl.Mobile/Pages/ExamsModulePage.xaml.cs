using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class ExamsModulePage : ContentPage
{
    private readonly ModulesService _modules;
    private readonly IServiceProvider _services;

    public ExamsModulePage(ModulesService modules, IServiceProvider services)
    {
        InitializeComponent();
        _modules = modules;
        _services = services;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var data = await _modules.GetExamsAsync();
            ItemsCollection.ItemsSource = data.Select(x => new
            {
                x.AssignmentId,
                x.Title,
                Status = NormalizeStatus(x.Status),
                StatusBg = StatusBg(x.Status),
                ScoreText = x.MaxScore > 0 ? $"Calificacion: {x.Score:N2}/{x.MaxScore:N2}" : "Sin calificar",
                Meta = $"Asignado: {x.AssignedAt:yyyy-MM-dd}" + (x.DueAt.HasValue ? $" | Limite: {x.DueAt:yyyy-MM-dd}" : ""),
                CanResolve = CanResolve(x.Status)
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnResolveClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not Guid assignmentId) return;
        var page = _services.GetRequiredService<ExamTakePage>();
        page.SetAssignment(assignmentId);
        await Navigation.PushAsync(page);
    }

    private static bool CanResolve(string raw)
        => !(raw.Equals("Submitted", StringComparison.OrdinalIgnoreCase)
             || raw.Equals("Graded", StringComparison.OrdinalIgnoreCase)
             || raw.Equals("Enviado", StringComparison.OrdinalIgnoreCase)
             || raw.Equals("Calificado", StringComparison.OrdinalIgnoreCase));

    private static string NormalizeStatus(string raw)
        => raw switch
        {
            "Assigned" => "Asignado",
            "InProgress" => "En progreso",
            "Submitted" => "Enviado",
            "Graded" => "Calificado",
            _ => raw
        };

    private static string StatusBg(string raw)
        => raw switch
        {
            "Assigned" => "#E6EEFF",
            "InProgress" => "#FFF0D5",
            "Submitted" => "#D7F2FF",
            "Graded" => "#DDF8E8",
            _ => "#EEF2F7"
        };
}
