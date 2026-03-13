using HNControl.Mobile.Models;
using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class ViaticosModulePage : ContentPage
{
    private readonly ViaticosService _viaticos;
    private readonly IServiceProvider _services;
    private bool _isBusy;

    public ViaticosModulePage(ViaticosService viaticos, IServiceProvider services)
    {
        InitializeComponent();
        _viaticos = viaticos;
        _services = services;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (WeeksCollection.ItemsSource is null)
        {
            await LoadWeeksAsync();
        }
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadWeeksAsync();
        WeeksRefreshView.IsRefreshing = false;
    }

    private async void OnNewWeekClicked(object sender, EventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            var week = await _viaticos.EnsureWeekAsync(DateTime.Today);
            var page = _services.GetRequiredService<ViaticWeekPage>();
            page.SetWeekId(week.Id);
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Viaticos", ex.Message, "OK");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async void OnNewTravelClicked(object sender, EventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var destino = (await DisplayPromptAsync("Solicitud de Gastos", "Destino del viaje", "Continuar", "Cancelar", "Ciudad / sucursal"))?.Trim();
        if (string.IsNullOrWhiteSpace(destino))
        {
            return;
        }

        var motivo = (await DisplayPromptAsync("Solicitud de Gastos", "Motivo del viaje", "Continuar", "Cancelar", "Actividad, alcance, dias estimados"))?.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return;
        }

        var montoTxt = (await DisplayPromptAsync("Solicitud de Gastos", "Monto solicitado", "Crear", "Cancelar", "0.00", keyboard: Keyboard.Numeric))?.Trim();
        if (!decimal.TryParse(montoTxt, out var monto) || monto <= 0m)
        {
            await DisplayAlertAsync("Viaticos", "Monto invalido.", "OK");
            return;
        }

        _isBusy = true;
        try
        {
            var week = await _viaticos.CreateTravelWeekAsync(new ViaticCreateTravelWeekDto
            {
                AnyDayInWeek = DateTime.Today,
                TripDestination = destino,
                TripPurpose = motivo,
                RequestedAdvanceAmount = monto
            });

            var page = _services.GetRequiredService<ViaticWeekPage>();
            page.SetWeekId(week.Id);
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Viaticos", ex.Message, "OK");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async void OnOpenWeekClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not Guid weekId)
        {
            return;
        }

        var page = _services.GetRequiredService<ViaticWeekPage>();
        page.SetWeekId(weekId);
        await Navigation.PushAsync(page);
    }

    private async Task LoadWeeksAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            var data = await _viaticos.GetWeeksAsync(26);
            WeeksCollection.ItemsSource = data;
            Title = $"Viaticos ({data.Count})";
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
}
