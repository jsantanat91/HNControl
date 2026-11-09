using HNControl.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace HNControl.Mobile.Pages;

public sealed class ServiceAccountsPage : ContentPage
{
    private readonly MobileApiClient _api;
    private readonly VerticalStackLayout _items = new() { Spacing = 12 };
    private readonly SearchBar _search = new() { Placeholder = "Proveedor, cuenta o correo" };
    private readonly Label _status = new() { FontSize = 12, TextColor = Color.FromArgb("#627793") };
    private readonly Button _prev = new() { Text = "Anterior" }, _next = new() { Text = "Siguiente" };
    private int _page = 1;
    private bool _busy;
    public ServiceAccountsPage(MobileApiClient api)
    {
        _api = api; Title = "Cuentas de proveedores"; BackgroundColor = Color.FromArgb("#F3F7FC");
        var root = new Grid { Padding = 18, RowSpacing = 12, RowDefinitions = new RowDefinitionCollection(new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)) };
        root.Add(new Label { Text = "Tus cuentas, a la mano", FontSize = 23, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#173B5D") });
        root.Add(_search, 0, 1); root.Add(_status, 0, 2); root.Add(new ScrollView { Content = _items }, 0, 3);
        var nav = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new(GridLength.Star), new(GridLength.Star)), ColumnSpacing = 12 }; nav.Add(_prev); nav.Add(_next, 1); root.Add(nav, 0, 4);
        _search.SearchButtonPressed += async (_, _) => { if(_busy)return; _page = 1; await Load(); };
        _prev.Clicked += async (_, _) => { if(_busy)return; _page--; await Load(); }; _next.Clicked += async (_, _) => { if(_busy)return; _page++; await Load(); };
        Content = root;
    }
    protected override async void OnAppearing() { base.OnAppearing(); await Load(); }
    private async Task Load()
    {
        if(_busy)return; _busy = true; _prev.IsEnabled = _next.IsEnabled = false; _status.Text = "Consultando cuentas…";
        try
        {
            var response = await _api.GetJsonAsync<AccountResponse>($"api/mobile/service-accounts?page={_page}&search={Uri.EscapeDataString(_search.Text ?? "")}");
            _items.Clear(); _status.Text = $"{response.Total} cuenta(s) · Página {_page}";
            foreach (var item in response.Items)
            {
                var stack = new VerticalStackLayout { Spacing = 8 };
                stack.Add(new Label { Text = item.CarrierKey.ToUpperInvariant() + " · " + (item.IsActive ? "ACTIVA" : "INACTIVA"), FontSize = 11, TextColor = Color.FromArgb("#1764D9"), FontAttributes = FontAttributes.Bold });
                stack.Add(new Label { Text = item.Label, FontSize = 19, FontAttributes = FontAttributes.Bold });
                stack.Add(new Label { Text = "Cuenta: " + (string.IsNullOrWhiteSpace(item.AccountNumber) ? "Sin registrar" : item.AccountNumber), FontSize = 14 });
                stack.Add(new Label { Text = item.Email, FontSize = 13 });
                stack.Add(new Label { Text = $"{item.ServiceCount} servicio(s) vinculado(s)", FontSize = 12, TextColor = Color.FromArgb("#627793") });
                var open = new Button { Text = "Ver cuenta y copiar datos", BackgroundColor = Color.FromArgb("#E7F1FF"), TextColor = Color.FromArgb("#1764D9") };
                open.Clicked += async (_, _) => await Open(item); stack.Add(open);
                _items.Add(new Border { Content = stack, BackgroundColor = Colors.White, Padding = 18, Stroke = Color.FromArgb("#D9E6F5"), StrokeShape = new RoundRectangle { CornerRadius = 18 } });
            }
            _prev.IsEnabled = _page > 1; _next.IsEnabled = _page * 50 < response.Total;
        }
        catch (Exception ex) { _items.Clear(); _status.Text = ex.Message; }
        finally { _busy = false; }
    }
    private async Task Open(AccountRow item)
    {
        var page = new ContentPage { Title = item.Label, BackgroundColor = Color.FromArgb("#F3F7FC") };
        var stack = new VerticalStackLayout { Padding = 22, Spacing = 16 };
        stack.Add(new Label { Text = item.Label, FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#173B5D") });
        stack.Add(new Label { Text = item.CarrierKey.ToUpperInvariant() + " · " + (item.IsActive ? "Activa" : "Inactiva"), TextColor = Color.FromArgb("#1764D9") });
        void Field(string title, string value)
        {
            stack.Add(new Label { Text = title, FontSize = 12, TextColor = Color.FromArgb("#627793") });
            stack.Add(new Label { Text = string.IsNullOrWhiteSpace(value) ? "Sin registrar" : value, FontSize = 17 });
            if(string.IsNullOrWhiteSpace(value))return;
            var copy = new Button { Text = "Copiar " + title.ToLowerInvariant(), BackgroundColor = Color.FromArgb("#E7F1FF"), TextColor = Color.FromArgb("#1764D9") };
            copy.Clicked += async (_, _) => { try { await Clipboard.Default.SetTextAsync(value); await page.DisplayAlertAsync("Cuenta", "Dato copiado.", "Aceptar"); } catch { await page.DisplayAlertAsync("Cuenta", "No se pudo copiar.", "Aceptar"); } }; stack.Add(copy);
        }
        Field("Número de cuenta", item.AccountNumber); Field("Correo de acceso", item.Email);
        stack.Add(new Label { Text = "Contraseña", FontSize = 12, TextColor = Color.FromArgb("#627793") });
        stack.Add(new Label { Text = item.CanCopyPassword ? "•••••••• · Solo se puede copiar" : "Sin contraseña registrada" });
        if(item.CanCopyPassword)
        {
            var password = new Button { Text = "Copiar contraseña", BackgroundColor = Color.FromArgb("#1764D9"), TextColor = Colors.White };
            password.Clicked += async (_, _) => {
                password.IsEnabled = false;
                try { var result = await _api.PostJsonAsync<object, ServiceInventoryDetailPage.AccountSecret>($"api/mobile/service-accounts/{item.Id}/password", new { }); await Clipboard.Default.SetTextAsync(result.Password); await page.DisplayAlertAsync("Cuenta", "Contraseña copiada.", "Aceptar"); }
                catch(Exception ex) { await page.DisplayAlertAsync("No se pudo copiar", ex.Message, "Aceptar"); }
                finally { password.IsEnabled = true; }
            }; stack.Add(password);
        }
        Field("Notas de la cuenta", item.Notes); page.Content = new ScrollView { Content = stack }; await Navigation.PushAsync(page);
    }
    public sealed class AccountResponse { public List<AccountRow> Items { get; set; } = new(); public int Total { get; set; } }
    public sealed class AccountRow
    {
        public Guid Id { get; set; } public string Label { get; set; } = ""; public string CarrierKey { get; set; } = "";
        public string Email { get; set; } = ""; public string AccountNumber { get; set; } = ""; public string Notes { get; set; } = "";
        public bool IsActive { get; set; } public bool CanCopyPassword { get; set; } public int ServiceCount { get; set; }
    }
}
