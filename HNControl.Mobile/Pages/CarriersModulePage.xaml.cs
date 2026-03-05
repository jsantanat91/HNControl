using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class CarriersModulePage : ContentPage
{
    private readonly ModulesService _modules;

    public CarriersModulePage(ModulesService modules)
    {
        InitializeComponent();
        _modules = modules;
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
}
