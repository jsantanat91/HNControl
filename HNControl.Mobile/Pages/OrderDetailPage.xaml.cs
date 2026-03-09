using System.Collections.ObjectModel;
using HNControl.Mobile.Models;
using HNControl.Mobile.Services;
using Microsoft.Maui.Graphics;

namespace HNControl.Mobile.Pages;

public partial class OrderDetailPage : ContentPage
{
    private readonly OrdersService _orders;
    private readonly SignatureDrawable _signature = new();
    private readonly SignatureDrawable _clientSignature = new();
    private Guid _orderId;
    private ServiceOrderDetailDto? _current;
    private bool _busy;

    public ObservableCollection<ChecklistEditItemVm> ChecklistItems { get; } = new();

    public OrderDetailPage(OrdersService orders)
    {
        InitializeComponent();
        _orders = orders;
        ChecklistCollection.ItemsSource = ChecklistItems;
        SignaturePad.Drawable = _signature;
        ClientSignaturePad.Drawable = _clientSignature;
    }

    public void SetOrderId(Guid orderId)
    {
        _orderId = orderId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RootScroll.IsEnabled = true;
        if (_orderId == Guid.Empty) return;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            var detail = await _orders.DetailAsync(_orderId);
            _current = detail;
            Bind(detail);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private void Bind(ServiceOrderDetailDto d)
    {
        TitleLabel.Text = d.Title;
        ClientLabel.Text = d.Client;
        TypeLabel.Text = MapType(d.Type);
        StatusLabel.Text = MapStatus(d.Status);
        AreaLabel.Text = MapArea(d.CurrentArea);
        TypeChip.BackgroundColor = Color.FromArgb(MapTypeBg(d.Type));
        StatusChip.BackgroundColor = Color.FromArgb(MapStatusBg(d.Status));
        AreaChip.BackgroundColor = Color.FromArgb(MapAreaBg(d.CurrentArea));

        ClaimedByLabel.Text = "Tomada por: " + (string.IsNullOrWhiteSpace(d.ClaimedBy) ? "Sin tomar" : d.ClaimedBy);
        CreatedLabel.Text = "Creada: " + d.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        EstimatedLabel.Text = d.EstimatedEndDate.HasValue
            ? "Entrega estimada: " + d.EstimatedEndDate.Value.ToLocalTime().ToString("yyyy-MM-dd")
            : "Entrega estimada: -";

        DescriptionLabel.Text = string.IsNullOrWhiteSpace(d.Description) ? "-" : d.Description;
        LevantamientoEditor.Text = d.LevantamientoNotes ?? "";
        MaterialesEditor.Text = d.MaterialesNotes ?? "";
        EvidenceCollection.ItemsSource = d.Evidences.Select(e => $"{e.UploadedAtLocal} · {e.OriginalFileName}").ToList();

        ChecklistItems.Clear();
        foreach (var i in d.Checklist)
        {
            ChecklistItems.Add(new ChecklistEditItemVm
            {
                Id = i.Id,
                Category = i.Category,
                Title = i.Title,
                IsDone = i.IsDone,
                Notes = i.Notes
            });
        }

        var canEdit = d.CanEdit;
        var isFirstArea = d.CurrentArea <= 1;
        var isLastArea = d.CurrentArea >= 4;
        var canSubmit = canEdit && isLastArea && d.Status is 1 or 2 or 6;

        LevantamientoSection.IsVisible = d.CurrentArea == 1;
        MaterialesSection.IsVisible = d.CurrentArea == 2;
        ChecklistSection.IsVisible = d.CurrentArea == 3;
        CloseSection.IsVisible = d.CurrentArea == 4;
        TechSignatureLabel.Text = "Técnico: " + (string.IsNullOrWhiteSpace(d.ClaimedBy) ? "Sin tomar" : d.ClaimedBy);

        LevantamientoEditor.IsReadOnly = !canEdit;
        MaterialesEditor.IsReadOnly = !canEdit;
        SaveButton.IsEnabled = canEdit && d.CurrentArea is 1 or 2;
        SaveButton.IsVisible = d.CurrentArea is 1 or 2;

        SaveChecklistButton.IsVisible = d.CurrentArea == 3;
        SaveChecklistButton.IsEnabled = canEdit && d.CurrentArea == 3;

        PreviousAreaButton.IsVisible = canEdit;
        NextAreaButton.IsVisible = canEdit;
        SubmitButton.IsVisible = canSubmit;

        PreviousAreaButton.IsEnabled = canEdit && !isFirstArea;
        NextAreaButton.IsEnabled = canEdit && !isLastArea;
        SubmitButton.IsEnabled = canSubmit;

        PreviousAreaButton.Text = isFirstArea ? "Primera área" : "Área anterior";
        NextAreaButton.Text = isLastArea ? "Última área" : "Siguiente área";

        var canAttachEvidence = canEdit && d.CurrentArea == 1;
        AttachEvidenceButton.IsEnabled = canAttachEvidence;
        AttachEvidenceButton.IsVisible = d.CurrentArea == 1;
        EvidenceHintLabel.Text = canAttachEvidence
            ? "Puedes subir foto o PDF."
            : "Adjunta evidencias en la etapa de levantamiento.";

        ClearSignatureButton.IsVisible = d.CurrentArea == 4;
        ClearSignatureButton.IsEnabled = canEdit && d.CurrentArea == 4;
        ClearClientSignatureButton.IsVisible = d.CurrentArea == 4;
        ClearClientSignatureButton.IsEnabled = canEdit && d.CurrentArea == 4;

        EditorCard.Opacity = canEdit ? 1 : 0.75;
        EditHintLabel.Text = canEdit
            ? (isLastArea
                ? "Última área: firma y envía a revisión."
                : "Puedes capturar datos y mover la orden por áreas.")
            : BuildReadOnlyReason(d);
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (_busy || _current == null) return;
        if (!_current.CanEdit)
        {
            await DisplayAlertAsync("Orden", BuildReadOnlyReason(_current), "OK");
            return;
        }

        _busy = true;
        try
        {
            var dto = new ServiceOrderNotesUpdateDto
            {
                LevantamientoNotes = (LevantamientoEditor.Text ?? "").Trim(),
                MaterialesNotes = (MaterialesEditor.Text ?? "").Trim()
            };
            var res = await _orders.UpdateNotesAsync(_orderId, dto);
            await DisplayAlertAsync("Orden", string.IsNullOrWhiteSpace(res.Message) ? "Guardado." : res.Message, "OK");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _busy = false;
        }
    }

    private async void OnSaveChecklistClicked(object sender, EventArgs e)
    {
        if (_busy || _current == null) return;
        if (!_current.CanEdit)
        {
            await DisplayAlertAsync("Orden", BuildReadOnlyReason(_current), "OK");
            return;
        }
        if (_current.CurrentArea != 3)
        {
            await DisplayAlertAsync("Orden", "El checklist solo se edita en Ejecución.", "OK");
            return;
        }

        _busy = true;
        try
        {
            var req = new ServiceOrderChecklistUpdateDto
            {
                Items = ChecklistItems.Select(x => new ServiceOrderChecklistUpdateItemDto
                {
                    Id = x.Id,
                    IsDone = x.IsDone,
                    Notes = x.Notes ?? string.Empty
                }).ToList()
            };

            var res = await _orders.UpdateChecklistAsync(_orderId, req);
            await DisplayAlertAsync("Orden", string.IsNullOrWhiteSpace(res.Message) ? "Checklist guardado." : res.Message, "OK");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _busy = false;
        }
    }

    private async void OnNextAreaClicked(object sender, EventArgs e)
    {
        if (_busy || _current == null) return;
        if (!_current.CanEdit)
        {
            await DisplayAlertAsync("Orden", BuildReadOnlyReason(_current), "OK");
            return;
        }
        if (_current.CurrentArea >= 4)
        {
            await DisplayAlertAsync("Orden", "Ya estás en la última área.", "OK");
            return;
        }

        _busy = true;
        try
        {
            if (_current.CurrentArea is 1 or 2)
            {
                await _orders.UpdateNotesAsync(_orderId, new ServiceOrderNotesUpdateDto
                {
                    LevantamientoNotes = (LevantamientoEditor.Text ?? "").Trim(),
                    MaterialesNotes = (MaterialesEditor.Text ?? "").Trim()
                });
            }
            else if (_current.CurrentArea == 3)
            {
                await _orders.UpdateChecklistAsync(_orderId, new ServiceOrderChecklistUpdateDto
                {
                    Items = ChecklistItems.Select(x => new ServiceOrderChecklistUpdateItemDto
                    {
                        Id = x.Id,
                        IsDone = x.IsDone,
                        Notes = x.Notes ?? string.Empty
                    }).ToList()
                });
            }

            await _orders.NextAreaAsync(_orderId);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _busy = false;
        }
    }

    private async void OnPreviousAreaClicked(object sender, EventArgs e)
    {
        if (_busy || _current == null) return;
        if (!_current.CanEdit)
        {
            await DisplayAlertAsync("Orden", BuildReadOnlyReason(_current), "OK");
            return;
        }
        if (_current.CurrentArea <= 1)
        {
            await DisplayAlertAsync("Orden", "Ya estás en la primera área.", "OK");
            return;
        }

        _busy = true;
        try
        {
            await _orders.PreviousAreaAsync(_orderId);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _busy = false;
        }
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (_busy || _current == null) return;
        if (!_current.CanEdit)
        {
            await DisplayAlertAsync("Orden", BuildReadOnlyReason(_current), "OK");
            return;
        }
        if (_current.CurrentArea != 4)
        {
            await DisplayAlertAsync("Orden", "Debes estar en Cierre técnico para firmar y enviar.", "OK");
            return;
        }

        if (!_signature.HasStrokes)
        {
            await DisplayAlertAsync("Firma requerida", "Dibuja la firma del técnico antes de enviar.", "OK");
            return;
        }
        if (!_clientSignature.HasStrokes)
        {
            await DisplayAlertAsync("Firma requerida", "Dibuja la firma del cliente antes de enviar.", "OK");
            return;
        }

        var ok = await DisplayAlertAsync("Enviar revisión", "Se enviará la orden para revisión de admin. ¿Continuar?", "Enviar", "Cancelar");
        if (!ok) return;

        _busy = true;
        try
        {
            var dataUrl = await CaptureSignatureDataUrlAsync();
            var clientDataUrl = await CaptureClientSignatureDataUrlAsync();
            var res = await _orders.SubmitAsync(_orderId, dataUrl, clientDataUrl);
            await DisplayAlertAsync("Orden", string.IsNullOrWhiteSpace(res.Message) ? "Enviada a revisión." : res.Message, "OK");
            _signature.Clear();
            _clientSignature.Clear();
            SignaturePad.Invalidate();
            ClientSignaturePad.Invalidate();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task<string> CaptureSignatureDataUrlAsync()
    {
        var capture = await SignaturePad.CaptureAsync();
        if (capture == null)
            return string.Empty;
        await using var stream = await capture.OpenReadAsync();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }

    private async Task<string> CaptureClientSignatureDataUrlAsync()
    {
        var capture = await ClientSignaturePad.CaptureAsync();
        if (capture == null)
            return string.Empty;
        await using var stream = await capture.OpenReadAsync();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }

    private void OnClearSignatureClicked(object sender, EventArgs e)
    {
        _signature.Clear();
        SignaturePad.Invalidate();
    }

    private void OnClearClientSignatureClicked(object sender, EventArgs e)
    {
        _clientSignature.Clear();
        ClientSignaturePad.Invalidate();
    }

    private void OnSignatureStart(object? sender, TouchEventArgs e)
    {
        if (_current?.CanEdit != true || _current.CurrentArea != 4)
            return;

        var p = e.Touches.FirstOrDefault();
        _signature.StartStroke(p);
        RootScroll.IsEnabled = false;
        SignaturePad.Invalidate();
    }

    private void OnSignatureDrag(object? sender, TouchEventArgs e)
    {
        if (_current?.CanEdit != true || _current.CurrentArea != 4)
            return;

        var p = e.Touches.FirstOrDefault();
        _signature.AddPoint(p);
        SignaturePad.Invalidate();
    }

    private void OnSignatureEnd(object? sender, TouchEventArgs e)
    {
        _signature.EndStroke();
        RootScroll.IsEnabled = true;
        SignaturePad.Invalidate();
    }

    private void OnClientSignatureStart(object? sender, TouchEventArgs e)
    {
        if (_current?.CanEdit != true || _current.CurrentArea != 4)
            return;

        var p = e.Touches.FirstOrDefault();
        _clientSignature.StartStroke(p);
        RootScroll.IsEnabled = false;
        ClientSignaturePad.Invalidate();
    }

    private void OnClientSignatureDrag(object? sender, TouchEventArgs e)
    {
        if (_current?.CanEdit != true || _current.CurrentArea != 4)
            return;

        var p = e.Touches.FirstOrDefault();
        _clientSignature.AddPoint(p);
        ClientSignaturePad.Invalidate();
    }

    private void OnClientSignatureEnd(object? sender, TouchEventArgs e)
    {
        _clientSignature.EndStroke();
        RootScroll.IsEnabled = true;
        ClientSignaturePad.Invalidate();
    }

    private async void OnAttachEvidenceClicked(object sender, EventArgs e)
    {
        if (_busy || _current == null) return;
        if (!_current.CanEdit)
        {
            await DisplayAlertAsync("Orden", BuildReadOnlyReason(_current), "OK");
            return;
        }

        try
        {
            var picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona evidencia (foto o PDF)",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "image/*", "application/pdf" } },
                    { DevicePlatform.iOS, new[] { "public.image", "com.adobe.pdf" } },
                    { DevicePlatform.WinUI, new[] { ".png", ".jpg", ".jpeg", ".webp", ".heic", ".pdf" } },
                    { DevicePlatform.MacCatalyst, new[] { "png", "jpg", "jpeg", "webp", "heic", "pdf" } }
                })
            });

            if (picked == null) return;

            _busy = true;
            await using var stream = await picked.OpenReadAsync();
            var res = await _orders.UploadEvidenceAsync(_orderId, stream, picked.FileName, picked.ContentType);
            await DisplayAlertAsync("Orden", string.IsNullOrWhiteSpace(res.Message) ? "Evidencia adjuntada." : res.Message, "OK");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _busy = false;
        }
    }

    private static string MapType(int val) => val switch
    {
        1 => "Correctivo",
        2 => "Preventivo",
        3 => "Nueva instalación",
        4 => "Levantamiento técnico",
        99 => "Global",
        _ => "Tipo " + val
    };

    private static string MapStatus(int val) => val switch
    {
        1 => "Creada",
        2 => "En proceso",
        3 => "En revisión",
        4 => "Finalizada",
        5 => "Pendiente firma cliente",
        6 => "Rechazada",
        _ => "Estatus " + val
    };

    private static string MapArea(int val) => val switch
    {
        1 => "Levantamiento",
        2 => "Materiales",
        3 => "Ejecución",
        4 => "Cierre técnico",
        _ => "Área " + val
    };

    private static string MapTypeBg(int val) => val switch
    {
        1 => "#FFECD8",
        2 => "#DDF8E8",
        3 => "#D9E8FF",
        4 => "#D7F2FF",
        99 => "#EBECF0",
        _ => "#EEF2F7"
    };

    private static string MapStatusBg(int val) => val switch
    {
        1 => "#EEF2FF",
        2 => "#D9E8FF",
        3 => "#FFF0D5",
        4 => "#DDF8E8",
        5 => "#FFF0D5",
        6 => "#FDE7E7",
        _ => "#EEF2F7"
    };

    private static string MapAreaBg(int val) => val switch
    {
        1 => "#D7F2FF",
        2 => "#E8F5D7",
        3 => "#E6EEFF",
        4 => "#E9E3FF",
        _ => "#EEF2F7"
    };

    private static string BuildReadOnlyReason(ServiceOrderDetailDto d)
    {
        if (d.Status is 3 or 4 or 5)
            return "La orden está en revisión/finalizada. Un admin debe rechazarla o reabrirla para seguir editando.";
        return "Solo quien tomó la orden (o admin global) puede editar.";
    }
}

public class ChecklistEditItemVm
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class SignatureDrawable : IDrawable
{
    private List<PointF> _currentStroke = new();
    public List<List<PointF>> Strokes { get; } = new();

    public bool HasStrokes => Strokes.Count > 0 || _currentStroke.Count > 1;

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
        if (_currentStroke.Count > 1)
            Strokes.Add(new List<PointF>(_currentStroke));
        _currentStroke.Clear();
    }

    public void Clear()
    {
        Strokes.Clear();
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

        foreach (var stroke in Strokes)
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
        if (stroke.Count < 2) return;
        for (var i = 1; i < stroke.Count; i++)
        {
            var p1 = stroke[i - 1];
            var p2 = stroke[i];
            canvas.DrawLine(p1.X, p1.Y, p2.X, p2.Y);
        }
    }
}
