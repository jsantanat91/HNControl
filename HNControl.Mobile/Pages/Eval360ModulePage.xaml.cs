using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class Eval360ModulePage : ContentPage
{
    private readonly ModulesService _modules;
    private readonly IServiceProvider _services;

    public Eval360ModulePage(ModulesService modules, IServiceProvider services)
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
            var data = await _modules.GetEval360Async();
            ItemsCollection.ItemsSource = data.Select(x => new
            {
                x.AssignmentId,
                x.Campaign,
                x.Role,
                x.Status,
                StatusBg = string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Status, "Pendiente", StringComparison.OrdinalIgnoreCase)
                    ? Color.FromArgb("#FFF4D6")
                    : string.Equals(x.Status, "Submitted", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(x.Status, "Enviado", StringComparison.OrdinalIgnoreCase)
                        ? Color.FromArgb("#DDF8E8")
                        : Color.FromArgb("#EEF2FF"),
                CanAnswer = (string.Equals(x.Role, "Evaluador", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(x.Role, "Evaluator", StringComparison.OrdinalIgnoreCase))
                            && (string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(x.Status, "Pendiente", StringComparison.OrdinalIgnoreCase)),
                Meta = $"Creada: {x.CreatedAt:yyyy-MM-dd}" + (x.SubmittedAt.HasValue ? $" | Enviada: {x.SubmittedAt:yyyy-MM-dd}" : "")
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnAnswerClicked(object sender, EventArgs e)
    {
        if (sender is not Button b || b.CommandParameter is not Guid assignmentId) return;
        var page = _services.GetRequiredService<Eval360TakePage>();
        page.SetAssignment(assignmentId);
        await Navigation.PushAsync(page);
    }
}
