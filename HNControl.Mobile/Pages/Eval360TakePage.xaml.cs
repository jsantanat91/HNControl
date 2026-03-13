using HNControl.Mobile.Models;
using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class Eval360TakePage : ContentPage
{
    private readonly ModulesService _modules;
    private Guid _assignmentId;
    private Eval360TakeDto? _model;

    private readonly Dictionary<Guid, Picker> _questionPickers = new();
    private readonly Dictionary<Guid, Editor> _commentEditors = new();

    public Eval360TakePage(ModulesService modules)
    {
        InitializeComponent();
        _modules = modules;
    }

    public void SetAssignment(Guid assignmentId) => _assignmentId = assignmentId;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_assignmentId == Guid.Empty) return;

        try
        {
            _model = await _modules.GetEval360TakeAsync(_assignmentId);
            CampaignLabel.Text = _model.Campaign;
            SubjectLabel.Text = $"Evaluando a: {_model.SubjectName}";

            _questionPickers.Clear();
            _commentEditors.Clear();
            CompetenciesHost.Children.Clear();

            foreach (var c in _model.Competencies)
            {
                var block = new Border
                {
                    Style = (Style)Application.Current!.Resources["SurfaceCard"],
                    Content = BuildCompetencyView(c)
                };
                CompetenciesHost.Children.Add(block);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Evaluación 360", ex.Message, "OK");
        }
    }

    private View BuildCompetencyView(Eval360TakeCompetencyDto c)
    {
        var root = new VerticalStackLayout { Spacing = 8 };
        root.Children.Add(new Label
        {
            Text = c.Competency,
            FontAttributes = FontAttributes.Bold,
            FontSize = 17
        });

        foreach (var q in c.Questions)
        {
            var qWrap = new VerticalStackLayout { Spacing = 4 };
            qWrap.Children.Add(new Label
            {
                Text = q.Text,
                FontSize = 14,
                LineBreakMode = LineBreakMode.WordWrap
            });

            var picker = new Picker { Title = "Calificación (1-5)" };
            picker.ItemsSource = new List<string> { "1", "2", "3", "4", "5" };
            picker.SelectedIndex = Math.Clamp((q.Score <= 0 ? 3 : q.Score) - 1, 0, 4);
            _questionPickers[q.QuestionId] = picker;
            qWrap.Children.Add(picker);
            root.Children.Add(qWrap);
        }

        root.Children.Add(new Label
        {
            Text = "Comentario (opcional)",
            FontSize = 12,
            TextColor = Color.FromArgb("#64748B")
        });

        var editor = new Editor
        {
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 80,
            Placeholder = "Comentario por competencia..."
        };
        editor.Text = c.Comment ?? string.Empty;
        _commentEditors[c.CompetencyId] = editor;
        root.Children.Add(editor);
        return root;
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (_model == null) return;
        try
        {
            var body = new Eval360SubmitDto
            {
                Scores = _model.Competencies
                    .SelectMany(c => c.Questions)
                    .Select(q => new Eval360SubmitScoreDto
                    {
                        QuestionId = q.QuestionId,
                        Score = (_questionPickers.TryGetValue(q.QuestionId, out var p) ? p.SelectedIndex : 2) + 1
                    })
                    .ToList(),
                Comments = _model.Competencies
                    .Select(c => new Eval360SubmitCommentDto
                    {
                        CompetencyId = c.CompetencyId,
                        Comment = _commentEditors.TryGetValue(c.CompetencyId, out var ed) ? (ed.Text ?? "").Trim() : ""
                    })
                    .ToList()
            };

            var resp = await _modules.SubmitEval360Async(_assignmentId, body);
            await DisplayAlertAsync("Evaluación 360", resp.Message, "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Evaluación 360", ex.Message, "OK");
        }
    }
}

