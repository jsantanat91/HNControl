using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class InventoryModulePage : ContentPage
{
    private readonly ModulesService _modules;

    public InventoryModulePage(ModulesService modules)
    {
        InitializeComponent();
        _modules = modules;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
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
}
