using HNControl.Mobile.Models;
using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class OrdersPage : ContentPage
{
    private readonly OrdersService _orders;
    private readonly AuthService _auth;
    private readonly IServiceProvider _services;
    private bool _isBusy;

    public OrdersPage(OrdersService orders, AuthService auth, IServiceProvider services)
    {
        InitializeComponent();
        _orders = orders;
        _auth = auth;
        _services = services;

        ToolbarItems.Add(new ToolbarItem("Salir", null, () =>
        {
            _auth.Logout();
            App.SwitchToLogin();
        }));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (OrdersCollection.ItemsSource is null)
        {
            await LoadOrdersAsync();
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadOrdersAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadOrdersAsync();
        OrdersRefreshView.IsRefreshing = false;
    }

    private async void OnDetailClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not Guid id)
        {
            return;
        }

        var detailPage = _services.GetRequiredService<OrderDetailPage>();
        detailPage.SetOrderId(id);
        await Navigation.PushAsync(detailPage);
    }

    private async void OnTakeClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not Guid id)
        {
            return;
        }

        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            await _orders.TakeAsync(id);
            await DisplayAlertAsync("Orden", "Orden tomada correctamente.", "OK");
            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task LoadOrdersAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            var data = await _orders.ListAsync(100);
            OrdersCollection.ItemsSource = data;
            Title = $"Ordenes ({data.Count})";
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Sesion expirada", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("No autorizado", StringComparison.OrdinalIgnoreCase))
            {
                _auth.Logout();
                App.SwitchToLogin();
                return;
            }

            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _isBusy = false;
        }
    }
}
