using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class TicketDetailPage : ContentPage
{
    private readonly TicketsService _tickets;
    private Guid _ticketId;

    public TicketDetailPage(TicketsService tickets)
    {
        InitializeComponent();
        _tickets = tickets;
    }

    public void SetTicket(Guid id) => _ticketId = id;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_ticketId == Guid.Empty) return;
        try
        {
            var d = await _tickets.DetailAsync(_ticketId);
            TicketNumberLabel.Text = d.TicketNumber;
            TitleLabel.Text = d.Title;
            ClientLabel.Text = $"{d.Client} · {d.Contract}";
            MetaLabel.Text = $"{d.Status} | {d.Priority} | SLA: {d.SlaResolutionDueAt:yyyy-MM-dd HH:mm}";
            DescriptionLabel.Text = d.Description;
            ResolveEntry.Text = d.ResolutionSummary;
            EventsCollection.ItemsSource = d.Events.Select(x => new EventVm
            {
                Top = $"{x.CreatedAt:yyyy-MM-dd HH:mm} | {x.EventType} | {x.UserName}",
                Message = x.Message
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ticket", ex.Message, "OK");
        }
    }

    private async Task Run(Func<Task> action)
    {
        try
        {
            await action();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ticket", ex.Message, "OK");
        }
    }

    private async void OnTakeClicked(object sender, EventArgs e) => await Run(async () =>
    {
        var r = await _tickets.TakeAsync(_ticketId);
        await DisplayAlertAsync("Ticket", r.Message, "OK");
    });

    private async void OnStartClicked(object sender, EventArgs e) => await Run(async () =>
    {
        var r = await _tickets.StartAsync(_ticketId);
        await DisplayAlertAsync("Ticket", r.Message, "OK");
    });

    private async void OnResolveClicked(object sender, EventArgs e) => await Run(async () =>
    {
        var r = await _tickets.ResolveAsync(_ticketId, ResolveEntry.Text ?? "");
        await DisplayAlertAsync("Ticket", r.Message, "OK");
    });

    private async void OnCloseClicked(object sender, EventArgs e) => await Run(async () =>
    {
        var r = await _tickets.CloseAsync(_ticketId);
        await DisplayAlertAsync("Ticket", r.Message, "OK");
    });

    private sealed class EventVm
    {
        public string Top { get; set; } = "";
        public string Message { get; set; } = "";
    }
}

