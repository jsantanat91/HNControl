using HNControl.Mobile.Models;
using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class OrderDetailPage : ContentPage
{
    private readonly OrdersService _orders;
    private Guid _orderId;

    public OrderDetailPage(OrdersService orders)
    {
        InitializeComponent();
        _orders = orders;
    }

    public void SetOrderId(Guid orderId)
    {
        _orderId = orderId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_orderId == Guid.Empty)
        {
            return;
        }

        try
        {
            var detail = await _orders.DetailAsync(_orderId);
            Bind(detail);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private void Bind(ServiceOrderDetailDto d)
    {
        TitleLabel.Text = d.Title;
        ClientLabel.Text = d.Client;
        TypeLabel.Text = MapType(d.Type);
        StatusLabel.Text = MapStatus(d.Status);
        AreaLabel.Text = MapArea(d.CurrentArea);
        ClaimedByLabel.Text = "Tomada por: " + (string.IsNullOrWhiteSpace(d.ClaimedBy) ? "Sin tomar" : d.ClaimedBy);
        CreatedLabel.Text = "Creada: " + d.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        EstimatedLabel.Text = d.EstimatedEndDate.HasValue
            ? "Entrega estimada: " + d.EstimatedEndDate.Value.ToLocalTime().ToString("yyyy-MM-dd")
            : "Entrega estimada: -";
        DescriptionLabel.Text = string.IsNullOrWhiteSpace(d.Description) ? "-" : d.Description;
        LevantamientoLabel.Text = string.IsNullOrWhiteSpace(d.LevantamientoNotes) ? "-" : d.LevantamientoNotes;
        MaterialesLabel.Text = string.IsNullOrWhiteSpace(d.MaterialesNotes) ? "-" : d.MaterialesNotes;
    }

    private static string MapType(int val) => val switch
    {
        1 => "Correctivo",
        2 => "Preventivo",
        3 => "Nueva instalacion",
        4 => "Levantamiento tecnico",
        99 => "Global",
        _ => "Tipo " + val
    };

    private static string MapStatus(int val) => val switch
    {
        1 => "Creada",
        2 => "En proceso",
        3 => "En revision",
        4 => "Finalizada",
        5 => "Pendiente firma cliente",
        6 => "Rechazada",
        _ => "Estatus " + val
    };

    private static string MapArea(int val) => val switch
    {
        1 => "Levantamiento",
        2 => "Materiales",
        3 => "Ejecucion",
        4 => "Cierre tecnico",
        _ => "Area " + val
    };
}
