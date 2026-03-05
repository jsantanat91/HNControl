using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class Eval360ModulePage : ContentPage
{
    private readonly ModulesService _modules;

    public Eval360ModulePage(ModulesService modules)
    {
        InitializeComponent();
        _modules = modules;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var data = await _modules.GetEval360Async();
            ItemsCollection.ItemsSource = data.Select(x => new
            {
                x.Campaign,
                x.Role,
                x.Status,
                Meta = $"Creada: {x.CreatedAt:yyyy-MM-dd}" + (x.SubmittedAt.HasValue ? $" | Enviada: {x.SubmittedAt:yyyy-MM-dd}" : "")
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
