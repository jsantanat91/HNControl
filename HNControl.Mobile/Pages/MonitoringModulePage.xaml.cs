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
            ItemsCollection.ItemsSource = data.Select(x =>
            {
                var up = string.Equals(x.Status, "Up", StringComparison.OrdinalIgnoreCase);
                var down = string.Equals(x.Status, "Down", StringComparison.OrdinalIgnoreCase);

                return new MonitorVm
                {
                    Id = x.Id,
                    Name = x.Name,
                    Client = x.Client,
                    StatusText = up ? "Arriba" : down ? "Abajo" : x.Status,
                    Address = x.Address,
                    Meta = $"{x.ProbeType} | {x.LastCheckedAt:yyyy-MM-dd HH:mm} | {(x.LastLatencyMs.HasValue ? x.LastLatencyMs + " ms" : "-")}",
                    StatusBg = up ? Color.FromArgb("#DCFCE7") : down ? Color.FromArgb("#FEE2E2") : Color.FromArgb("#E2E8F0"),
                    StatusStroke = up ? Color.FromArgb("#86EFAC") : down ? Color.FromArgb("#FCA5A5") : Color.FromArgb("#CBD5E1"),
                    StatusTextColor = up ? Color.FromArgb("#166534") : down ? Color.FromArgb("#B91C1C") : Color.FromArgb("#334155")
                };
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
        public string StatusText { get; set; } = "";
        public string Address { get; set; } = "";
        public string Meta { get; set; } = "";
        public Color StatusBg { get; set; } = Color.FromArgb("#E2E8F0");
        public Color StatusStroke { get; set; } = Color.FromArgb("#CBD5E1");
        public Color StatusTextColor { get; set; } = Color.FromArgb("#334155");
    }
}
