using HNControl.Mobile.Models;
using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class OrderDetailPage : ContentPage
{
    private readonly OrdersService _orders;
    private Guid _orderId;
    private ServiceOrderDetailDto? _current;
    private bool _busy;

    public OrderDetailPage(OrdersService orders)
    {
        InitializeComponent();
        _orders = orders;
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
        EvidenceCollection.ItemsSource = d.Evidences.Select(e => $"{e.UploadedAtLocal} Â· {e.OriginalFileName}").ToList();

        var canEdit = d.CanEdit;
        LevantamientoEditor.IsReadOnly = !canEdit;
        MaterialesEditor.IsReadOnly = !canEdit;
        SaveButton.IsEnabled = canEdit;
        var isFirstArea = d.CurrentArea <= 1;
        var isLastArea = d.CurrentArea >= 4;
        var canSubmit = canEdit && isLastArea && d.Status is 1 or 2 or 6;

        PreviousAreaButton.IsVisible = canEdit;
        NextAreaButton.IsVisible = canEdit;
        SubmitButton.IsVisible = canSubmit;

        PreviousAreaButton.IsEnabled = canEdit && !isFirstArea;
        NextAreaButton.IsEnabled = canEdit && !isLastArea;
        SubmitButton.IsEnabled = canSubmit;
        PreviousAreaButton.Text = isFirstArea ? "Primera Ã¡rea" : "Ãrea anterior";
        NextAreaButton.Text = isLastArea ? "Ãšltima Ã¡rea" : "Siguiente Ã¡rea";
        var canAttachEvidence = canEdit;
        AttachEvidenceButton.IsEnabled = canAttachEvidence;
        AttachEvidenceButton.IsVisible = canEdit;
        EvidenceHintLabel.Text = canAttachEvidence
            ? "Puedes subir foto o PDF."
            : "Solo quien tomó la orden (o admin global) puede adjuntar evidencias.";
        EditorCard.Opacity = canEdit ? 1 : 0.7;
        EditHintLabel.Text = canEdit
            ? (isLastArea
                ? "Ãšltima Ã¡rea: guarda datos y cuando termines, envÃ­a a revisiÃ³n."
                : "Puedes capturar datos y mover la orden por Ã¡reas.")
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
        if (_current.CurrentArea >= 4)
        {
            await DisplayAlertAsync("Orden", "Ya estÃ¡s en Cierre tÃ©cnico. Usa 'Enviar revisiÃ³n (final)' cuando termines.", "OK");
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
            await _orders.UpdateNotesAsync(_orderId, new ServiceOrderNotesUpdateDto
            {
                LevantamientoNotes = (LevantamientoEditor.Text ?? "").Trim(),
                MaterialesNotes = (MaterialesEditor.Text ?? "").Trim()
            });
            var res = await _orders.NextAreaAsync(_orderId);
            await DisplayAlertAsync("Orden", string.IsNullOrWhiteSpace(res.Message) ? "Ãrea actualizada." : res.Message, "OK");
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
            await _orders.UpdateNotesAsync(_orderId, new ServiceOrderNotesUpdateDto
            {
                LevantamientoNotes = (LevantamientoEditor.Text ?? "").Trim(),
                MaterialesNotes = (MaterialesEditor.Text ?? "").Trim()
            });
            var res = await _orders.PreviousAreaAsync(_orderId);
            await DisplayAlertAsync("Orden", string.IsNullOrWhiteSpace(res.Message) ? "Ãrea actualizada." : res.Message, "OK");
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

        var ok = await DisplayAlertAsync("Enviar revisiÃ³n", "Se enviarÃ¡ la orden para revisiÃ³n de admin. Â¿Continuar?", "Enviar", "Cancelar");
        if (!ok) return;

        _busy = true;
        try
        {
            await _orders.UpdateNotesAsync(_orderId, new ServiceOrderNotesUpdateDto
            {
                LevantamientoNotes = (LevantamientoEditor.Text ?? "").Trim(),
                MaterialesNotes = (MaterialesEditor.Text ?? "").Trim()
            });
            var res = await _orders.SubmitAsync(_orderId);
            await DisplayAlertAsync("Orden", string.IsNullOrWhiteSpace(res.Message) ? "Enviada a revisiÃ³n." : res.Message, "OK");
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
        3 => "Nueva instalaciÃ³n",
        4 => "Levantamiento tÃ©cnico",
        99 => "Global",
        _ => "Tipo " + val
    };

    private static string MapStatus(int val) => val switch
    {
        1 => "Creada",
        2 => "En proceso",
        3 => "En revisiÃ³n",
        4 => "Finalizada",
        5 => "Pendiente firma cliente",
        6 => "Rechazada",
        _ => "Estatus " + val
    };

    private static string MapArea(int val) => val switch
    {
        1 => "Levantamiento",
        2 => "Materiales",
        3 => "EjecuciÃ³n",
        4 => "Cierre tÃ©cnico",
        _ => "Ãrea " + val
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
            return "La orden estÃ¡ en revisiÃ³n/finalizada. Un admin debe rechazarla o reabrirla para seguir editando.";
        return "Solo quien tomÃ³ la orden (o admin global) puede editar.";
    }
}

