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
            ItemsCollection.ItemsSource = list.Select(x =>
            {
                var statusText = NormalizeStatus(x.Status);
                var priorityText = NormalizePriority(x.Priority);
                var statusColor = GetStatusColor(statusText);
                var priorityColor = GetPriorityColor(priorityText);

                return new TicketVm
                {
                    Id = x.Id,
                    TicketNumber = x.TicketNumber,
                    Title = x.Title,
                    Client = x.Client,
                    Status = statusText,
                    Priority = priorityText,
                    Meta = $"{x.Source} | {x.CreatedAt:yyyy-MM-dd HH:mm}",
                    IsBreach = x.Breach,
                    BreachText = x.Breach ? "SLA vencido" : "",
                    CanTake = x.CanTake,
                    StatusBg = statusColor.bg,
                    StatusStroke = statusColor.stroke,
                    StatusColor = statusColor.text,
                    PriorityBg = priorityColor.bg,
                    PriorityStroke = priorityColor.stroke,
                    PriorityColor = priorityColor.text
                };
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
            "Mios" => "mine",
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

    private static (Color bg, Color stroke, Color text) GetStatusColor(string value)
        => value switch
        {
            "Cerrado" or "Resuelto" => (Color.FromArgb("#DCFCE7"), Color.FromArgb("#86EFAC"), Color.FromArgb("#166534")),
            "Cancelado" => (Color.FromArgb("#FEE2E2"), Color.FromArgb("#FCA5A5"), Color.FromArgb("#991B1B")),
            "Asignado" or "En proceso" => (Color.FromArgb("#DBEAFE"), Color.FromArgb("#93C5FD"), Color.FromArgb("#1D4ED8")),
            _ => (Color.FromArgb("#E2E8F0"), Color.FromArgb("#CBD5E1"), Color.FromArgb("#334155"))
        };

    private static (Color bg, Color stroke, Color text) GetPriorityColor(string value)
        => value switch
        {
            "Urgente" or "Alta" => (Color.FromArgb("#FEE2E2"), Color.FromArgb("#FCA5A5"), Color.FromArgb("#B91C1C")),
            "Intermedia" or "Media" => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#FCD34D"), Color.FromArgb("#92400E")),
            _ => (Color.FromArgb("#DCFCE7"), Color.FromArgb("#86EFAC"), Color.FromArgb("#166534"))
        };

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

        public Color StatusBg { get; set; } = Color.FromArgb("#E2E8F0");
        public Color StatusStroke { get; set; } = Color.FromArgb("#CBD5E1");
        public Color StatusColor { get; set; } = Color.FromArgb("#334155");

        public Color PriorityBg { get; set; } = Color.FromArgb("#E2E8F0");
        public Color PriorityStroke { get; set; } = Color.FromArgb("#CBD5E1");
        public Color PriorityColor { get; set; } = Color.FromArgb("#334155");
    }
}
