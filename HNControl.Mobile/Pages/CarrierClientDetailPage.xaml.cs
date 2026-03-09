using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class CarrierClientDetailPage : ContentPage
{
    private readonly ModulesService _modules;
    private Guid _clientId;

    public CarrierClientDetailPage(ModulesService modules)
    {
        InitializeComponent();
        _modules = modules;
    }

    public void SetClientId(Guid clientId) => _clientId = clientId;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_clientId == Guid.Empty) return;
        try
        {
            var d = await _modules.GetCarrierClientDetailAsync(_clientId);
            ClientLabel.Text = d.ClientName;
            ClientMetaLabel.Text = $"RFC: {d.Rfc} | Email: {d.Email} | Tel: {d.Phone}";
            ServicesCollection.ItemsSource = d.Services.Select(s => new
            {
                s.ServiceLabel,
                s.Carrier,
                Plan = string.IsNullOrWhiteSpace(s.Plan) ? "-" : s.Plan,
                Numbers = $"Cuenta: {(string.IsNullOrWhiteSpace(s.AccountNumber) ? "-" : s.AccountNumber)} | Contrato: {(string.IsNullOrWhiteSpace(s.ContractNumber) ? "-" : s.ContractNumber)} | Circuito: {(string.IsNullOrWhiteSpace(s.CircuitId) ? "-" : s.CircuitId)}",
                Address = $"Dirección: {(string.IsNullOrWhiteSpace(s.ServiceAddress) ? "-" : s.ServiceAddress)} | IP: {(string.IsNullOrWhiteSpace(s.IpInfo) ? "-" : s.IpInfo)}",
                Support = $"Soporte: {(string.IsNullOrWhiteSpace(s.SupportPhone) ? "-" : s.SupportPhone)}",
                Notes = string.IsNullOrWhiteSpace(s.Notes) ? "-" : s.Notes,
                LastNotes = string.IsNullOrWhiteSpace(s.LastNotesSummary) ? "Sin bitácora reciente." : $"Bitácora: {s.LastNotesSummary}"
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
