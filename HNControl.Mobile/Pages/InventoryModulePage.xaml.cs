using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class InventoryModulePage : ContentPage
{
    private readonly ModulesService _modules;
    private readonly IServiceProvider _services;

    public InventoryModulePage(ModulesService modules, IServiceProvider services)
    {
        InitializeComponent();
        _modules = modules;
        _services = services;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var data = await _modules.GetInventoryRequestsAsync();
            ItemsCollection.ItemsSource = data.Select(x => new
            {
                x.ItemsPreview,
                x.StatusLabel,
                Meta = $"{x.RequestedAt:yyyy-MM-dd HH:mm} | {x.Type} | Lineas: {x.LinesCount} | Resp: {x.ResponsibleName}"
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnRequestInClicked(object sender, EventArgs e)
    {
        var page = _services.GetRequiredService<InventoryRequestPage>();
        page.SetMode(isInMode: true);
        await Navigation.PushAsync(page);
    }

    private async void OnRequestOutClicked(object sender, EventArgs e)
    {
        var page = _services.GetRequiredService<InventoryRequestPage>();
        page.SetMode(isInMode: false);
        await Navigation.PushAsync(page);
    }
}
