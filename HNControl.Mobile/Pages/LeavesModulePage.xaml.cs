using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class LeavesModulePage : ContentPage
{
    private readonly ModulesService _modules;
    private readonly IServiceProvider _services;

    public LeavesModulePage(ModulesService modules, IServiceProvider services)
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
            var data = await _modules.GetLeavesAsync();
            ItemsCollection.ItemsSource = data.Select(x => new LeaveVm
            {
                Id = x.Id,
                Type = x.Type,
                Status = x.Status,
                Range = $"{x.StartDate:yyyy-MM-dd} a {x.EndDate:yyyy-MM-dd}",
                Meta = $"Días: {x.TotalDays} | Solicitado: {x.RequestedAt:yyyy-MM-dd HH:mm}"
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
        var page = _services.GetRequiredService<LeaveDetailPage>();
        page.SetLeaveId(id);
        await Navigation.PushAsync(page);
    }

    private sealed class LeaveVm
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = "";
        public string Status { get; set; } = "";
        public string Range { get; set; } = "";
        public string Meta { get; set; } = "";
    }
}
