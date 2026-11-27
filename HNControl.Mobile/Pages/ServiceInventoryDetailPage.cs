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
        SetDynamicResource(BackgroundProperty, "AppBackgroundBrush");
        var content = new VerticalStackLayout { Spacing = 16, Padding = new Thickness(18, 18, 18, 28) };
        var badges = new FlexLayout { Wrap = FlexWrap.Wrap };
        badges.Add(Badge(row.Carrier, "#DCEBFF", "#1257A6"));
        badges.Add(Badge(row.Status, row.IsActive ? "#DDF5E9" : "#E7EBF0", row.IsActive ? "#176447" : "#53657C"));
        if (!string.IsNullOrWhiteSpace(row.PlanType)) badges.Add(Badge(row.PlanType, "#EDE9FF", "#6344A4"));
        if (!string.IsNullOrWhiteSpace(row.DeliveryType)) badges.Add(Badge(row.DeliveryType, "#DBF2FF", "#17607D"));
        var hero = new VerticalStackLayout { Spacing = 12 };
        hero.Add(new Label { Text = row.Name, Style = (Style)Application.Current!.Resources["HeroTitle"] });
        hero.Add(badges);
        hero.Add(new Label { Text = row.Client, Style = (Style)Application.Current.Resources["HeroSubtitle"] });
        content.Add(new Border { Content = hero, Style = (Style)Application.Current.Resources["HeaderCard"] });
        content.Add(_feedback);

        var account = Section(content, "Cuenta del proveedor");
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

        var support = Section(content, "Soporte");
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

        var plan = Section(content, "Plan y contrato");
        Field(plan, "Plan contratado", row.PlanName, false);
        Field(plan, "Velocidad", row.SpeedMbps.HasValue ? $"{row.SpeedMbps:0.##} Mbps" : null, false);
        Field(plan, "Costo mensual", row.MonthlyCost?.ToString("C2", CultureInfo.GetCultureInfo("es-MX")) is { } cost ? cost + " MXN" : null, false);
        Field(plan, "Plazo", row.ContractMonths.HasValue ? $"{row.ContractMonths} meses" : "Sin plazo registrado", false);
        Field(plan, "Inicio del contrato", row.ContractStartsOn?.ToString("dd/MM/yyyy"), false);
        Field(plan, "Fin del contrato", row.ContractEndsOn?.ToString("dd/MM/yyyy"), false);
        var equipment = Section(content, "Equipo y ubicación");
        Field(equipment, "Modelo", row.DeviceModel);
        Field(equipment, "Número de serie", row.SerialNumber);
        if (row.CarrierKey == "starlink") Field(equipment, "Número de kit", row.KitNumber);
        Field(equipment, "Ubicación física", row.PhysicalLocation);
        var network = Section(content, "Configuración de red");
        Field(network, "IP fija", row.FixedIp); Field(network, "Máscara de red", row.SubnetMask);
        Field(network, "Gateway", row.Gateway); Field(network, "DNS", row.Dns);
        Field(network, "LAN", row.Lan); Field(network, "DDNS", row.Ddns);
        var notes = Section(content, "Notas del servicio"); Field(notes, "Indicaciones", row.Notes, false);
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
        BackgroundColor = Color.FromArgb(background), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 999 },
        Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 6, 6), HorizontalOptions = LayoutOptions.Start,
        Content = new Label { Text = text, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(foreground) }
    };
    private static VerticalStackLayout Section(VerticalStackLayout parent, string title)
    {
        var stack = new VerticalStackLayout { Spacing = 16 };
        stack.Add(new Label { Text = title, Style = (Style)Application.Current!.Resources["SectionTitle"] });
        parent.Add(new Border { Content = stack, Style = (Style)Application.Current.Resources["SurfaceCard"] });
        return stack;
    }
    private static Button ActionButton(string text, bool primary) => new() {
        Text = text,
        Style = (Style)Application.Current!.Resources[primary ? "PrimaryButton" : "GhostButton"]
    };
    public sealed class AccountSecret { public string Password { get; set; } = ""; }
}
