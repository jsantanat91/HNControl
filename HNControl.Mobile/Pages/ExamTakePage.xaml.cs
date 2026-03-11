using HNControl.Mobile.Models;
using HNControl.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace HNControl.Mobile.Pages;

public partial class ExamTakePage : ContentPage
{
    private readonly ModulesService _modules;
    private readonly Dictionary<Guid, QuestionState> _states = new();
    private Guid _assignmentId;
    private bool _isReadOnly;
    private bool _isChangingChoice;

    public ExamTakePage(ModulesService modules)
    {
        InitializeComponent();
        _modules = modules;
    }

    public void SetAssignment(Guid assignmentId)
    {
        _assignmentId = assignmentId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_assignmentId != Guid.Empty)
        {
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            var dto = await _modules.GetExamTakeAsync(_assignmentId);
            TitleLabel.Text = dto.Title;
            DescriptionLabel.Text = string.IsNullOrWhiteSpace(dto.Description) ? "-" : dto.Description;
            MetaLabel.Text = $"Estado: {NormalizeStatus(dto.Status)}"
                             + (dto.DueAt.HasValue ? $" | Limite: {dto.DueAt:yyyy-MM-dd HH:mm}" : "")
                             + (dto.MaxScore > 0m ? $" | Calificacion: {dto.Score:N2}/{dto.MaxScore:N2}" : "");

            _isReadOnly = dto.Status.Equals("Submitted", StringComparison.OrdinalIgnoreCase)
                          || dto.Status.Equals("Graded", StringComparison.OrdinalIgnoreCase);

            QuestionsStack.Children.Clear();
            _states.Clear();

            foreach (var q in (dto.Questions ?? new List<ExamTakeQuestionDto>()).OrderBy(x => x.Ordinal))
            {
                var card = BuildQuestionCard(q);
                QuestionsStack.Children.Add(card);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Examen", ex.Message, "OK");
        }
    }

    private View BuildQuestionCard(ExamTakeQuestionDto q)
    {
        var layout = new VerticalStackLayout { Spacing = 8 };
        layout.Children.Add(new Label
        {
            Text = $"{q.Ordinal}. {q.Text}",
            FontAttributes = FontAttributes.Bold,
            FontSize = 16
        });
        layout.Children.Add(new Label
        {
            Text = $"{NormalizeQuestionType(q.Type)} | Puntos: {q.Points:N2}" + (q.IsRequired ? " | Requerida" : ""),
            FontSize = 12,
            Opacity = 0.75
        });

        var state = new QuestionState { Question = q };
        _states[q.QuestionId] = state;

        if (q.Type.Equals("OpenText", StringComparison.OrdinalIgnoreCase)
            || q.Type.Equals("Attachment", StringComparison.OrdinalIgnoreCase))
        {
            var editor = new Editor
            {
                Text = q.TextAnswer ?? "",
                AutoSize = EditorAutoSizeOption.TextChanges,
                Placeholder = q.Type.Equals("Attachment", StringComparison.OrdinalIgnoreCase)
                    ? "Respuesta/nota. Adjuntos se gestionan en web."
                    : "Escribe tu respuesta..."
            };
            editor.IsEnabled = !_isReadOnly;
            state.TextEditor = editor;
            layout.Children.Add(editor);
        }
        else
        {
            var checksWrap = new VerticalStackLayout { Spacing = 6 };
            var selected = new HashSet<Guid>(q.SelectedChoiceIds ?? new List<Guid>());
            foreach (var c in q.Choices.OrderBy(x => x.Ordinal))
            {
                var check = new CheckBox
                {
                    IsChecked = selected.Contains(c.ChoiceId),
                    IsEnabled = !_isReadOnly
                };
                check.CheckedChanged += (_, args) => OnChoiceChanged(q, c.ChoiceId, check, args.Value);

                state.ChoiceChecks.Add((c.ChoiceId, check));

                var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(30), new(GridLength.Star) } };
                row.Add(check, 0, 0);
                row.Add(new Label { Text = c.Text, VerticalTextAlignment = TextAlignment.Center }, 1, 0);
                checksWrap.Children.Add(row);
            }
            layout.Children.Add(checksWrap);
        }

        return new Border
        {
            Background = Colors.White,
            Padding = 12,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Content = layout
        };
    }

    private void OnChoiceChanged(ExamTakeQuestionDto q, Guid changedChoiceId, CheckBox source, bool isChecked)
    {
        if (_isChangingChoice) return;
        if (!q.Type.Equals("SingleChoice", StringComparison.OrdinalIgnoreCase) || !isChecked) return;

        if (!_states.TryGetValue(q.QuestionId, out var state)) return;
        _isChangingChoice = true;
        try
        {
            foreach (var entry in state.ChoiceChecks)
            {
                if (entry.ChoiceId != changedChoiceId)
                {
                    entry.Check.IsChecked = false;
                }
            }
        }
        finally
        {
            _isChangingChoice = false;
        }
    }

    private ExamTakeSaveDto BuildSaveBody()
    {
        var body = new ExamTakeSaveDto();
        foreach (var kv in _states)
        {
            var q = kv.Value.Question;
            if (q.Type.Equals("OpenText", StringComparison.OrdinalIgnoreCase)
                || q.Type.Equals("Attachment", StringComparison.OrdinalIgnoreCase))
            {
                body.Answers.Add(new ExamTakeAnswerInputDto
                {
                    QuestionId = q.QuestionId,
                    TextAnswer = kv.Value.TextEditor?.Text ?? "",
                    ChoiceIds = new List<Guid>()
                });
            }
            else
            {
                var selected = kv.Value.ChoiceChecks.Where(x => x.Check.IsChecked).Select(x => x.ChoiceId).ToList();
                body.Answers.Add(new ExamTakeAnswerInputDto
                {
                    QuestionId = q.QuestionId,
                    TextAnswer = "",
                    ChoiceIds = selected
                });
            }
        }
        return body;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (_isReadOnly)
        {
            await DisplayAlertAsync("Examen", "Este examen ya fue enviado/calificado.", "OK");
            return;
        }

        try
        {
            var res = await _modules.SaveExamAsync(_assignmentId, BuildSaveBody());
            await DisplayAlertAsync("Examen", res.Message, "OK");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Examen", ex.Message, "OK");
        }
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (_isReadOnly)
        {
            await DisplayAlertAsync("Examen", "Este examen ya fue enviado/calificado.", "OK");
            return;
        }

        var confirm = await DisplayAlertAsync("Examen", "Se enviara el examen para revision/calificacion. Deseas continuar?", "Enviar", "Cancelar");
        if (!confirm) return;

        try
        {
            var res = await _modules.SubmitExamAsync(_assignmentId, BuildSaveBody());
            await DisplayAlertAsync("Examen", res.Message, "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Examen", ex.Message, "OK");
        }
    }

    private static string NormalizeQuestionType(string raw)
        => raw switch
        {
            "SingleChoice" => "Opcion unica",
            "MultipleChoice" => "Seleccion multiple",
            "OpenText" => "Respuesta abierta",
            "Attachment" => "Respuesta abierta/adjunto",
            _ => raw
        };

    private static string NormalizeStatus(string raw)
        => raw switch
        {
            "Assigned" => "Asignado",
            "InProgress" => "En progreso",
            "Submitted" => "Enviado",
            "Graded" => "Calificado",
            _ => raw
        };

    private sealed class QuestionState
    {
        public ExamTakeQuestionDto Question { get; set; } = new();
        public Editor? TextEditor { get; set; }
        public List<(Guid ChoiceId, CheckBox Check)> ChoiceChecks { get; set; } = new();
    }
}
