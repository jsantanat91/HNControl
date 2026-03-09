using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class MonitoringModulePage : ContentPage
{
    private readonly ModulesService _modules;
    private readonly IServiceProvider _services;

    public MonitoringModulePage(ModulesService modules, IServiceProvider services)
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
            var data = await _modules.GetMonitoringAsync();
            ItemsCollection.ItemsSource = data.Select(x => new MonitorVm
            {
                Id = x.Id,
                Name = x.Name,
                Client = x.Client,
                Status = x.Status,
                Address = x.Address,
                Meta = $"{x.ProbeType} | {x.LastCheckedAt:yyyy-MM-dd HH:mm} | {(x.LastLatencyMs.HasValue ? x.LastLatencyMs + " ms" : "-")}"
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnOpenDetailClicked(object sender, EventArgs e)
    {
        if (sender is not Button b || b.CommandParameter is not Guid id) return;
        var page = _services.GetRequiredService<MonitoringTargetDetailPage>();
        page.SetTargetId(id);
        await Navigation.PushAsync(page);
    }

    private sealed class MonitorVm
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Client { get; set; } = "";
        public string Status { get; set; } = "";
        public string Address { get; set; } = "";
        public string Meta { get; set; } = "";
    }
}
