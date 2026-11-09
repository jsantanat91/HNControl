using HNControl.Mobile.Models;
using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public sealed class TravelRequestPage : ContentPage
{
    private readonly Entry _destination = new() { Placeholder = "Ciudad o sucursal", MaxLength = 220 };
    private readonly Editor _purpose = new() { Placeholder = "Actividad y alcance del viaje", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 100, MaxLength = 500 };
    private readonly Entry _amount = new() { Placeholder = "0.00", Keyboard = Keyboard.Numeric };
    private readonly DatePicker _date = new() { Date = DateTime.Today, Format = "dd MMM yyyy" };
    private readonly Button _save = new() { Text = "Crear solicitud", Style = (Style)Application.Current!.Resources["PrimaryButton"] };
    private readonly Label _error = new() { TextColor = Color.FromArgb("#B42318"), IsVisible = false };
    private bool _busy;

    public TravelRequestPage(ViaticosService service, IServiceProvider services)
    {
        Title = "Solicitar viaje";
        BackgroundColor = Color.FromArgb("#F3F7FB");
        var form = new VerticalStackLayout { Spacing = 16 };
        form.Add(new Label { Text = "Tu próximo viaje", FontSize = 27, FontAttributes = FontAttributes.Bold });
        form.Add(new Label { Text = "Solicita el anticipo y lleva el seguimiento de tus comprobantes.", TextColor = Color.FromArgb("#69809E") });
        AddField(form, "Fecha del viaje", _date);
        AddField(form, "Destino", _destination);
        AddField(form, "Motivo", _purpose);
        AddField(form, "Anticipo solicitado · MXN", _amount);
        form.Add(_error);
        form.Add(_save);
        Content = new ScrollView { Content = new Border { Margin = 20, Style = (Style)Application.Current.Resources["SurfaceCard"], Content = form } };
        _save.Clicked += async (_, _) =>
        {
            if (_busy) return;
            _error.IsVisible = false;
            if (string.IsNullOrWhiteSpace(_destination.Text) || string.IsNullOrWhiteSpace(_purpose.Text)
                || !decimal.TryParse(_amount.Text, out var amount) || amount <= 0 || amount > 9999999)
            {
                _error.Text = "Completa destino, motivo y un importe entre $0.01 y $9,999,999.";
                _error.IsVisible = true;
                return;
            }
            _busy = true;
            _save.IsEnabled = false;
            _save.Text = "Creando solicitud…";
            try
            {
                var result = await service.CreateTravelWeekAsync(new ViaticCreateTravelWeekDto
                {
                    AnyDayInWeek = _date.Date ?? DateTime.Today,
                    TripDestination = _destination.Text.Trim(),
                    TripPurpose = _purpose.Text.Trim(),
                    RequestedAdvanceAmount = amount
                });
                var page = services.GetRequiredService<ViaticWeekPage>();
                page.SetWeekId(result.Id);
                await Navigation.PushAsync(page);
                Navigation.RemovePage(this);
            }
            catch (Exception ex) { _error.Text = ex.Message; _error.IsVisible = true; }
            finally { _busy = false; _save.IsEnabled = true; _save.Text = "Crear solicitud"; }
        };
    }

    private static void AddField(VerticalStackLayout layout, string title, View input)
    {
        layout.Add(new Label { Text = title, FontSize = 12, FontAttributes = FontAttributes.Bold });
        layout.Add(new Border { Style = (Style)Application.Current!.Resources["InputCard"], Content = input });
    }
}
