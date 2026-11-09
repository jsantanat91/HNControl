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
        BusyIndicator.IsVisible = BusyIndicator.IsRunning = true;
        ClientPicker.IsEnabled = CarrierPicker.IsEnabled = false;
        PreviousButton.IsEnabled = NextButton.IsEnabled = false;
        try
        {
            var client = (ClientPicker.SelectedItem as InventoryFilterDto)?.Id ?? "";
            var carrier = (CarrierPicker.SelectedItem as InventoryFilterDto)?.Id ?? "";
            var data = await _api.GetJsonAsync<ServiceInventoryResponse>($"/api/mobile/service-inventory?page={_page}&carrier={Uri.EscapeDataString(carrier)}{(client.Length > 0 ? "&clientId=" + Uri.EscapeDataString(client) : "")}");
            _total = data.Total; _page = data.Page;
            _settingFilters = true;
            data.Clients.Insert(0, new() { Label = "Todos los clientes" });
            data.Carriers.Insert(0, new() { Label = "Todos los proveedores" });
            ClientPicker.ItemsSource = data.Clients; CarrierPicker.ItemsSource = data.Carriers;
            ClientPicker.SelectedItem = data.Clients.FirstOrDefault(x => x.Id == client) ?? data.Clients[0];
            CarrierPicker.SelectedItem = data.Carriers.FirstOrDefault(x => x.Id == carrier) ?? data.Carriers[0];
            ItemsView.ItemsSource = data.Items;
            CountLabel.Text = $"{_total} servicio(s) · Consulta de solo lectura";
            PageLabel.Text = $"{_page} / {Math.Max(1, (_total + 49) / 50)}";
        }
        catch (Exception ex) { ItemsView.ItemsSource = null; CountLabel.Text = "Consulta no disponible"; ErrorLabel.Text = ex.Message; ErrorLabel.IsVisible = true; }
        finally
        {
            _settingFilters = _loading = false;
            BusyIndicator.IsVisible = BusyIndicator.IsRunning = false;
            ClientPicker.IsEnabled = CarrierPicker.IsEnabled = true;
            PreviousButton.IsEnabled = _page > 1; NextButton.IsEnabled = _page * 50 < _total;
        }
    }
    private async void OnFilterChanged(object? sender, EventArgs e) { if (_settingFilters || _loading) return; _page = 1; await LoadAsync(); }
    private async void OnRefresh(object? sender, EventArgs e) { if (_loading) return; _page = 1; await LoadAsync(); }
    private async void OnPrevious(object? sender, EventArgs e) { if (_loading || _page <= 1) return; _page--; await LoadAsync(); }
    private async void OnNext(object? sender, EventArgs e) { if (_loading || _page * 50 >= _total) return; _page++; await LoadAsync(); }
    private async void OnSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ServiceInventoryRow row) return;
        ItemsView.SelectedItem = null;
        var fields = new VerticalStackLayout { Spacing = 16, Padding = 22 };
        fields.Add(new Label { Text = row.Name, FontSize = 26, FontAttributes = FontAttributes.Bold });
        void Field(string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            fields.Add(new VerticalStackLayout { Spacing = 4, Children = {
                new Label { Text = label.ToUpperInvariant(), FontSize = 10, CharacterSpacing = 1, TextColor = Color.FromArgb("#69809E") },
                new Label { Text = value, FontSize = 16, TextColor = Color.FromArgb("#10284A") } } });
        }
        Field("Proveedor / estado", row.Carrier + " · " + row.Status); Field("Cliente", row.Client);
        Field("Plan", row.Summary); Field("Ubicación", row.PhysicalLocation); Field("Equipo", row.DeviceModel);
        Field("Serie", row.SerialNumber); Field("Kit Starlink", row.KitNumber); Field("Soporte", row.SupportPhone);
        Field("IP fija", row.FixedIp); Field("Máscara", row.SubnetMask); Field("Gateway", row.Gateway);
        Field("DNS", row.Dns); Field("LAN", row.Lan); Field("DDNS", row.Ddns);
        Field("Fin de contrato", row.ContractEndsOn?.ToString("dd/MM/yyyy"));
        await Navigation.PushAsync(new ContentPage { Title = "Detalle del servicio", BackgroundColor = Color.FromArgb("#F3F7FC"), Content = new ScrollView { Content = fields } });
    }
}
