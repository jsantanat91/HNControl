using HNControl.Mobile.Models;
using HNControl.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using System.Globalization;

namespace HNControl.Mobile.Pages;

public sealed class ServiceInventoryDetailPage : ContentPage
{
    private readonly Label _feedback = new() { FontSize = 12, TextColor = Color.FromArgb("#1764D9"), IsVisible = false };
    private static readonly Color Ink = Color.FromArgb("#10284A");
    private static readonly Color Muted = Color.FromArgb("#69809E");
    public ServiceInventoryDetailPage(ServiceInventoryRow row, MobileApiClient api)
    {
        Title = "Detalle del servicio";
        BackgroundColor = Color.FromArgb("#F3F7FC");
        var content = new VerticalStackLayout { Spacing = 16, Padding = new Thickness(18, 18, 18, 28) };
        var badges = new FlexLayout { Wrap = FlexWrap.Wrap };
        badges.Add(Badge(row.Carrier, "#DCEBFF", "#1257A6"));
        badges.Add(Badge(row.Status, row.IsActive ? "#DDF5E9" : "#E7EBF0", row.IsActive ? "#176447" : "#53657C"));
        if (!string.IsNullOrWhiteSpace(row.PlanType)) badges.Add(Badge(row.PlanType, "#EDE9FF", "#6344A4"));
        if (!string.IsNullOrWhiteSpace(row.DeliveryType)) badges.Add(Badge(row.DeliveryType, "#DBF2FF", "#17607D"));
        var hero = new VerticalStackLayout { Spacing = 12 };
        hero.Add(new Label { Text = "INVENTARIO DE SERVICIOS", FontSize = 10, CharacterSpacing = 1.4, TextColor = Color.FromArgb("#87C9FF") });
        hero.Add(new Label { Text = row.Name, FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = Colors.White });
        hero.Add(badges);
        hero.Add(new Label { Text = row.Client, FontSize = 14, TextColor = Color.FromArgb("#C9DCF3") });
        content.Add(Card(hero, "#10284A", 22));
        content.Add(_feedback);

        var account = Section(content, "01", "Cuenta del proveedor", "Ten estos datos a la mano al comunicarte con soporte.");
        Field(account, "Cuenta vinculada", row.AccountId.HasValue ? row.AccountLabel : "Sin cuenta vinculada", false);
        if (row.AccountId.HasValue) account.Add(Badge(row.AccountIsActive ? "Cuenta activa" : "Cuenta inactiva", row.AccountIsActive ? "#E6F5EE" : "#FFF0DB", "#52657B"));
        Field(account, "Número de cuenta", row.ProviderAccountNumber);
        Field(account, "Número de servicio / línea", row.AccountNumber);
        Field(account, "Correo de acceso", row.AccountEmail);
        if (row.CanCopyPassword)
        {
            var password = ActionButton("Copiar contraseña", false);
            password.Clicked += async (_, _) => {
                password.IsEnabled = false;
                try {
                    var result = await api.PostJsonAsync<object, AccountSecret>($"api/mobile/service-inventory/{row.Id}/account-password", new { });
                    if (string.IsNullOrEmpty(result.Password)) throw new InvalidOperationException("La cuenta no tiene una contraseña registrada.");
                    await Clipboard.Default.SetTextAsync(result.Password);
                    Notice("Contraseña copiada al portapapeles.");
                } catch (Exception ex) { await DisplayAlertAsync("No se pudo copiar", ex.Message, "Aceptar"); }
                finally { password.IsEnabled = true; }
            };
            account.Add(password);
        }
        Field(account, "Notas de la cuenta", row.AccountNotes, false);

        var support = Section(content, "02", "Soporte", "Abre el marcador y utiliza el número de cuenta registrado.");
        Field(support, "Canal de atención", row.SupportPhone);
        if (row.CarrierKey == "starlink")
        {
            var online = ActionButton("Abrir soporte Starlink  ↗", true);
            online.Clicked += async (_, _) => { try { await Launcher.Default.OpenAsync("https://www.starlink.com/support"); } catch { await DisplayAlertAsync("Soporte", "No se pudo abrir el navegador.", "Aceptar"); } };
            support.Add(online);
        }
        else if (row.SupportNumber is { } phone)
        {
            var call = ActionButton("Llamar a soporte", true);
            call.Clicked += async (_, _) => {
                try { if (!PhoneDialer.Default.IsSupported) throw new InvalidOperationException(); PhoneDialer.Default.Open(phone); }
                catch { await DisplayAlertAsync("Soporte", "Este dispositivo no tiene marcador disponible. Puedes copiar el teléfono de soporte.", "Aceptar"); }
            };
            support.Add(call);
        }

        var plan = Section(content, "03", "Plan y contrato");
        Field(plan, "Plan contratado", row.PlanName, false);
        Field(plan, "Velocidad", row.SpeedMbps.HasValue ? $"{row.SpeedMbps:0.##} Mbps" : null, false);
        Field(plan, "Costo mensual", row.MonthlyCost?.ToString("C2", CultureInfo.GetCultureInfo("es-MX")) is { } cost ? cost + " MXN" : null, false);
        Field(plan, "Plazo", row.ContractMonths.HasValue ? $"{row.ContractMonths} meses" : "Sin plazo registrado", false);
        Field(plan, "Inicio del contrato", row.ContractStartsOn?.ToString("dd/MM/yyyy"), false);
        Field(plan, "Fin del contrato", row.ContractEndsOn?.ToString("dd/MM/yyyy"), false);
        var equipment = Section(content, "04", "Equipo y ubicación");
        Field(equipment, "Modelo", row.DeviceModel);
        Field(equipment, "Número de serie", row.SerialNumber);
        if (row.CarrierKey == "starlink") Field(equipment, "Número de kit", row.KitNumber);
        Field(equipment, "Ubicación física", row.PhysicalLocation);
        var network = Section(content, "05", "Configuración de red");
        Field(network, "IP fija", row.FixedIp); Field(network, "Máscara de red", row.SubnetMask);
        Field(network, "Gateway", row.Gateway); Field(network, "DNS", row.Dns);
        Field(network, "LAN", row.Lan); Field(network, "DDNS", row.Ddns);
        var notes = Section(content, "06", "Notas del servicio"); Field(notes, "Indicaciones", row.Notes, false);
        Content = new ScrollView { Content = content };
    }
    private void Notice(string text) { _feedback.Text = text; _feedback.IsVisible = true; }
    private void Field(VerticalStackLayout target, string title, string? value, bool copy = true)
    {
        var available = !string.IsNullOrWhiteSpace(value);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 10 };
        row.Add(new VerticalStackLayout { Spacing = 4, Children = {
            new Label { Text = title.ToUpperInvariant(), FontSize = 10, CharacterSpacing = .6, TextColor = Muted },
            new Label { Text = available ? value : "Sin registrar", FontSize = 15, TextColor = available ? Ink : Muted, LineBreakMode = LineBreakMode.WordWrap }
        } });
        if (copy && available)
        {
            var button = ActionButton("Copiar", false); button.FontSize = 11; button.Padding = new Thickness(10, 6);
            button.Clicked += async (_, _) => { try { await Clipboard.Default.SetTextAsync(value!); Notice(title + " copiado."); } catch { await DisplayAlertAsync("Copiar", "No se pudo acceder al portapapeles.", "Aceptar"); } };
            row.Add(button, 1);
        }
        target.Add(row);
    }
    private static Border Badge(string text, string background, string foreground) => new() {
        BackgroundColor = Color.FromArgb(background), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 8 },
        Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 6, 6), HorizontalOptions = LayoutOptions.Start,
        Content = new Label { Text = text, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(foreground) }
    };
    private static Border Card(View content, string color = "#FFFFFF", int padding = 18) => new() {
        Content = content, BackgroundColor = Color.FromArgb(color), Stroke = Color.FromArgb("#DFE8F2"), StrokeThickness = color == "#FFFFFF" ? 1 : 0,
        StrokeShape = new RoundRectangle { CornerRadius = 20 }, Padding = padding
    };
    private static VerticalStackLayout Section(VerticalStackLayout parent, string number, string title, string? hint = null)
    {
        var stack = new VerticalStackLayout { Spacing = 16 };
        stack.Add(new HorizontalStackLayout { Spacing = 8, Children = { Badge(number, "#EAF3FF", "#1764D9"), new Label { Text = title, FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Ink, VerticalOptions = LayoutOptions.Center } } });
        if (hint != null) stack.Add(new Label { Text = hint, FontSize = 12, TextColor = Muted });
        parent.Add(Card(stack)); return stack;
    }
    private static Button ActionButton(string text, bool primary) => new() {
        Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold, CornerRadius = 12, MinimumHeightRequest = 44,
        BackgroundColor = Color.FromArgb(primary ? "#087EE8" : "#EAF3FF"), TextColor = primary ? Colors.White : Color.FromArgb("#1764D9")
    };
    public sealed class AccountSecret { public string Password { get; set; } = ""; }
}
