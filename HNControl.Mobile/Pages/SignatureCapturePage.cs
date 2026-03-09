using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Shapes;

namespace HNControl.Mobile.Pages;

public sealed class SignatureCapturePage : ContentPage
{
    private readonly SignatureCaptureDrawable _drawable = new();
    private readonly GraphicsView _pad;
    private readonly TaskCompletionSource<string?> _tcs;

    private SignatureCapturePage(string title, string subtitle, TaskCompletionSource<string?> tcs)
    {
        _tcs = tcs;
        Title = title;
        BackgroundColor = Color.FromArgb("#EEF2FF");

        _pad = new GraphicsView
        {
            HeightRequest = 320,
            Drawable = _drawable
        };
        _pad.StartInteraction += OnStart;
        _pad.DragInteraction += OnDrag;
        _pad.EndInteraction += OnEnd;

        var header = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = title, FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#0E2242") },
                new Label { Text = subtitle, FontSize = 13, Opacity = 0.75 }
            }
        };

        var body = new VerticalStackLayout
        {
            Padding = new Thickness(16),
            Spacing = 12,
            Children =
            {
                header,
                new Border
                {
                    Background = Colors.White,
                    Stroke = Color.FromArgb("#CBD5E1"),
                    StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
                    Padding = 6,
                    Content = _pad
                },
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star)
                    },
                    ColumnSpacing = 8,
                    Children =
                    {
                        CreateButton("Cancelar", OnCancel, "#E2E8F0", "#0F172A", 0),
                        CreateButton("Limpiar", OnClear, "#F8FAFE", "#0F172A", 1),
                        CreateButton("Guardar firma", OnSave, "#2252D5", "#FFFFFF", 2)
                    }
                }
            }
        };

        Content = body;
    }

    public static async Task<string?> CaptureAsync(INavigation navigation, string title, string subtitle)
    {
        var tcs = new TaskCompletionSource<string?>();
        var page = new SignatureCapturePage(title, subtitle, tcs);
        await navigation.PushModalAsync(new NavigationPage(page));
        return await tcs.Task;
    }

    private static View CreateButton(string text, EventHandler onClick, string bg, string fg, int column)
    {
        var button = new Button
        {
            Text = text,
            CornerRadius = 10,
            BackgroundColor = Color.FromArgb(bg),
            TextColor = Color.FromArgb(fg)
        };
        button.Clicked += onClick;
        Grid.SetColumn(button, column);
        return button;
    }

    private void OnStart(object? sender, TouchEventArgs e)
    {
        if (!e.Touches.Any()) return;
        var p = e.Touches.First();
        _drawable.StartStroke(p);
        _pad.Invalidate();
    }

    private void OnDrag(object? sender, TouchEventArgs e)
    {
        if (!e.Touches.Any()) return;
        var p = e.Touches.First();
        _drawable.AddPoint(p);
        _pad.Invalidate();
    }

    private void OnEnd(object? sender, TouchEventArgs e)
    {
        _drawable.EndStroke();
        _pad.Invalidate();
    }

    private async void OnCancel(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    private void OnClear(object? sender, EventArgs e)
    {
        _drawable.Clear();
        _pad.Invalidate();
    }

    private async void OnSave(object? sender, EventArgs e)
    {
        if (!_drawable.HasStrokes)
        {
            await DisplayAlertAsync("Firma", "Dibuja la firma antes de guardar.", "OK");
            return;
        }

        var capture = await _pad.CaptureAsync();
        if (capture == null)
        {
            await DisplayAlertAsync("Firma", "No se pudo capturar la firma.", "OK");
            return;
        }

        await using var stream = await capture.OpenReadAsync();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var dataUrl = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());

        _tcs.TrySetResult(dataUrl);
        await Navigation.PopModalAsync();
    }
}

public sealed class SignatureCaptureDrawable : IDrawable
{
    private readonly List<List<PointF>> _strokes = new();
    private List<PointF> _currentStroke = new();

    public bool HasStrokes => _strokes.Count > 0 || _currentStroke.Count > 0;

    public void StartStroke(PointF p)
    {
        _currentStroke = new List<PointF> { p };
    }

    public void AddPoint(PointF p)
    {
        _currentStroke.Add(p);
    }

    public void EndStroke()
    {
        if (_currentStroke.Count > 0)
            _strokes.Add(new List<PointF>(_currentStroke));
        _currentStroke.Clear();
    }

    public void Clear()
    {
        _strokes.Clear();
        _currentStroke.Clear();
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.White;
        canvas.FillRectangle(dirtyRect);

        canvas.StrokeColor = Color.FromArgb("#0F172A");
        canvas.StrokeSize = 2;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        foreach (var stroke in _strokes)
        {
            DrawStroke(canvas, stroke);
        }

        if (_currentStroke.Count > 1)
        {
            DrawStroke(canvas, _currentStroke);
        }
    }

    private static void DrawStroke(ICanvas canvas, List<PointF> stroke)
    {
        if (stroke.Count == 1)
        {
            var p = stroke[0];
            canvas.FillColor = Color.FromArgb("#0F172A");
            canvas.FillCircle(p.X, p.Y, 1.5f);
            return;
        }
        if (stroke.Count < 2) return;
        for (var i = 1; i < stroke.Count; i++)
        {
            var p1 = stroke[i - 1];
            var p2 = stroke[i];
            canvas.DrawLine(p1.X, p1.Y, p2.X, p2.Y);
        }
    }
}
