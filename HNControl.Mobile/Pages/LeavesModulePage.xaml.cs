using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class LeavesModulePage : ContentPage
{
    private readonly ModulesService _modules;

    public LeavesModulePage(ModulesService modules)
    {
        InitializeComponent();
        _modules = modules;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var data = await _modules.GetLeavesAsync();
            ItemsCollection.ItemsSource = data.Select(x => new
            {
                x.Type,
                x.Status,
                Range = $"{x.StartDate:yyyy-MM-dd} a {x.EndDate:yyyy-MM-dd}",
                Meta = $"Dias: {x.TotalDays} | Solicitado: {x.RequestedAt:yyyy-MM-dd HH:mm}"
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
