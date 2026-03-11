using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class TicketDetailPage : ContentPage
{
    private readonly TicketsService _tickets;
    private Guid _ticketId;
    private FileResult? _selectedEvidence;

    public TicketDetailPage(TicketsService tickets)
    {
        InitializeComponent();
        _tickets = tickets;
    }

    public void SetTicket(Guid id) => _ticketId = id;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_ticketId == Guid.Empty) return;
        try
        {
            var d = await _tickets.DetailAsync(_ticketId);
            TicketNumberLabel.Text = d.TicketNumber;
            TitleLabel.Text = d.Title;
            ClientLabel.Text = $"{d.Client} - {d.Contract}";
            MetaLabel.Text = $"{NormalizeStatus(d.Status)} | {NormalizePriority(d.Priority)} | SLA: {d.SlaResolutionDueAt:yyyy-MM-dd HH:mm}";
            DescriptionLabel.Text = d.Description;

            BranchLabel.Text = $"Sucursal: {Safe(d.Branch)}";
            BranchAddressLabel.Text = $"Direccion: {Safe(d.BranchAddress)}";
            CarrierLabel.Text = Safe(d.Carrier);
            CarrierServiceLabel.Text = Safe(d.CarrierService);
            CarrierAccountLabel.Text = Safe(d.CarrierAccount);
            CarrierCircuitLabel.Text = Safe(d.CarrierCircuit);
            CarrierIpLabel.Text = Safe(d.CarrierIp);

            ResolveEntry.Text = d.ResolutionSummary;
            var canOperate = !d.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase)
                             && !d.Status.Equals("Cancelado", StringComparison.OrdinalIgnoreCase)
                             && !d.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
            EvidenceCard.IsEnabled = canOperate;
            EvidenceCard.Opacity = canOperate ? 1 : 0.75;

            AttachmentsCollection.ItemsSource = d.Attachments
                .Select(a => new AttachmentVm
                {
                    Id = a.Id,
                    FileName = string.IsNullOrWhiteSpace(a.FileName) ? "adjunto" : a.FileName,
                    Meta = $"{a.UploadedAt:yyyy-MM-dd HH:mm} · {Safe(a.UploadedBy)}"
                })
                .ToList();

            EventsCollection.ItemsSource = d.Events.Select(x => new EventVm
            {
                Top = $"{x.CreatedAt:yyyy-MM-dd HH:mm} | {x.EventType} | {x.UserName}",
                Message = x.Message
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ticket", ex.Message, "OK");
        }
    }

    private async Task Run(Func<Task> action)
    {
        try
        {
            await action();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ticket", ex.Message, "OK");
        }
    }

    private async void OnTakeClicked(object sender, EventArgs e) => await Run(async () =>
    {
        var r = await _tickets.TakeAsync(_ticketId);
        await DisplayAlertAsync("Ticket", r.Message, "OK");
    });

    private async void OnStartClicked(object sender, EventArgs e) => await Run(async () =>
    {
        var r = await _tickets.StartAsync(_ticketId);
        await DisplayAlertAsync("Ticket", r.Message, "OK");
    });

    private async void OnResolveClicked(object sender, EventArgs e) => await Run(async () =>
    {
        var r = await _tickets.ResolveAsync(_ticketId, ResolveEntry.Text ?? "");
        await DisplayAlertAsync("Ticket", r.Message, "OK");
    });

    private async void OnCloseClicked(object sender, EventArgs e) => await Run(async () =>
    {
        var r = await _tickets.CloseAsync(_ticketId);
        await DisplayAlertAsync("Ticket", r.Message, "OK");
    });

    private async void OnAddNoteClicked(object sender, EventArgs e) => await Run(async () =>
    {
        var note = (NoteEditor.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            await DisplayAlertAsync("Ticket", "Escribe una nota para guardar en la bitácora.", "OK");
            return;
        }

        var r = await _tickets.AddNoteAsync(_ticketId, note);
        NoteEditor.Text = "";
        await DisplayAlertAsync("Ticket", r.Message, "OK");
    });

    private async void OnPickEvidenceClicked(object sender, EventArgs e)
    {
        try
        {
            var picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona evidencia (PDF o imagen)",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/pdf", "image/*" } },
                    { DevicePlatform.iOS, new[] { "com.adobe.pdf", "public.image" } },
                    { DevicePlatform.WinUI, new[] { ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".heic" } },
                    { DevicePlatform.MacCatalyst, new[] { "pdf", "png", "jpg", "jpeg", "webp", "heic" } }
                })
            });

            if (picked == null) return;
            _selectedEvidence = picked;
            SelectedEvidenceLabel.Text = $"Seleccionado: {_selectedEvidence.FileName}";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Evidencia", ex.Message, "OK");
        }
    }

    private async void OnCaptureEvidenceClicked(object sender, EventArgs e)
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlertAsync("Evidencia", "Este dispositivo no soporta captura de cámara en la app.", "OK");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo == null) return;
            _selectedEvidence = photo;
            SelectedEvidenceLabel.Text = $"Foto: {_selectedEvidence.FileName}";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Evidencia", ex.Message, "OK");
        }
    }

    private async void OnUploadEvidenceClicked(object sender, EventArgs e) => await Run(async () =>
    {
        if (_selectedEvidence == null)
        {
            await DisplayAlertAsync("Ticket", "Selecciona una evidencia (foto/PDF) antes de subir.", "OK");
            return;
        }

        await using var fileStream = await _selectedEvidence.OpenReadAsync();
        var note = (NoteEditor.Text ?? "").Trim();
        var r = await _tickets.AddEvidenceAsync(_ticketId, note, fileStream, _selectedEvidence.FileName, _selectedEvidence.ContentType);
        _selectedEvidence = null;
        NoteEditor.Text = "";
        SelectedEvidenceLabel.Text = "Sin evidencia seleccionada";
        await DisplayAlertAsync("Ticket", r.Message, "OK");
    });

    private async void OnOpenAttachmentClicked(object sender, EventArgs e)
    {
        if (sender is not Button b || b.CommandParameter is not Guid attachmentId) return;
        try
        {
            var detail = await _tickets.DetailAsync(_ticketId);
            var att = detail.Attachments.FirstOrDefault(x => x.Id == attachmentId);
            var fileName = att?.FileName;
            var bytes = await _tickets.DownloadAttachmentAsync(attachmentId);
            fileName = string.IsNullOrWhiteSpace(fileName) ? $"ticket_{attachmentId:N}.bin" : fileName;
            var path = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(path, bytes);
            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                File = new ReadOnlyFile(path)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Adjunto", ex.Message, "OK");
        }
    }

    private sealed class EventVm
    {
        public string Top { get; set; } = "";
        public string Message { get; set; } = "";
    }

    private sealed class AttachmentVm
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = "";
        public string Meta { get; set; } = "";
    }

    private static string Safe(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static string NormalizePriority(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        return value.Equals("Urge", StringComparison.OrdinalIgnoreCase)
            ? "Urgente"
            : value;
    }

    private static string NormalizeStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        return value switch
        {
            "New" => "Nuevo",
            "Assigned" => "Asignado",
            "InProgress" => "En proceso",
            "PendingCustomer" => "Pendiente cliente",
            "Resolved" => "Resuelto",
            "Closed" => "Cerrado",
            "Cancelled" => "Cancelado",
            _ => value
        };
    }
}
