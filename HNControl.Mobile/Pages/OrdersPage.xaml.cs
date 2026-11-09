using HNControl.Mobile.Models;
using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class OrdersPage : ContentPage
{
    private readonly OrdersService _orders;
    private readonly AuthService _auth;
    private readonly IServiceProvider _services;
    private bool _isBusy;
    private List<ServiceOrderListItemDto> _items = new();
    private string _filter = "all";

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void OnFilterClicked(object sender, EventArgs e)
    {
        if (sender is Button button) _filter = button.CommandParameter as string ?? "all";
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (ResultsLabel == null) return;
        var result = OrderPresentation.Filter(_items, _filter, SearchEntry.Text);
        OrdersCollection.ItemsSource = result;
        ResultsLabel.Text = $"{result.Count} de {_items.Count} órdenes cargadas" + (_items.Count == 300 ? " · límite de consulta" : "");
        foreach (var button in new[] { AllFilter, MineFilter, AvailableFilter, ClosedFilter })
        {
            var active = (string?)button.CommandParameter == _filter;
            button.BackgroundColor = Color.FromArgb(active ? "#087EE8" : "#EAF4FF");
            button.TextColor = active ? Colors.White : Color.FromArgb("#55708E");
        }
    }

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
        await LoadOrdersAsync();
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
            await FetchOrdersAsync();
        }
        catch (Exception ex)
        {
            ResultsLabel.Text = "No se pudo actualizar. Intenta de nuevo.";
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async void OnPdfClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not Guid id)
            return;

        try
        {
            var bytes = await _orders.GetPdfAsync(id);
            var fileName = $"OrdenServicio_{id:N}.pdf";
            var path = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(path, bytes);
            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                File = new ReadOnlyFile(path)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("PDF", ex.Message, "OK");
        }
    }

    private async Task FetchOrdersAsync()
    {
        _items = await _orders.ListAsync(300);
        ActiveCount.Text = _items.Count(x => x.Status != 4).ToString();
        MineCount.Text = _items.Count(x => x.IsMine && x.Status != 4).ToString();
        AvailableCount.Text = _items.Count(x => x.CanTake).ToString();
        ApplyFilters();
    }

    private async Task LoadOrdersAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        ResultsLabel.Text = "Actualizando órdenes…";
        try
        {
            await FetchOrdersAsync();
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

            ResultsLabel.Text = "No se pudo actualizar. Intenta de nuevo.";
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _isBusy = false;
        }
    }
}
