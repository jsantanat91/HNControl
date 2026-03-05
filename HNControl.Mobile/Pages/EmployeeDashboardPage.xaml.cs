using HNControl.Mobile.Models;
using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class EmployeeDashboardPage : ContentPage
{
    private readonly EmployeeService _employeeService;
    private readonly AuthService _auth;
    private bool _isBusy;

    public EmployeeDashboardPage(EmployeeService employeeService, AuthService auth)
    {
        InitializeComponent();
        _employeeService = employeeService;
        _auth = auth;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            var data = await _employeeService.DashboardAsync();
            Bind(data);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Sesion expirada", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("No autorizado", StringComparison.OrdinalIgnoreCase))
            {
                _auth.Logout();
                App.SwitchToLogin();
                return;
            }

            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void Bind(EmployeeDashboardDto data)
    {
        FullNameLabel.Text = data.Profile.FullName;
        PositionLabel.Text = $"{data.Profile.Position} | Antiguedad: {data.Profile.SeniorityText}";
        EmailLabel.Text = data.Profile.Email;

        NetPayLabel.Text = data.Payroll.NetQuincenal.ToString("C2");
        VariableLabel.Text = $"{data.Payroll.VariablePercent:0.##}%";
        PayrollPeriodLabel.Text = $"Periodo: {data.Payroll.Period}";

        DeductionsTotalLabel.Text = "-" + data.Payroll.DeductionsQuincenal.ToString("C2");
        BonusesTotalLabel.Text = "+" + data.Payroll.BonusesQuincenal.ToString("C2");

        VacationSummaryLabel.Text =
            $"{data.Vacations.Year}\nDisponibles: {data.Vacations.RemainingDays} / {data.Vacations.AllowanceDays}\nPendientes: {data.Vacations.PendingRequests}";

        ExamsSummaryLabel.Text =
            $"Asignados: {data.Exams.Assigned}\nEn progreso: {data.Exams.InProgress}\nCalificados: {data.Exams.Graded}";

        HistoryCollection.ItemsSource = data.PayrollHistory.Select(x => new HistoryVm
        {
            Label = x.Label,
            VariableRatio = (double)Math.Clamp(x.VariablePercent / 100m, 0m, 1m),
            VariableText = $"{x.VariablePercent:0.#}%"
        }).ToList();

        DeductionsCollection.ItemsSource = data.Deductions.Select(d =>
        {
            var hasProgress = d.ProgressPaidPeriods.HasValue && d.ProgressTotalPeriods.HasValue && d.ProgressTotalPeriods.Value > 0;
            var ratio = hasProgress ? (double)Math.Clamp((decimal)d.ProgressPaidPeriods!.Value / d.ProgressTotalPeriods!.Value, 0m, 1m) : 0d;
            return new DeductionVm
            {
                Concept = d.Concept,
                AmountText = (d.IsBonus ? "+" : "-") + d.PeriodAmount.ToString("C2"),
                AmountColor = d.IsBonus ? Color.FromArgb("#067647") : Color.FromArgb("#B42318"),
                Detail = $"{d.Type} | {d.Direction}",
                HasProgress = hasProgress,
                ProgressRatio = ratio,
                ProgressText = hasProgress ? $"{d.ProgressPaidPeriods}/{d.ProgressTotalPeriods}" : "-"
            };
        }).ToList();

        InventoryCollection.ItemsSource = data.InventoryOrders.Select(x => new InventoryVm
        {
            ItemsPreview = x.ItemsPreview,
            StatusLabel = x.StatusLabel,
            Meta = $"{x.RequestedAt:yyyy-MM-dd HH:mm} | {x.Type} | Lineas: {x.LinesCount} | Responsable: {x.ResponsibleName}"
        }).ToList();
    }

    private sealed class HistoryVm
    {
        public string Label { get; set; } = "";
        public double VariableRatio { get; set; }
        public string VariableText { get; set; } = "";
    }

    private sealed class DeductionVm
    {
        public string Concept { get; set; } = "";
        public string AmountText { get; set; } = "";
        public Color AmountColor { get; set; } = Colors.Black;
        public string Detail { get; set; } = "";
        public bool HasProgress { get; set; }
        public double ProgressRatio { get; set; }
        public string ProgressText { get; set; } = "";
    }

    private sealed class InventoryVm
    {
        public string ItemsPreview { get; set; } = "";
        public string StatusLabel { get; set; } = "";
        public string Meta { get; set; } = "";
    }
}
