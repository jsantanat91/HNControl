using HNControl.Mobile.Models;
using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class ServiceInventoryPage : ContentPage
{
    private readonly MobileApiClient _api;
    private bool _loading, _settingFilters;
    private int _page = 1, _total;
    public ServiceInventoryPage(MobileApiClient api) { InitializeComponent(); _api = api; }
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true; ErrorLabel.IsVisible = false;
        CountLabel.Text = "Consultando servicios…";
        ClientPicker.IsEnabled = CarrierPicker.IsEnabled = false;
        PreviousButton.IsEnabled = NextButton.IsEnabled = false;
        try
        {
            var client = (ClientPicker.SelectedItem as InventoryFilterDto)?.Id ?? "";
            var carrier = (CarrierPicker.SelectedItem as InventoryFilterDto)?.Id ?? "";
            var data = await _api.GetJsonAsync<ServiceInventoryResponse>($"/api/mobile/service-inventory?page={_page}&carrier={Uri.EscapeDataString(carrier)}{(client.Length > 0 ? "&clientId=" + Uri.EscapeDataString(client) : "")}");
            _total = data.Total; _page = data.Page;
            _settingFilters = true;
            data.Clients.Insert(0, new() { Label = "Todos" });
            data.Carriers.Insert(0, new() { Label = "Todos" });
            ClientPicker.ItemsSource = data.Clients; CarrierPicker.ItemsSource = data.Carriers;
            ClientPicker.SelectedItem = data.Clients.FirstOrDefault(x => x.Id == client) ?? data.Clients[0];
            CarrierPicker.SelectedItem = data.Carriers.FirstOrDefault(x => x.Id == carrier) ?? data.Carriers[0];
            ItemsView.ItemsSource = data.Items;
            CountLabel.Text = _total == 1 ? "1 servicio" : $"{_total} servicios";
            EmptyLabel.Text = "No hay servicios para estos filtros.";
            PageLabel.Text = $"{_page} / {Math.Max(1, (_total + 49) / 50)}";
        }
        catch (Exception ex) { ItemsView.ItemsSource = null; EmptyLabel.Text = "Desliza hacia abajo para reintentar."; CountLabel.Text = "Consulta no disponible"; ErrorLabel.Text = ex.Message; ErrorLabel.IsVisible = true; }
        finally
        {
            _settingFilters = _loading = false;
            ServicesRefresh.IsRefreshing = false;
            ClientPicker.IsEnabled = CarrierPicker.IsEnabled = true;
            PreviousButton.IsEnabled = _page > 1; NextButton.IsEnabled = _page * 50 < _total;
        }
    }
    private async void OnFilterChanged(object? sender, EventArgs e) { if (_settingFilters || _loading) return; _page = 1; await LoadAsync(); }
    private async void OnRefreshing(object? sender, EventArgs e)
    {
        if (_loading) { ServicesRefresh.IsRefreshing = false; return; }
        await LoadAsync();
    }
    private async void OnPrevious(object? sender, EventArgs e) { if (_loading || _page <= 1) return; _page--; await LoadAsync(); }
    private async void OnNext(object? sender, EventArgs e) { if (_loading || _page * 50 >= _total) return; _page++; await LoadAsync(); }
    private async void OnSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ServiceInventoryRow row) return;
        ItemsView.SelectedItem = null;
        await Navigation.PushAsync(new ServiceInventoryDetailPage(row, _api));
    }
    private async void OnAccounts(object? sender, EventArgs e) => await Navigation.PushAsync(new ServiceAccountsPage(_api));
}
