using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class TicketsModulePage : ContentPage
{
    private readonly TicketsService _tickets;
    private readonly IServiceProvider _services;

    public TicketsModulePage(TicketsService tickets, IServiceProvider services)
    {
        InitializeComponent();
        _tickets = tickets;
        _services = services;
        StatusPicker.SelectedIndex = 0;
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
            var status = GetStatusApiValue(StatusPicker.SelectedItem?.ToString());
            var list = await _tickets.ListAsync(status);
            ItemsCollection.ItemsSource = list.Select(x => new TicketVm
            {
                Id = x.Id,
                TicketNumber = x.TicketNumber,
                Title = x.Title,
                Client = x.Client,
                Status = NormalizeStatus(x.Status),
                Priority = NormalizePriority(x.Priority),
                Meta = $"{x.Source} | {x.CreatedAt:yyyy-MM-dd HH:mm}",
                IsBreach = x.Breach,
                BreachText = x.Breach ? "SLA vencido" : "",
                CanTake = x.CanTake
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Tickets", ex.Message, "OK");
        }
    }

    private async void OnFilterChanged(object sender, EventArgs e) => await LoadAsync();
    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadAsync();

    private async void OnTakeClicked(object sender, EventArgs e)
    {
        if (sender is not Button b || b.CommandParameter is not Guid id) return;
        try
        {
            var res = await _tickets.TakeAsync(id);
            await DisplayAlertAsync("Ticket", res.Message, "OK");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ticket", ex.Message, "OK");
        }
    }

    private async void OnOpenClicked(object sender, EventArgs e)
    {
        if (sender is not Button b || b.CommandParameter is not Guid id) return;
        var page = _services.GetRequiredService<TicketDetailPage>();
        page.SetTicket(id);
        await Navigation.PushAsync(page);
    }

    private static string GetStatusApiValue(string? selected)
        => selected switch
        {
            "Míos" => "mine",
            "Cerrados" => "closed",
            _ => "open"
        };

    private static string NormalizePriority(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        return value.Equals("Urge", StringComparison.OrdinalIgnoreCase)
            ? "Urgente"
            : value;
    }

    private static string NormalizeStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        return value switch
        {
            "New" => "Nuevo",
            "Assigned" => "Asignado",
            "InProgress" => "En proceso",
            "PendingCustomer" => "Pendiente cliente",
            "Resolved" => "Resuelto",
            "Closed" => "Cerrado",
            "Cancelled" => "Cancelado",
            _ => value
        };
    }

    private sealed class TicketVm
    {
        public Guid Id { get; set; }
        public string TicketNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public string Client { get; set; } = "";
        public string Status { get; set; } = "";
        public string Priority { get; set; } = "";
        public string Meta { get; set; } = "";
        public bool IsBreach { get; set; }
        public string BreachText { get; set; } = "";
        public bool CanTake { get; set; }
    }
}
