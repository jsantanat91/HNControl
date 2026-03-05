using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class ExamsModulePage : ContentPage
{
    private readonly ModulesService _modules;

    public ExamsModulePage(ModulesService modules)
    {
        InitializeComponent();
        _modules = modules;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var data = await _modules.GetExamsAsync();
            ItemsCollection.ItemsSource = data.Select(x => new
            {
                x.Title,
                x.Status,
                ScoreText = x.MaxScore > 0 ? $"Calificacion: {x.Score:N2}/{x.MaxScore:N2}" : "Sin calificar",
                Meta = $"Asignado: {x.AssignedAt:yyyy-MM-dd}" + (x.DueAt.HasValue ? $" | Limite: {x.DueAt:yyyy-MM-dd}" : "")
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
