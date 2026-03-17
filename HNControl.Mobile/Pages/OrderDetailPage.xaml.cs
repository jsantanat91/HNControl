using System.Collections.ObjectModel;
using HNControl.Mobile.Models;
using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class OrderDetailPage : ContentPage
{
    private readonly OrdersService _orders;
    private Guid _orderId;
    private ServiceOrderDetailDto? _current;
    private bool _busy;
    private bool _descriptionStepActive = true;
    private int _lastArea = -1;
    private string? _techSigDataUrl;
    private string? _clientSigDataUrl;

    public ObservableCollection<ChecklistEditItemVm> ChecklistItems { get; } = new();

    public OrderDetailPage(OrdersService orders)
    {
        InitializeComponent();
        _orders = orders;
        ChecklistCollection.ItemsSource = ChecklistItems;
    }

    public void SetOrderId(Guid orderId)
    {
        _orderId = orderId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
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

        if (d.CurrentArea == 1)
        {
            if (_lastArea != 1)
            {
                _descriptionStepActive = true;
            }
        }
        else
        {
            _descriptionStepActive = false;
        }
        _lastArea = d.CurrentArea;

        AreaLabel.Text = d.CurrentArea == 1 && _descriptionStepActive
            ? "Descripción"
            : MapArea(d.CurrentArea);
        TypeChip.BackgroundColor = Color.FromArgb(MapTypeBg(d.Type));
        StatusChip.BackgroundColor = Color.FromArgb(MapStatusBg(d.Status));
        AreaChip.BackgroundColor = d.CurrentArea == 1 && _descriptionStepActive
            ? Color.FromArgb("#E9E3FF")
            : Color.FromArgb(MapAreaBg(d.CurrentArea));

        ClaimedByLabel.Text = "Tomada por: " + (string.IsNullOrWhiteSpace(d.ClaimedBy) ? "Sin tomar" : d.ClaimedBy);
        CreatedLabel.Text = "Creada: " + d.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        EstimatedLabel.Text = d.EstimatedEndDate.HasValue
            ? "Entrega estimada: " + d.EstimatedEndDate.Value.ToLocalTime().ToString("yyyy-MM-dd")
            : "Entrega estimada: -";

        DescriptionLabel.Text = string.IsNullOrWhiteSpace(d.Description) ? "-" : d.Description;
        DescriptionStepLabel.Text = string.IsNullOrWhiteSpace(d.Description) ? "-" : d.Description;
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
        var isDescriptionStep = d.CurrentArea == 1 && _descriptionStepActive;
        var isFirstArea = d.CurrentArea <= 1;
        var isLastArea = d.CurrentArea >= 4;
        var canSubmit = canEdit && isLastArea && d.Status is 1 or 2 or 6 && !isDescriptionStep;

        DescriptionStepSection.IsVisible = isDescriptionStep;
        LevantamientoSection.IsVisible = d.CurrentArea == 1 && !isDescriptionStep;
        MaterialesSection.IsVisible = d.CurrentArea == 2;
        ChecklistSection.IsVisible = d.CurrentArea == 3;
        CloseSection.IsVisible = d.CurrentArea == 4;
        TechSignatureLabel.Text = "Técnico: " + (string.IsNullOrWhiteSpace(d.ClaimedBy) ? "Sin tomar" : d.ClaimedBy);

        LevantamientoEditor.IsReadOnly = !canEdit;
        MaterialesEditor.IsReadOnly = !canEdit;
        SaveButton.IsEnabled = canEdit && ((d.CurrentArea == 1 && !isDescriptionStep) || d.CurrentArea == 2);
        SaveButton.IsVisible = (d.CurrentArea == 1 && !isDescriptionStep) || d.CurrentArea == 2;

        SaveChecklistButton.IsVisible = d.CurrentArea == 3;
        SaveChecklistButton.IsEnabled = canEdit && d.CurrentArea == 3;

        PreviousAreaButton.IsVisible = canEdit;
        NextAreaButton.IsVisible = canEdit;
        SubmitButton.IsVisible = canSubmit;

        PreviousAreaButton.IsEnabled = canEdit && !(isFirstArea && isDescriptionStep);
        NextAreaButton.IsEnabled = canEdit && !(isLastArea && !isDescriptionStep);
        SubmitButton.IsEnabled = canSubmit;

        PreviousAreaButton.Text = isFirstArea && isDescriptionStep ? "Primera área" : (d.CurrentArea == 1 ? "Descripción" : "Área anterior");
        NextAreaButton.Text = isDescriptionStep ? "Ir a levantamiento" : (isLastArea ? "Última área" : "Siguiente área");

        var canAttachEvidence = canEdit && d.CurrentArea == 1 && !isDescriptionStep;
        AttachEvidenceButton.IsEnabled = canAttachEvidence;
        AttachEvidenceButton.IsVisible = d.CurrentArea == 1 && !isDescriptionStep;
        EvidenceHintLabel.Text = canAttachEvidence
            ? "Puedes subir foto o PDF."
            : "Adjunta evidencias en la etapa de levantamiento.";

        SignTechButton.IsVisible = d.CurrentArea == 4;
        SignTechButton.IsEnabled = canEdit && d.CurrentArea == 4;
        SignClientButton.IsVisible = d.CurrentArea == 4;
        SignClientButton.IsEnabled = canEdit && d.CurrentArea == 4;

        TechSignStatusLabel.Text = string.IsNullOrWhiteSpace(_techSigDataUrl) ? "Pendiente" : "Firma capturada";
        TechSignStatusLabel.TextColor = string.IsNullOrWhiteSpace(_techSigDataUrl) ? Color.FromArgb("#B91C1C") : Color.FromArgb("#15803D");
        ClientSignStatusLabel.Text = string.IsNullOrWhiteSpace(_clientSigDataUrl) ? "Pendiente" : "Firma capturada";
        ClientSignStatusLabel.TextColor = string.IsNullOrWhiteSpace(_clientSigDataUrl) ? Color.FromArgb("#B91C1C") : Color.FromArgb("#15803D");

        EditorCard.Opacity = canEdit ? 1 : 0.75;
        EditHintLabel.Text = canEdit
            ? (isLastArea
                ? "Última área: captura ambas firmas y envía a revisión."
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

        if (_current.CurrentArea == 1 && _descriptionStepActive)
        {
            _descriptionStepActive = false;
            Bind(_current);
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
        if (_current.CurrentArea <= 1 && _descriptionStepActive)
        {
            await DisplayAlertAsync("Orden", "Ya estás en la primera área.", "OK");
            return;
        }

        if (_current.CurrentArea == 1 && !_descriptionStepActive)
        {
            _descriptionStepActive = true;
            Bind(_current);
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

    private async void OnSignTechClicked(object sender, EventArgs e)
    {
        if (_current?.CanEdit != true || _current.CurrentArea != 4) return;
        var sig = await SignatureCapturePage.CaptureAsync(Navigation, "Firma del técnico", TechSignatureLabel.Text);
        if (!string.IsNullOrWhiteSpace(sig))
        {
            _techSigDataUrl = sig;
            Bind(_current);
        }
    }

    private async void OnSignClientClicked(object sender, EventArgs e)
    {
        if (_current?.CanEdit != true || _current.CurrentArea != 4) return;
        var sig = await SignatureCapturePage.CaptureAsync(Navigation, "Firma del cliente", "Cliente");
        if (!string.IsNullOrWhiteSpace(sig))
        {
            _clientSigDataUrl = sig;
            Bind(_current);
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

        if (string.IsNullOrWhiteSpace(_techSigDataUrl))
        {
            await DisplayAlertAsync("Firma requerida", "Captura la firma del técnico antes de enviar.", "OK");
            return;
        }
        if (string.IsNullOrWhiteSpace(_clientSigDataUrl))
        {
            await DisplayAlertAsync("Firma requerida", "Captura la firma del cliente antes de enviar.", "OK");
            return;
        }

        var ok = await DisplayAlertAsync("Enviar revisión", "Se enviará la orden para revisión de admin. ¿Continuar?", "Enviar", "Cancelar");
        if (!ok) return;

        _busy = true;
        try
        {
            var res = await _orders.SubmitAsync(_orderId, _techSigDataUrl, _clientSigDataUrl);
            await DisplayAlertAsync("Orden", string.IsNullOrWhiteSpace(res.Message) ? "Enviada a revisión." : res.Message, "OK");
            _techSigDataUrl = null;
            _clientSigDataUrl = null;
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
