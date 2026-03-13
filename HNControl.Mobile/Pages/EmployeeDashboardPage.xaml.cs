using HNControl.Mobile.Models;
using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class EmployeeDashboardPage : ContentPage
{
    private readonly EmployeeService _employeeService;
    private readonly AuthService _auth;
    private bool _isBusy;
    private string _rhFullNotes = "";

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
        PayrollDonut.NetValue = (double)data.Payroll.NetQuincenal;
        PayrollDonut.DeductionValue = (double)data.Payroll.DeductionsQuincenal;
        PayrollDonut.BonusValue = (double)data.Payroll.BonusesQuincenal;

        DeductionsTotalLabel.Text = "-" + data.Payroll.DeductionsQuincenal.ToString("C2");
        BonusesTotalLabel.Text = "+" + data.Payroll.BonusesQuincenal.ToString("C2");

        if (data.KpiFeedback is not null)
        {
            KpiRetroMetaLabel.Text = $"Periodo: {data.KpiFeedback.Period} | Calificado por: {data.KpiFeedback.RatedBy}";
            KpiRetroTitleLabel.Text = "Retro KPI vigente";
            KpiRetroScoreLabel.Text = $"{data.KpiFeedback.VariablePercent:0.##}%";
            _rhFullNotes = string.IsNullOrWhiteSpace(data.KpiFeedback.Notes) ? "" : data.KpiFeedback.Notes;
            KpiRetroNotesLabel.Text = string.IsNullOrWhiteSpace(_rhFullNotes)
                ? "Sin retroalimentacion escrita por RH."
                : CompactPreview(_rhFullNotes, 130);

            KpiMetricsCollection.ItemsSource = data.KpiFeedback.Metrics.Select(x => new KpiMetricVm
            {
                Name = x.Name,
                ScoreText = $"{x.Score:0.0}/5"
            }).ToList();
            RhCommentsButton.IsVisible = !string.IsNullOrWhiteSpace(_rhFullNotes);
        }
        else
        {
            KpiRetroMetaLabel.Text = "Sin evaluacion KPI registrada.";
            KpiRetroTitleLabel.Text = "KPI";
            KpiRetroScoreLabel.Text = "0%";
            KpiRetroNotesLabel.Text = "Aun no hay retroalimentacion de RH.";
            KpiMetricsCollection.ItemsSource = new List<KpiMetricVm>();
            RhCommentsButton.IsVisible = false;
            _rhFullNotes = "";
        }

        if (data.Eval360Feedback is not null)
        {
            Eval360TitleLabel.Text = data.Eval360Feedback.CampaignTitle;
            Eval360MetaLabel.Text = $"Periodo: {data.Eval360Feedback.Period}";

            if (!data.Eval360Feedback.VisibleToEmployee)
            {
                Eval360AutoScoreLabel.Text = "";
                Eval360OthersScoreLabel.Text = "";
                Eval360CommentsCollection.ItemsSource = new List<EvalCommentVm>
                {
                    new()
                    {
                        Competency = "Resultados no publicados",
                        Comment = "Tu administrador aun no habilita visibilidad de resultados para esta campana."
                    }
                };
            }
            else
            {
                Eval360AutoScoreLabel.Text = $"Auto: {data.Eval360Feedback.AutoPercent:0}%";
                Eval360OthersScoreLabel.Text = $"Equipo: {data.Eval360Feedback.OthersPercent:0}%";
                Eval360CommentsCollection.ItemsSource = data.Eval360Feedback.Comments.Select(x => new EvalCommentVm
                {
                    Competency = x.Competency,
                    Comment = x.Comment
                }).ToList();
            }
        }
        else
        {
            Eval360TitleLabel.Text = "Sin campana cerrada";
            Eval360MetaLabel.Text = "Aun no hay resultados de evaluacion 360 para mostrar.";
            Eval360AutoScoreLabel.Text = "";
            Eval360OthersScoreLabel.Text = "";
            Eval360CommentsCollection.ItemsSource = new List<EvalCommentVm>();
        }

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

        var ticketMax = Math.Max(1, data.TicketHistory.Any() ? data.TicketHistory.Max(x => x.Resolved) : 1);
        TicketHistoryCollection.ItemsSource = data.TicketHistory.Select(x => new TicketHistoryVm
        {
            Label = x.Label,
            Ratio = (double)Math.Clamp((decimal)x.Resolved / ticketMax, 0m, 1m),
            ValueText = x.Resolved.ToString()
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

    private async void OnRhCommentsClicked(object? sender, EventArgs e)
    {
        var text = string.IsNullOrWhiteSpace(_rhFullNotes)
            ? "Aun no hay comentarios de RH."
            : _rhFullNotes;
        await DisplayAlertAsync("Comentarios de RH", text, "OK");
    }

    private static string CompactPreview(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var trimmed = text.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "...";
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

    private sealed class TicketHistoryVm
    {
        public string Label { get; set; } = "";
        public double Ratio { get; set; }
        public string ValueText { get; set; } = "";
    }

    private sealed class EvalCommentVm
    {
        public string Competency { get; set; } = "";
        public string Comment { get; set; } = "";
    }

    private sealed class KpiMetricVm
    {
        public string Name { get; set; } = "";
        public string ScoreText { get; set; } = "";
    }

    private sealed class InventoryVm
    {
        public string ItemsPreview { get; set; } = "";
        public string StatusLabel { get; set; } = "";
        public string Meta { get; set; } = "";
    }
}
