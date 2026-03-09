using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class LeaveDetailPage : ContentPage
{
    private readonly ModulesService _modules;
    private Guid _leaveId;

    public LeaveDetailPage(ModulesService modules)
    {
        InitializeComponent();
        _modules = modules;
    }

    public void SetLeaveId(Guid leaveId) => _leaveId = leaveId;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_leaveId == Guid.Empty) return;

        try
        {
            var d = await _modules.GetLeaveDetailAsync(_leaveId);
            TitleLabel.Text = d.Type;
            StatusLabel.Text = d.Status;
            RangeLabel.Text = $"{d.StartDate:yyyy-MM-dd} a {d.EndDate:yyyy-MM-dd}";
            MetaLabel.Text = $"Días: {d.TotalDays} | Solicitado: {d.RequestedAt:yyyy-MM-dd HH:mm}";
            ReviewedLabel.Text = d.ReviewedAt.HasValue ? $"Revisado: {d.ReviewedAt.Value:yyyy-MM-dd HH:mm}" : "Revisado: -";
            ReasonLabel.Text = string.IsNullOrWhiteSpace(d.Reason) ? "-" : d.Reason;
            AdminCommentLabel.Text = string.IsNullOrWhiteSpace(d.AdminComment) ? "-" : d.AdminComment;
            EvidenceCollection.ItemsSource = d.EvidenceFiles;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
