using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class MonitoringTargetDetailPage : ContentPage
{
    private readonly ModulesService _modules;
    private Guid _targetId;

    public MonitoringTargetDetailPage(ModulesService modules)
    {
        InitializeComponent();
        _modules = modules;
    }

    public void SetTargetId(Guid targetId) => _targetId = targetId;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_targetId == Guid.Empty) return;
        try
        {
            var d = await _modules.GetMonitoringDetailAsync(_targetId);
            NameLabel.Text = d.Name;
            ClientLabel.Text = d.Client;
            StatusLabel.Text = $"Estatus: {d.Status} | Ultima latencia: {(d.LastLatencyMs.HasValue ? d.LastLatencyMs + " ms" : "-")}";
            AddressLabel.Text = $"Host/IP: {d.Address}";
            ProbeLabel.Text = $"Probe: {d.ProbeType} | Ultimo check: {(d.LastCheckedAt.HasValue ? d.LastCheckedAt.Value.ToString("yyyy-MM-dd HH:mm") : "-")}";
            ContractLabel.Text = $"Contrato: {d.ContractLabel}";
            ServiceLabel.Text = $"Servicio carrier: {d.CarrierServiceLabel}";
            NotesLabel.Text = string.IsNullOrWhiteSpace(d.Notes) ? "Notas: -" : $"Notas: {d.Notes}";
            ChecksCollection.ItemsSource = d.LastChecks.Select(c =>
                $"{c.CheckedAt:yyyy-MM-dd HH:mm} | {(c.Success ? "OK" : "FAIL")} | {(c.LatencyMs.HasValue ? c.LatencyMs + " ms" : "-")} | {(string.IsNullOrWhiteSpace(c.Error) ? "-" : c.Error)}").ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
