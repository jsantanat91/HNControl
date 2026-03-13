using HNControl.Mobile.Models;
using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class ViaticWeekPage : ContentPage
{
    private readonly ViaticosService _viaticos;
    private readonly List<CategoryOption> _categories =
    [
        new(1, "Transporte"),
        new(2, "Gasolina"),
        new(3, "Material"),
        new(4, "Otros")
    ];

    private Guid _weekId;
    private Guid? _editingEntryId;
    private ViaticWeekDetailDto? _current;
    private FileResult? _selectedAttachment;
    private bool _isBusy;

    public ViaticWeekPage(ViaticosService viaticos)
    {
        InitializeComponent();
        _viaticos = viaticos;
        CategoryPicker.ItemsSource = _categories;
        CategoryPicker.SelectedIndex = 0;
        DayDatePicker.Date = DateTime.Today;
    }

    public void SetWeekId(Guid weekId)
    {
        _weekId = weekId;
        _current = null;
        _editingEntryId = null;
        ResetForm();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_weekId == Guid.Empty)
        {
            return;
        }

        await LoadWeekAsync();
    }

    private async void OnSaveEntryClicked(object sender, EventArgs e)
    {
        if (_isBusy || _current == null)
        {
            return;
        }

        var description = (DescriptionEntry.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            await DisplayAlertAsync("Viaticos", "Captura una descripcion.", "OK");
            return;
        }

        if (!decimal.TryParse(AmountEntry.Text, out var amount) || amount <= 0m)
        {
            await DisplayAlertAsync("Viaticos", "Monto invalido.", "OK");
            return;
        }

        var category = (CategoryPicker.SelectedItem as CategoryOption)?.Value ?? 1;
        var dto = new ViaticUpsertEntryDto
        {
            DayDate = (DayDatePicker.Date ?? DateTime.Today).Date,
            Category = category,
            Description = description,
            Amount = amount,
            IsBillable = IsBillableSwitch.IsToggled
        };

        _isBusy = true;
        try
        {
            if (_editingEntryId.HasValue)
            {
                await _viaticos.EditEntryAsync(_editingEntryId.Value, dto);
            }
            else
            {
                if (dto.IsBillable)
                {
                    if (_selectedAttachment == null)
                    {
                        await DisplayAlertAsync("Viaticos", "Adjunta la factura (PDF o imagen) para gasto facturable.", "OK");
                        return;
                    }

                    await using var stream = await _selectedAttachment.OpenReadAsync();
                    await _viaticos.AddEntryWithAttachmentAsync(
                        _weekId,
                        dto,
                        stream,
                        _selectedAttachment.FileName,
                        _selectedAttachment.ContentType);
                }
                else
                {
                    await _viaticos.AddEntryAsync(_weekId, dto);
                }
            }

            ResetForm();
            await LoadWeekAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void OnCancelEditClicked(object sender, EventArgs e)
    {
        ResetForm();
    }

    private void OnEditEntryClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not ViaticEntryDto entry)
        {
            return;
        }

        _editingEntryId = entry.Id;
        FormTitleLabel.Text = "Editar gasto";
        SaveEntryButton.Text = "Actualizar";
        CancelEditButton.IsVisible = true;
        DayDatePicker.Date = entry.DayDate.Date;
        CategoryPicker.SelectedItem = _categories.FirstOrDefault(x => x.Value == entry.Category) ?? _categories[0];
        DescriptionEntry.Text = entry.Description;
        AmountEntry.Text = entry.Amount.ToString("0.##");
        IsBillableSwitch.IsToggled = entry.IsBillable;
        _selectedAttachment = null;
        SelectedPdfLabel.Text = entry.HasAttachment ? "Archivo existente cargado" : "Sin archivo adjunto";
    }

    private async void OnPickAttachmentClicked(object sender, EventArgs e)
    {
        try
        {
            var picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona factura (PDF o imagen)",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/pdf", "image/*" } },
                    { DevicePlatform.iOS, new[] { "com.adobe.pdf", "public.image" } },
                    { DevicePlatform.WinUI, new[] { ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".heic" } },
                    { DevicePlatform.MacCatalyst, new[] { "pdf", "png", "jpg", "jpeg", "webp", "heic" } }
                })
            });

            if (picked == null)
            {
                return;
            }

            _selectedAttachment = picked;
            SelectedPdfLabel.Text = $"Adjunto: {_selectedAttachment.FileName}";
            IsBillableSwitch.IsToggled = true;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Adjunto", ex.Message, "OK");
        }
    }

    private async void OnDeleteEntryClicked(object sender, EventArgs e)
    {
        if (_isBusy || sender is not Button btn || btn.CommandParameter is not Guid entryId)
        {
            return;
        }

        var ok = await DisplayAlertAsync("Eliminar", "Eliminar este gasto?", "Si", "No");
        if (!ok)
        {
            return;
        }

        _isBusy = true;
        try
        {
            await _viaticos.DeleteEntryAsync(entryId);
            await LoadWeekAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async void OnSubmitWeekClicked(object sender, EventArgs e)
    {
        if (_isBusy || _current == null)
        {
            return;
        }

        if (_current.Status is 2 or 5 or 6)
        {
            await DisplayAlertAsync("Viaticos", "Este flujo ya fue enviado y no permite nuevo envio.", "OK");
            return;
        }

        var msg = _current.FlowType == 2 && _current.Status == 3
            ? "Se enviara la comprobacion final al admin."
            : "Se enviara al admin para revision.";
        var ok = await DisplayAlertAsync("Enviar", msg, "Enviar", "Cancelar");
        if (!ok)
        {
            return;
        }

        _isBusy = true;
        try
        {
            var result = await _viaticos.SubmitWeekAsync(_weekId);
            await DisplayAlertAsync("Viaticos", string.IsNullOrWhiteSpace(result.Message) ? "Semana enviada." : result.Message, "OK");
            await LoadWeekAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task LoadWeekAsync()
    {
        if (_isBusy || _weekId == Guid.Empty)
        {
            return;
        }

        _isBusy = true;
        try
        {
            _current = await _viaticos.GetWeekAsync(_weekId);

            WeekTitleLabel.Text = $"Semana {_current.WeekStartDate:yyyy-MM-dd}";
            WeekStatusLabel.Text = $"Estatus: {ToStatusLabel(_current.Status)}";
            TotalLabel.Text = _current.TotalAmount.ToString("$#,##0.00");
            BillableLabel.Text = _current.BillableAmount.ToString("$#,##0.00");
            EntriesCountLabel.Text = $"{_current.Entries.Count} registro(s)";
            EntriesCollection.ItemsSource = _current.Entries.OrderByDescending(x => x.DayDate).ToList();
            TravelInfoCard.IsVisible = _current.FlowType == 2;
            if (_current.FlowType == 2)
            {
                TravelSummaryLabel.Text = $"Destino: {_current.TripDestination} | Motivo: {_current.TripPurpose}";
                TravelRequestedLabel.Text = $"Monto solicitado: {_current.RequestedAdvanceAmount:N2}";
                TravelApprovedLabel.Text = $"Monto aprobado: {(_current.ApprovedAdvanceAmount ?? 0m):N2}";
            }

            var weekStart = _current.WeekStartDate.Date;
            DayDatePicker.MinimumDate = weekStart;
            DayDatePicker.MaximumDate = weekStart.AddDays(6);
            if (DayDatePicker.Date < DayDatePicker.MinimumDate || DayDatePicker.Date > DayDatePicker.MaximumDate)
            {
                DayDatePicker.Date = weekStart;
            }

            var canEdit = CanEditCurrent();
            FormCard.IsVisible = canEdit;
            SubmitButton.IsVisible = CanSubmitCurrent();
            SubmitButton.Text = _current.FlowType == 2 && _current.Status == 3
                ? "Enviar comprobacion"
                : _current.FlowType == 2
                    ? "Enviar solicitud al admin"
                    : "Enviar semana al admin";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void ResetForm()
    {
        _editingEntryId = null;
        FormTitleLabel.Text = "Agregar gasto";
        SaveEntryButton.Text = "Guardar";
        CancelEditButton.IsVisible = false;
        DescriptionEntry.Text = "";
        AmountEntry.Text = "";
        CategoryPicker.SelectedIndex = 0;
        IsBillableSwitch.IsToggled = false;
        _selectedAttachment = null;
        SelectedPdfLabel.Text = "Sin archivo adjunto";
    }

    private static string ToStatusLabel(int status)
    {
        return status switch
        {
            1 => "Borrador",
            2 => "Enviado",
            3 => "Aprobado",
            4 => "Rechazado",
            5 => "Comprobacion enviada",
            6 => "Comprobacion aprobada",
            _ => "Estatus " + status
        };
    }

    private bool CanEditCurrent()
    {
        if (_current == null)
        {
            return false;
        }

        if (_current.FlowType == 2)
        {
            return _current.Status is 1 or 3 or 4;
        }

        return _current.Status is 1 or 4;
    }

    private bool CanSubmitCurrent()
    {
        if (_current == null)
        {
            return false;
        }

        if (_current.FlowType == 2)
        {
            return _current.Status is 1 or 3 or 4;
        }

        return _current.Status is 1 or 4;
    }

    private sealed record CategoryOption(int Value, string Label);
}
