using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class ProjectsModulePage : ContentPage
{
    private readonly ModulesService _modules;

    public ProjectsModulePage(ModulesService modules)
    {
        InitializeComponent();
        _modules = modules;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var data = await _modules.GetProjectsAsync();
            ItemsCollection.ItemsSource = data.Select(x => new
            {
                x.Title,
                x.Client,
                x.Status,
                Meta = $"Inicio: {x.StartDate:yyyy-MM-dd} | Estimado: {x.EstimatedEndDate:yyyy-MM-dd}"
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
