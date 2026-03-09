using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class CarriersModulePage : ContentPage
{
    private readonly ModulesService _modules;
    private readonly IServiceProvider _services;

    public CarriersModulePage(ModulesService modules, IServiceProvider services)
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
            ItemsCollection.ItemsSource = await _modules.GetCarriersAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnOpenClientClicked(object sender, EventArgs e)
    {
        if (sender is not Button b || b.CommandParameter is not Guid clientId) return;
        var page = _services.GetRequiredService<CarrierClientDetailPage>();
        page.SetClientId(clientId);
        await Navigation.PushAsync(page);
    }
}
