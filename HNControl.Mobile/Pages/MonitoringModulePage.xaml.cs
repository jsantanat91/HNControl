using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class MonitoringModulePage : ContentPage
{
    private readonly ModulesService _modules;

    public MonitoringModulePage(ModulesService modules)
    {
        InitializeComponent();
        _modules = modules;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var data = await _modules.GetMonitoringAsync();
            ItemsCollection.ItemsSource = data.Select(x => new
            {
                x.Name,
                x.Client,
                x.Status,
                x.Address,
                Meta = $"{x.ProbeType} | {x.LastCheckedAt:yyyy-MM-dd HH:mm} | {(x.LastLatencyMs.HasValue ? x.LastLatencyMs + " ms" : "-")}"
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
