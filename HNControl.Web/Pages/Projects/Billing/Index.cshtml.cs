using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects.Billing;

[Authorize(Policy = "EmployeeOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IEmailSender _email;
    private readonly IBillingInvoicePdfRenderer _pdf;
    private readonly IBillingFiscalService _fiscal;
    private readonly IEventEmailTemplateService _templates;

    public IndexModel(
        ApplicationDbContext db,
        IFileStorage storage,
        IEmailSender email,
        IBillingInvoicePdfRenderer pdf,
        IBillingFiscalService fiscal,
        IEventEmailTemplateService templates)
    {
        _db = db;
        _storage = storage;
        _email = email;
        _pdf = pdf;
        _fiscal = fiscal;
        _templates = templates;
    }

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public List<LineInputModel> InputLines { get; set; } = [new()];
    [BindProperty(SupportsGet = true)] public int PlansPage { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int RunsPage { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int AuditPage { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string? OpenModal { get; set; }
    public int PlansTotalPages { get; set; } = 1;
    public int RunsTotalPages { get; set; } = 1;
    public int AuditsTotalPages { get; set; } = 1;

    public SelectList ClientItems { get; set; } = default!;
    public SelectList OpportunityItems { get; set; } = default!;
    public SelectList QuoteItems { get; set; } = default!;
    public SelectList ContractItems { get; set; } = default!;
    public SelectList PeriodicityItems { get; set; } = default!;

    public List<PlanVm> Plans { get; set; } = new();
    public List<RunVm> Runs { get; set; } = new();
    public List<RunVm> EmittedRuns { get; set; } = new();
    public List<AuditVm> Audits { get; set; } = new();

    public int ActivePlans { get; set; }
    public int PendingRuns { get; set; }
    public decimal MonthlyProjection { get; set; }
    public bool BillingSchemaReady { get; set; } = true;
    public string? BillingSchemaMessage { get; set; }

    public record PlanLineVm(
        string Category,
        string Concept,
        int Quantity,
        decimal UnitPrice,
        decimal Subtotal,
        decimal VatAmount,
        decimal Total);

    public record PlanVm(
        Guid Id,
        string Client,
        string Concept,
        decimal Total,
        string Periodicity,
        string Status,
        DateTime StartDate,
        DateTime NextRunDate,
        DateTime InvoiceIssueDate,
        string SendToEmail,
        string SatSummary,
        string CcEmails,
        string Notes,
        string? ContractName,
        int ContractMonths,
        int LinesCount,
        Guid? LatestPdfRunId,
        IReadOnlyList<PlanLineVm> Lines);

    public record RunVm(
        Guid Id,
        Guid PlanId,
        string Client,
        string Concept,
        string PeriodLabel,
        DateTime ScheduledFor,
        string Status,
        string Email,
        bool HasPdf,
        string CfdiUuid,
        string CfdiStatus,
        string SatStatusMessage,
        bool IsPaid,
        DateTime? PaidAt,
        string PaymentFormCode,
        string PaymentMethodCode);

    public record AuditVm(DateTime CreatedAt, string Client, string EventType, string Details, string UserName);

    public class InputModel
    {
        [Required] public Guid ClientId { get; set; }
        public Guid? SalesOpportunityId { get; set; }
        public Guid? QuoteRequestId { get; set; }
        public Guid? ContractId { get; set; }

        [Required, MaxLength(220)]
        public string Concept { get; set; } = "Servicio mensual";

        [Range(0, 999999999)] public decimal Subtotal { get; set; } = 0m;
        [Range(0, 1)] public decimal VatRate { get; set; } = 0.16m;

        [Required] public BillingPeriodicity Periodicity { get; set; } = BillingPeriodicity.Monthly;
        [Required] public DateTime StartDate { get; set; } = DateTime.Today;
        [Required] public DateTime InvoiceIssueDate { get; set; } = DateTime.Today;
        [Range(1, 120)] public int? ContractMonths { get; set; } = 12;

        [Required, EmailAddress, MaxLength(256)]
        public string SendToEmail { get; set; } = "";

        [MaxLength(600)] public string CcEmails { get; set; } = "";
        [MaxLength(2000)] public string Notes { get; set; } = "";

        [Required] public BillingInvoiceType InvoiceType { get; set; } = BillingInvoiceType.Ingreso;

        [Required, MaxLength(4)] public string CfdiUseCode { get; set; } = "G03";
        [Required, MaxLength(4)] public string FiscalRegimeCode { get; set; } = "601";
        [Required, MaxLength(4)] public string PaymentMethodCode { get; set; } = "PUE";
        [Required, MaxLength(4)] public string PaymentFormCode { get; set; } = "03";
    }

    public class LineInputModel
    {
        [MaxLength(80)] public string Category { get; set; } = "";
        [Required, MaxLength(220)] public string Concept { get; set; } = "";
        [Range(1, 9999)] public int Quantity { get; set; } = 1;
        [Range(0, 99999999)] public decimal UnitPrice { get; set; } = 0;
        [Range(0, 1)] public decimal VatRate { get; set; } = 0.16m;
    }

    public class PaymentInputModel
    {
        [Required] public Guid RunId { get; set; }
        [Required] public DateTime PaidAt { get; set; } = DateTime.Today;
        [Required, MaxLength(4)] public string PaymentFormCode { get; set; } = "03";
        [Required, MaxLength(4)] public string PaymentMethodCode { get; set; } = "PUE";
        [MaxLength(1200)] public string Notes { get; set; } = "";
    }

    [BindProperty] public PaymentInputModel PaymentInput { get; set; } = new();

    public async Task OnGetAsync(Guid? opportunityId = null, Guid? quoteId = null, Guid? contractId = null)
    {
        if (opportunityId.HasValue) Input.SalesOpportunityId = opportunityId;
        if (quoteId.HasValue) Input.QuoteRequestId = quoteId;
        if (contractId.HasValue) Input.ContractId = contractId;
        await PrefillFromOriginAsync();
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreatePlanAsync()
    {
        if (!await CheckBillingSchemaReadyAsync())
        {
            Flash = BillingSchemaMessage ?? "Falta actualizar esquema de facturación.";
            FlashType = "warning";
            await LoadAsync();
            return Page();
        }

        if (!ModelState.IsValid)
        {
            OpenModal = "newPlan";
            await LoadAsync();
            return Page();
        }

        ClientServiceContract? contract = null;
        if (Input.ContractId.HasValue)
        {
            contract = await _db.ClientServiceContracts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Input.ContractId.Value);
            if (contract == null)
            {
                Flash = "Contrato no encontrado.";
                FlashType = "danger";
                await LoadAsync();
                return Page();
            }

            Input.ClientId = contract.ClientId;
        }

        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Input.ClientId);
        if (client == null)
        {
            Flash = "Cliente no encontrado.";
            FlashType = "danger";
            await LoadAsync();
            return Page();
        }

        var sourceLines = (InputLines ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Concept) && x.Quantity > 0)
            .ToList();

        if (sourceLines.Count == 0)
        {
            sourceLines.Add(new LineInputModel
            {
                Category = "Servicio",
                Concept = string.IsNullOrWhiteSpace(Input.Concept) ? "Servicio mensual" : Input.Concept.Trim(),
                Quantity = 1,
                UnitPrice = Math.Max(0.01m, Input.Subtotal),
                VatRate = Math.Clamp(Input.VatRate, 0m, 1m)
            });
        }

        var billingLines = new List<BillingInvoiceLine>();
        var sortOrder = 1;
        foreach (var line in sourceLines)
        {
            var unitPrice = Math.Round(Math.Max(0m, line.UnitPrice), 2);
            var qty = Math.Max(1, line.Quantity);
            var lineSubtotal = Math.Round(unitPrice * qty, 2);
            var lineVatRate = Math.Clamp(line.VatRate, 0m, 1m);
            var lineVat = Math.Round(lineSubtotal * lineVatRate, 2);
            var lineTotal = Math.Round(lineSubtotal + lineVat, 2);

            billingLines.Add(new BillingInvoiceLine
            {
                Category = string.IsNullOrWhiteSpace(line.Category) ? "Servicio" : line.Category.Trim(),
                Concept = line.Concept.Trim(),
                Quantity = qty,
                UnitPrice = unitPrice,
                Subtotal = lineSubtotal,
                VatRate = lineVatRate,
                VatAmount = lineVat,
                Total = lineTotal,
                SortOrder = sortOrder++,
                CreatedAt = DateTime.UtcNow
            });
        }

        var subtotal = billingLines.Sum(x => x.Subtotal);
        var vat = billingLines.Sum(x => x.VatAmount);
        var total = billingLines.Sum(x => x.Total);

        var plan = new BillingInvoicePlan
        {
            ClientId = client.Id,
            QuoteRequestId = Input.QuoteRequestId,
            SalesOpportunityId = Input.SalesOpportunityId,
            ClientServiceContractId = Input.ContractId,
            Concept = (Input.Concept ?? "").Trim(),
            Currency = "MXN",
            Subtotal = subtotal,
            VatRate = subtotal > 0 ? vat / subtotal : Math.Clamp(Input.VatRate, 0m, 1m),
            VatAmount = vat,
            Total = total,
            InvoiceType = Input.InvoiceType,
            CfdiUseCode = (Input.CfdiUseCode ?? "G03").Trim().ToUpperInvariant(),
            FiscalRegimeCode = (Input.FiscalRegimeCode ?? "601").Trim().ToUpperInvariant(),
            PaymentMethodCode = (Input.PaymentMethodCode ?? "PUE").Trim().ToUpperInvariant(),
            PaymentFormCode = (Input.PaymentFormCode ?? "03").Trim().ToUpperInvariant(),
            Periodicity = Input.Periodicity,
            StartDate = Input.StartDate.Date,
            InvoiceIssueDate = Input.InvoiceIssueDate.Date,
            NextRunDate = Input.InvoiceIssueDate.Date,
            RemainingRuns = Input.ContractMonths.HasValue && Input.ContractMonths.Value > 0 ? Input.ContractMonths.Value : null,
            SendToEmail = (Input.SendToEmail ?? "").Trim(),
            CcEmails = (Input.CcEmails ?? "").Trim(),
            Notes = (Input.Notes ?? "").Trim(),
            Status = BillingPlanStatus.Active,
            CreatedByUserId = User.Identity?.Name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Lines = billingLines
        };

        _db.BillingInvoicePlans.Add(plan);
        await _db.SaveChangesAsync();

        _db.BillingInvoiceRuns.Add(new BillingInvoiceRun
        {
            PlanId = plan.Id,
            PeriodLabel = BuildPeriodLabel(plan.NextRunDate, plan.Periodicity),
            ScheduledFor = plan.NextRunDate,
            Status = BillingRunStatus.Scheduled,
            SentToEmail = plan.SendToEmail,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await AddBillingAuditAsync(plan.Id, "billing.plan.create", $"Plan creado por {plan.Total:C2}, periodicidad {plan.Periodicity}.");

        Flash = "Plan de facturación creado.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMarkSentAsync(Guid planId)
    {
        if (!await CheckBillingSchemaReadyAsync())
        {
            Flash = BillingSchemaMessage ?? "Falta actualizar esquema de facturación.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var plan = await _db.BillingInvoicePlans
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == planId);

        if (plan == null) return RedirectToPage();
        if (plan.Status != BillingPlanStatus.Active)
        {
            Flash = "El plan no está activo.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var run = await _db.BillingInvoiceRuns
            .Where(x => x.PlanId == plan.Id && x.Status == BillingRunStatus.Scheduled)
            .OrderBy(x => x.ScheduledFor)
            .FirstOrDefaultAsync();

        if (run == null)
        {
            run = new BillingInvoiceRun
            {
                PlanId = plan.Id,
                PeriodLabel = BuildPeriodLabel(plan.NextRunDate, plan.Periodicity),
                ScheduledFor = plan.NextRunDate,
                Status = BillingRunStatus.Scheduled,
                SentToEmail = plan.SendToEmail,
                CreatedAt = DateTime.UtcNow
            };
            _db.BillingInvoiceRuns.Add(run);
            await _db.SaveChangesAsync();
        }

        var pdfBytes = await _pdf.RenderAsync(plan, run);
        var fileName = $"factura_simulada_{plan.ClientId:N}_{run.ScheduledFor:yyyyMMdd}.pdf";
        var (storagePath, _, _) = await _storage.SaveBytesAsync(pdfBytes, "billing", fileName, "application/pdf");

        var (subject, body) = await _templates.RenderAsync(
            "billing.invoice.scheduled",
            $"Estado de facturación {plan.Client?.Name} - {run.PeriodLabel}",
            $@"<p>Hola,</p>
<p>Compartimos el estado de cuenta programado del periodo <b>{run.PeriodLabel}</b>.</p>
<p>Concepto: <b>{plan.Concept}</b><br/>Total: <b>{plan.Total:C2}</b></p>
<p>Este documento es simulacion de facturación interna (sin timbrado SAT).</p>",
            new Dictionary<string, string>
            {
                ["Cliente"] = plan.Client?.Name ?? "-",
                ["Periodo"] = run.PeriodLabel,
                ["Concepto"] = plan.Concept,
                ["Monto"] = plan.Total.ToString("C2")
            });

        await _email.SendAsync(plan.SendToEmail, subject, body, pdfBytes, fileName, "application/pdf");

        foreach (var cc in ParseEmails(plan.CcEmails))
            await _email.SendAsync(cc, subject, body, pdfBytes, fileName, "application/pdf");

        run.Status = BillingRunStatus.Sent;
        run.SentAt = DateTime.UtcNow;
        run.PdfStoragePath = storagePath;
        run.SentToEmail = plan.SendToEmail;

        var sync = await _fiscal.SyncAsync(plan, run);
        run.CfdiStatus = sync.Status;
        if (!string.IsNullOrWhiteSpace(sync.CfdiUuid)) run.CfdiUuid = sync.CfdiUuid;
        run.PacTrackingId = sync.TrackingId;
        run.LastSyncAt = DateTime.UtcNow;
        run.SatStatusMessage = sync.Message;

        plan.LastSentAt = DateTime.UtcNow;
        plan.UpdatedAt = DateTime.UtcNow;

        AdvancePlan(plan);

        await _db.SaveChangesAsync();
        await AddBillingAuditAsync(plan.Id, "billing.run.sent", $"Enviado {run.PeriodLabel} a {plan.SendToEmail}.");
        Flash = "Factura enviada y ciclo actualizado.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSyncCfdiAsync(Guid runId)
    {
        if (!await CheckBillingSchemaReadyAsync())
        {
            Flash = BillingSchemaMessage ?? "Falta actualizar esquema de facturación.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var run = await _db.BillingInvoiceRuns
            .Include(x => x.Plan!)
            .ThenInclude(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == runId);

        if (run == null || run.Plan == null)
            return RedirectToPage();

        if (run.Status != BillingRunStatus.Sent)
        {
            Flash = "Primero marca el envío para poder sincronizar CFDI.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var result = await _fiscal.SyncAsync(run.Plan, run);
        run.CfdiStatus = result.Status;
        run.CfdiUuid = result.CfdiUuid;
        run.PacTrackingId = result.TrackingId;
        run.LastSyncAt = DateTime.UtcNow;
        run.SatStatusMessage = result.Message;

        await _db.SaveChangesAsync();
        await AddBillingAuditAsync(run.PlanId, "billing.cfdi.sync", $"Sync CFDI: {result.Message}");

        Flash = result.Message;
        FlashType = result.Ok ? "success" : "warning";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCancelCfdiAsync(Guid runId, string? reasonCode)
    {
        if (!await CheckBillingSchemaReadyAsync())
        {
            Flash = BillingSchemaMessage ?? "Falta actualizar esquema de facturación.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var run = await _db.BillingInvoiceRuns
            .Include(x => x.Plan!)
            .ThenInclude(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == runId);

        if (run == null || run.Plan == null)
            return RedirectToPage();

        if (run.Status != BillingRunStatus.Sent)
        {
            Flash = "Solo puedes cancelar CFDI de corridas enviadas.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var reason = string.IsNullOrWhiteSpace(reasonCode) ? "02" : reasonCode.Trim();
        var result = await _fiscal.CancelAsync(run.Plan, run, reason);

        if (result.Ok)
        {
            run.CfdiStatus = BillingCfdiStatus.Cancelled;
            run.CancelReasonCode = reason;
            run.CancellationRequestedAt = DateTime.UtcNow;
            run.CancelledAt = DateTime.UtcNow;
            run.LastSyncAt = DateTime.UtcNow;
            run.SatStatusMessage = result.Message;
            run.PacTrackingId = result.TrackingId;
            run.Status = BillingRunStatus.Cancelled;
        }
        else
        {
            run.SatStatusMessage = result.Message;
        }

        await _db.SaveChangesAsync();
        await AddBillingAuditAsync(run.PlanId, "billing.cfdi.cancel", $"Cancelación CFDI: {result.Message}");

        Flash = result.Message;
        FlashType = result.Ok ? "success" : "warning";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMarkPaidAsync()
    {
        if (!await CheckBillingSchemaReadyAsync())
        {
            Flash = BillingSchemaMessage ?? "Falta actualizar esquema de facturación.";
            FlashType = "warning";
            return RedirectToPage(new { openModal = "emitted" });
        }

        if (!ModelState.IsValid)
        {
            Flash = "Datos de pago incompletos.";
            FlashType = "danger";
            return RedirectToPage(new { openModal = "emitted" });
        }

        var run = await _db.BillingInvoiceRuns
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == PaymentInput.RunId);

        if (run == null || run.Plan == null)
            return RedirectToPage(new { openModal = "emitted" });

        run.IsPaid = true;
        run.PaidAt = PaymentInput.PaidAt.Date;
        run.PaymentFormCode = (PaymentInput.PaymentFormCode ?? "03").Trim().ToUpperInvariant();
        run.PaymentMethodCode = (PaymentInput.PaymentMethodCode ?? "PUE").Trim().ToUpperInvariant();
        run.PaymentNotes = (PaymentInput.Notes ?? "").Trim();

        await _db.SaveChangesAsync();
        await AddBillingAuditAsync(run.PlanId, "billing.run.paid",
            $"Factura {run.PeriodLabel} pagada {run.PaidAt:yyyy-MM-dd} · forma {run.PaymentFormCode} · método {run.PaymentMethodCode}.");

        Flash = "Factura marcada como pagada.";
        FlashType = "success";
        return RedirectToPage(new { openModal = "emitted" });
    }

    public async Task<IActionResult> OnPostToggleStatusAsync(Guid planId)
    {
        if (!await CheckBillingSchemaReadyAsync())
        {
            Flash = BillingSchemaMessage ?? "Falta actualizar esquema de facturación.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var plan = await _db.BillingInvoicePlans.FirstOrDefaultAsync(x => x.Id == planId);
        if (plan == null) return RedirectToPage();

        plan.Status = plan.Status switch
        {
            BillingPlanStatus.Active => BillingPlanStatus.Paused,
            BillingPlanStatus.Paused => BillingPlanStatus.Active,
            _ => plan.Status
        };
        plan.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await AddBillingAuditAsync(plan.Id, "billing.plan.toggle", $"Plan cambiado a {plan.Status}.");
        Flash = plan.Status == BillingPlanStatus.Active ? "Plan reactivado." : "Plan pausado.";
        FlashType = "info";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdatePlanAsync(
        Guid planId,
        string? concept,
        BillingPeriodicity periodicity,
        DateTime startDate,
        DateTime invoiceIssueDate,
        int? contractMonths,
        string? sendToEmail,
        string? ccEmails,
        string? notes,
        string? linesJson)
    {
        if (!await CheckBillingSchemaReadyAsync())
        {
            Flash = BillingSchemaMessage ?? "Falta actualizar esquema de facturación.";
            FlashType = "warning";
            return RedirectToPage(new { openModal = "newPlan" });
        }

        var plan = await _db.BillingInvoicePlans
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == planId);
        if (plan == null)
        {
            Flash = "Plan no encontrado.";
            FlashType = "danger";
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(sendToEmail))
        {
            Flash = "Correo destino es obligatorio.";
            FlashType = "warning";
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(linesJson))
        {
            Flash = "Debes capturar al menos una línea.";
            FlashType = "warning";
            return RedirectToPage();
        }

        List<LineInputModel>? incomingLines;
        try
        {
            incomingLines = global::System.Text.Json.JsonSerializer.Deserialize<List<LineInputModel>>(
                linesJson,
                new global::System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            Flash = "Formato inválido en líneas del plan.";
            FlashType = "warning";
            return RedirectToPage();
        }

        incomingLines ??= [];
        var sourceLines = incomingLines
            .Where(x => !string.IsNullOrWhiteSpace(x.Concept) && x.Quantity > 0)
            .ToList();
        if (sourceLines.Count == 0)
        {
            Flash = "Debes capturar al menos una línea válida.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var rebuiltLines = new List<BillingInvoiceLine>();
        var sortOrder = 1;
        foreach (var line in sourceLines)
        {
            var unitPrice = Math.Round(Math.Max(0m, line.UnitPrice), 2);
            var qty = Math.Max(1, line.Quantity);
            var lineSubtotal = Math.Round(unitPrice * qty, 2);
            var lineVatRate = Math.Clamp(line.VatRate, 0m, 1m);
            var lineVat = Math.Round(lineSubtotal * lineVatRate, 2);
            var lineTotal = Math.Round(lineSubtotal + lineVat, 2);

            rebuiltLines.Add(new BillingInvoiceLine
            {
                Category = string.IsNullOrWhiteSpace(line.Category) ? "Servicio" : line.Category.Trim(),
                Concept = line.Concept.Trim(),
                Quantity = qty,
                UnitPrice = unitPrice,
                Subtotal = lineSubtotal,
                VatRate = lineVatRate,
                VatAmount = lineVat,
                Total = lineTotal,
                SortOrder = sortOrder++,
                CreatedAt = DateTime.UtcNow
            });
        }

        var subtotal = rebuiltLines.Sum(x => x.Subtotal);
        var vat = rebuiltLines.Sum(x => x.VatAmount);
        var total = rebuiltLines.Sum(x => x.Total);

        var existingLines = plan.Lines.ToList();
        _db.RemoveRange(existingLines);
        plan.Lines.Clear();
        foreach (var rebuiltLine in rebuiltLines)
            plan.Lines.Add(rebuiltLine);

        plan.Concept = string.IsNullOrWhiteSpace(concept) ? plan.Concept : concept.Trim();
        plan.Periodicity = periodicity;
        plan.StartDate = startDate.Date;
        plan.InvoiceIssueDate = invoiceIssueDate.Date;
        plan.RemainingRuns = contractMonths.HasValue && contractMonths.Value > 0 ? contractMonths.Value : null;
        plan.SendToEmail = sendToEmail.Trim();
        plan.CcEmails = (ccEmails ?? "").Trim();
        plan.Notes = (notes ?? "").Trim();
        plan.Subtotal = subtotal;
        plan.VatAmount = vat;
        plan.Total = total;
        plan.VatRate = subtotal > 0 ? vat / subtotal : plan.VatRate;
        plan.UpdatedAt = DateTime.UtcNow;

        if (plan.NextRunDate < DateTime.Today)
            plan.NextRunDate = invoiceIssueDate.Date;

        await _db.SaveChangesAsync();
        await AddBillingAuditAsync(plan.Id, "billing.plan.update", "Plan actualizado desde dashboard.");

        Flash = "Plan actualizado correctamente.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeletePlanAsync(Guid planId)
    {
        if (!await CheckBillingSchemaReadyAsync())
        {
            Flash = BillingSchemaMessage ?? "Falta actualizar esquema de facturación.";
            FlashType = "warning";
            return RedirectToPage();
        }

        var plan = await _db.BillingInvoicePlans.FirstOrDefaultAsync(x => x.Id == planId);
        if (plan == null) return RedirectToPage();

        _db.BillingInvoicePlans.Remove(plan);
        await _db.SaveChangesAsync();
        Flash = "Plan eliminado.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetDownloadPdfAsync(Guid runId)
    {
        if (!await CheckBillingSchemaReadyAsync())
            return RedirectToPage();

        var run = await _db.BillingInvoiceRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == runId);
        if (run == null || string.IsNullOrWhiteSpace(run.PdfStoragePath)) return NotFound();
        var (stream, contentType, downloadName) = await _storage.OpenAsync(run.PdfStoragePath, $"factura_{run.ScheduledFor:yyyyMMdd}.pdf");
        return File(stream, contentType, downloadName);
    }

    public async Task<IActionResult> OnGetDownloadLatestPdfAsync(Guid planId)
    {
        if (!await CheckBillingSchemaReadyAsync())
            return RedirectToPage();

        var run = await _db.BillingInvoiceRuns
            .AsNoTracking()
            .Where(x => x.PlanId == planId && x.PdfStoragePath != null && x.PdfStoragePath != "")
            .OrderByDescending(x => x.ScheduledFor)
            .FirstOrDefaultAsync();

        if (run == null || string.IsNullOrWhiteSpace(run.PdfStoragePath)) return NotFound();
        var (stream, contentType, downloadName) = await _storage.OpenAsync(run.PdfStoragePath, $"factura_{run.ScheduledFor:yyyyMMdd}.pdf");
        return File(stream, contentType, downloadName);
    }

    private async Task LoadAsync()
    {
        const int cardPageSize = 12;
        const int historyPageSize = 20;
        PlansPage = Math.Max(1, PlansPage);
        RunsPage = Math.Max(1, RunsPage);
        AuditPage = Math.Max(1, AuditPage);

        var clients = await _db.Clients
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsTemporaryLead)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, Label = x.ClientCode + " · " + x.Name })
            .ToListAsync();
        ClientItems = new SelectList(clients, "Id", "Label", Input.ClientId);

        var opportunities = await _db.SalesOpportunities
            .AsNoTracking()
            .Include(x => x.QuoteRequest)
            .Where(x => x.Status == SalesOpportunityStatus.ClosedWon || x.Status == SalesOpportunityStatus.ContractSigned || x.Status == SalesOpportunityStatus.CommissionApplied)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new { x.Id, Label = (x.QuoteRequest != null ? x.QuoteRequest.Folio : "-") + " · " + (x.QuoteRequest != null ? x.QuoteRequest.CustomerName : "-") })
            .ToListAsync();
        OpportunityItems = new SelectList(opportunities, "Id", "Label", Input.SalesOpportunityId);

        var quotes = await _db.QuoteRequests
            .AsNoTracking()
            .Where(x => x.Status == QuoteRequestStatus.Accepted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(300)
            .Select(x => new { x.Id, Label = x.Folio + " · " + x.CustomerName })
            .ToListAsync();
        QuoteItems = new SelectList(quotes, "Id", "Label", Input.QuoteRequestId);

        var contracts = await _db.ClientServiceContracts
            .AsNoTracking()
            .Include(x => x.Client)
            .OrderByDescending(x => x.CreatedAt)
            .Take(300)
            .Select(x => new
            {
                x.Id,
                Label = (x.Client != null ? x.Client.Name : "-") + " · " + (string.IsNullOrWhiteSpace(x.Label) ? x.ContractNumber : x.Label)
            })
            .ToListAsync();
        ContractItems = new SelectList(contracts, "Id", "Label", Input.ContractId);
        PeriodicityItems = new SelectList(
            Enum.GetValues<BillingPeriodicity>()
                .Select(x => new { Value = x, Label = LabelPeriodicity(x) }),
            "Value",
            "Label",
            Input.Periodicity);

        if (!await CheckBillingSchemaReadyAsync())
        {
            Plans = [];
            Runs = [];
            EmittedRuns = [];
            Audits = [];
            ActivePlans = 0;
            PendingRuns = 0;
            MonthlyProjection = 0m;
            return;
        }

        var plansBase = _db.BillingInvoicePlans
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.ClientServiceContract)
            .Include(x => x.Lines)
            .Where(x => x.Status == BillingPlanStatus.Active)
            .OrderByDescending(x => x.CreatedAt)
            .AsQueryable();

        var totalPlans = await plansBase.CountAsync();
        PlansTotalPages = Math.Max(1, (int)Math.Ceiling(totalPlans / (double)cardPageSize));
        if (PlansPage > PlansTotalPages) PlansPage = PlansTotalPages;

        var planEntities = await plansBase
            .Skip((PlansPage - 1) * cardPageSize)
            .Take(cardPageSize)
            .ToListAsync();
        var planIds = planEntities.Select(x => x.Id).ToList();
        var latestRunByPlan = await _db.BillingInvoiceRuns.AsNoTracking()
            .Where(x => planIds.Contains(x.PlanId) && x.PdfStoragePath != null && x.PdfStoragePath != "")
            .GroupBy(x => x.PlanId)
            .Select(g => new { PlanId = g.Key, RunId = g.OrderByDescending(r => r.ScheduledFor).Select(r => r.Id).FirstOrDefault() })
            .ToListAsync();
        var latestRunMap = latestRunByPlan.ToDictionary(x => x.PlanId, x => (Guid?)x.RunId);
        Plans = planEntities.Select(x => new PlanVm(
            x.Id,
            x.Client != null ? x.Client.Name : "-",
            x.Concept,
            x.Total,
            LabelPeriodicity(x.Periodicity),
            x.Status.ToString(),
            x.StartDate,
            x.NextRunDate,
            x.InvoiceIssueDate,
            x.SendToEmail,
            $"Tipo {MapInvoiceType(x.InvoiceType)} · Uso {x.CfdiUseCode} · Régimen {x.FiscalRegimeCode}",
            x.CcEmails ?? "",
            x.Notes ?? "",
            x.ClientServiceContract != null ? x.ClientServiceContract.Label : null,
            x.RemainingRuns ?? 0,
            x.Lines.Count,
            latestRunMap.GetValueOrDefault(x.Id),
            x.Lines
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.CreatedAt)
                .Select(l => new PlanLineVm(
                    l.Category,
                    l.Concept,
                    l.Quantity,
                    l.UnitPrice,
                    l.Subtotal,
                    l.VatAmount,
                    l.Total))
                .ToList()
        )).ToList();

        var runsBase = _db.BillingInvoiceRuns
            .AsNoTracking()
            .Include(x => x.Plan!).ThenInclude(x => x.Client)
            .OrderByDescending(x => x.ScheduledFor)
            .AsQueryable();

        var totalRuns = await runsBase.CountAsync();
        RunsTotalPages = Math.Max(1, (int)Math.Ceiling(totalRuns / (double)historyPageSize));
        if (RunsPage > RunsTotalPages) RunsPage = RunsTotalPages;
        Runs = await runsBase
            .Skip((RunsPage - 1) * historyPageSize)
            .Take(historyPageSize)
            .Select(x => new RunVm(
                x.Id,
                x.PlanId,
                x.Plan != null && x.Plan.Client != null ? x.Plan.Client.Name : "-",
                x.Plan != null ? x.Plan.Concept : "-",
                x.PeriodLabel,
                x.ScheduledFor,
                x.Status.ToString(),
                x.SentToEmail,
                !string.IsNullOrWhiteSpace(x.PdfStoragePath),
                x.CfdiUuid,
                x.CfdiStatus.ToString(),
                x.SatStatusMessage,
                x.IsPaid,
                x.PaidAt,
                x.PaymentFormCode,
                x.PaymentMethodCode))
            .ToListAsync();

        EmittedRuns = await _db.BillingInvoiceRuns
            .AsNoTracking()
            .Include(x => x.Plan!).ThenInclude(x => x.Client)
            .Where(x => x.Status == BillingRunStatus.Sent)
            .OrderByDescending(x => x.ScheduledFor)
            .Take(80)
            .Select(x => new RunVm(
                x.Id,
                x.PlanId,
                x.Plan != null && x.Plan.Client != null ? x.Plan.Client.Name : "-",
                x.Plan != null ? x.Plan.Concept : "-",
                x.PeriodLabel,
                x.ScheduledFor,
                x.Status.ToString(),
                x.SentToEmail,
                !string.IsNullOrWhiteSpace(x.PdfStoragePath),
                x.CfdiUuid,
                x.CfdiStatus.ToString(),
                x.SatStatusMessage,
                x.IsPaid,
                x.PaidAt,
                x.PaymentFormCode,
                x.PaymentMethodCode))
            .ToListAsync();

        var auditsBase = _db.BillingAuditLogs
            .AsNoTracking()
            .Include(x => x.BillingPlan!).ThenInclude(x => x.Client)
            .OrderByDescending(x => x.CreatedAt)
            .AsQueryable();

        var totalAudits = await auditsBase.CountAsync();
        AuditsTotalPages = Math.Max(1, (int)Math.Ceiling(totalAudits / (double)historyPageSize));
        if (AuditPage > AuditsTotalPages) AuditPage = AuditsTotalPages;
        Audits = await auditsBase
            .Skip((AuditPage - 1) * historyPageSize)
            .Take(historyPageSize)
            .Select(x => new AuditVm(
                x.CreatedAt,
                x.BillingPlan != null && x.BillingPlan.Client != null ? x.BillingPlan.Client.Name : "-",
                x.EventType,
                x.Details,
                x.UserName))
            .ToListAsync();

        ActivePlans = totalPlans;
        PendingRuns = await _db.BillingInvoiceRuns.AsNoTracking().CountAsync(x => x.Status == BillingRunStatus.Scheduled);
        MonthlyProjection = Plans.Sum(x => x.Total);
    }

    private async Task<bool> CheckBillingSchemaReadyAsync()
    {
        if (!BillingSchemaReady) return false;

        try
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();

            var hasContractCol = await ColumnExistsAsync(conn, "public", "BillingInvoicePlans", "ClientServiceContractId");
            var hasIssueDateCol = await ColumnExistsAsync(conn, "public", "BillingInvoicePlans", "InvoiceIssueDate");
            var hasLinesTable = await TableExistsAsync(conn, "public", "BillingInvoiceLines");
            var hasPaidCols =
                await ColumnExistsAsync(conn, "public", "BillingInvoiceRuns", "IsPaid")
                && await ColumnExistsAsync(conn, "public", "BillingInvoiceRuns", "PaidAt")
                && await ColumnExistsAsync(conn, "public", "BillingInvoiceRuns", "PaymentFormCode")
                && await ColumnExistsAsync(conn, "public", "BillingInvoiceRuns", "PaymentMethodCode");
            BillingSchemaReady = hasContractCol && hasIssueDateCol && hasLinesTable && hasPaidCols;
        }
        catch
        {
            BillingSchemaReady = false;
        }

        if (!BillingSchemaReady)
        {
            BillingSchemaMessage = "Facturación necesita actualización de esquema (faltan columnas/tablas nuevas). Ejecuta el SQL de actualización con el usuario OWNER en pgAdmin.";
            Flash ??= BillingSchemaMessage;
            FlashType ??= "warning";
        }

        return BillingSchemaReady;
    }

    private static async Task<bool> ColumnExistsAsync(System.Data.Common.DbConnection conn, string schema, string table, string column)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
SELECT EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_schema = @schema
      AND table_name = @table
      AND column_name = @column
);
""";

        var pSchema = cmd.CreateParameter();
        pSchema.ParameterName = "@schema";
        pSchema.Value = schema;
        cmd.Parameters.Add(pSchema);

        var pTable = cmd.CreateParameter();
        pTable.ParameterName = "@table";
        pTable.Value = table;
        cmd.Parameters.Add(pTable);

        var pColumn = cmd.CreateParameter();
        pColumn.ParameterName = "@column";
        pColumn.Value = column;
        cmd.Parameters.Add(pColumn);

        var result = await cmd.ExecuteScalarAsync();
        return result is bool ok && ok;
    }

    private static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection conn, string schema, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
SELECT EXISTS (
    SELECT 1
    FROM information_schema.tables
    WHERE table_schema = @schema
      AND table_name = @table
);
""";

        var pSchema = cmd.CreateParameter();
        pSchema.ParameterName = "@schema";
        pSchema.Value = schema;
        cmd.Parameters.Add(pSchema);

        var pTable = cmd.CreateParameter();
        pTable.ParameterName = "@table";
        pTable.Value = table;
        cmd.Parameters.Add(pTable);

        var result = await cmd.ExecuteScalarAsync();
        return result is bool ok && ok;
    }

    private async Task PrefillFromOriginAsync()
    {
        var sys = await _db.SystemConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync();

        if (sys != null)
        {
            if (string.IsNullOrWhiteSpace(Input.FiscalRegimeCode) || Input.FiscalRegimeCode == "601")
                Input.FiscalRegimeCode = string.IsNullOrWhiteSpace(sys.CompanyFiscalRegimeCode) ? Input.FiscalRegimeCode : sys.CompanyFiscalRegimeCode;
            if (string.IsNullOrWhiteSpace(Input.SendToEmail))
                Input.SendToEmail = sys.BillingEmail ?? "";
        }

        if (Input.ContractId.HasValue)
        {
            var contract = await _db.ClientServiceContracts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == Input.ContractId.Value);
            if (contract != null)
            {
                Input.ClientId = contract.ClientId;
                Input.Concept = string.IsNullOrWhiteSpace(contract.Label) ? Input.Concept : contract.Label;
                if (contract.MonthlyAmount.HasValue && contract.MonthlyAmount.Value > 0)
                    Input.Subtotal = contract.MonthlyAmount.Value;
                if (contract.ContractStartDate.HasValue)
                {
                    Input.StartDate = contract.ContractStartDate.Value.Date;
                    Input.InvoiceIssueDate = contract.ContractStartDate.Value.Date;
                }
            }
        }

        if (Input.SalesOpportunityId.HasValue)
        {
            var opp = await _db.SalesOpportunities
                .AsNoTracking()
                .Include(x => x.QuoteRequest)
                .FirstOrDefaultAsync(x => x.Id == Input.SalesOpportunityId.Value);
            if (opp != null)
            {
                Input.ClientId = opp.ClientId ?? Input.ClientId;
                Input.QuoteRequestId = opp.QuoteRequestId;
                Input.Subtotal = opp.QuoteRequest != null ? (opp.QuoteRequest.EstimatedTotal ?? opp.QuoteRequest.SubtotalAuto) : Input.Subtotal;
                Input.Concept = opp.QuoteRequest != null ? $"Servicio {opp.QuoteRequest.Folio}" : Input.Concept;
            }
        }

        if (Input.QuoteRequestId.HasValue)
        {
            var q = await _db.QuoteRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == Input.QuoteRequestId.Value);
            if (q != null)
            {
                if (Input.ClientId == Guid.Empty && q.ClientId.HasValue) Input.ClientId = q.ClientId.Value;
                Input.Subtotal = q.EstimatedTotal ?? q.SubtotalAuto;
                Input.Concept = $"Servicio {q.Folio}";
                Input.ContractMonths = q.ContractTermMonths ?? Input.ContractMonths;
            }
        }

        if (Input.ClientId != Guid.Empty)
        {
            var c = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Input.ClientId);
            if (c != null)
            {
                if (string.IsNullOrWhiteSpace(Input.SendToEmail))
                    Input.SendToEmail = string.IsNullOrWhiteSpace(c.BillingEmail) ? (c.Email ?? "") : c.BillingEmail;
                if (string.IsNullOrWhiteSpace(Input.FiscalRegimeCode) && !string.IsNullOrWhiteSpace(c.FiscalRegimeCode))
                    Input.FiscalRegimeCode = c.FiscalRegimeCode;
                if (string.IsNullOrWhiteSpace(Input.CfdiUseCode) && !string.IsNullOrWhiteSpace(c.CfdiUseCodeDefault))
                    Input.CfdiUseCode = c.CfdiUseCodeDefault;
            }
        }
    }

    private void AdvancePlan(BillingInvoicePlan plan)
    {
        if (plan.Periodicity == BillingPeriodicity.OneTime)
        {
            plan.Status = BillingPlanStatus.Completed;
            return;
        }

        if (plan.RemainingRuns.HasValue)
        {
            plan.RemainingRuns -= 1;
            if (plan.RemainingRuns <= 0)
            {
                plan.Status = BillingPlanStatus.Completed;
                return;
            }
        }

        var next = AddPeriod(plan.NextRunDate, plan.Periodicity);
        if (plan.EndDate.HasValue && next.Date > plan.EndDate.Value.Date)
        {
            plan.Status = BillingPlanStatus.Completed;
            return;
        }

        plan.NextRunDate = next.Date;

        _db.BillingInvoiceRuns.Add(new BillingInvoiceRun
        {
            PlanId = plan.Id,
            PeriodLabel = BuildPeriodLabel(plan.NextRunDate, plan.Periodicity),
            ScheduledFor = plan.NextRunDate,
            Status = BillingRunStatus.Scheduled,
            SentToEmail = plan.SendToEmail,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static DateTime AddPeriod(DateTime date, BillingPeriodicity periodicity) => periodicity switch
    {
        BillingPeriodicity.Weekly => date.AddDays(7),
        BillingPeriodicity.Biweekly => date.AddDays(15),
        BillingPeriodicity.Monthly => date.AddMonths(1),
        BillingPeriodicity.Bimonthly => date.AddMonths(2),
        BillingPeriodicity.Quarterly => date.AddMonths(3),
        BillingPeriodicity.Semiannual => date.AddMonths(6),
        BillingPeriodicity.Annual => date.AddYears(1),
        _ => date
    };

    private static string BuildPeriodLabel(DateTime date, BillingPeriodicity periodicity) => periodicity switch
    {
        BillingPeriodicity.Weekly => $"Semana {date:yyyy-MM-dd}",
        BillingPeriodicity.Biweekly => $"Quincena {date:yyyy-MM}",
        BillingPeriodicity.Monthly => $"Mes {date:yyyy-MM}",
        BillingPeriodicity.Bimonthly => $"Bimestre {date:yyyy-MM}",
        BillingPeriodicity.Quarterly => $"Trimestre {date:yyyy-MM}",
        BillingPeriodicity.Semiannual => $"Semestre {date:yyyy-MM}",
        BillingPeriodicity.Annual => $"Anual {date:yyyy}",
        _ => $"Unica {date:yyyy-MM-dd}"
    };

    private static string LabelPeriodicity(BillingPeriodicity periodicity) => periodicity switch
    {
        BillingPeriodicity.OneTime => "Única",
        BillingPeriodicity.Weekly => "Semanal",
        BillingPeriodicity.Biweekly => "Quincenal",
        BillingPeriodicity.Monthly => "Mensual",
        BillingPeriodicity.Bimonthly => "Bimestral",
        BillingPeriodicity.Quarterly => "Trimestral",
        BillingPeriodicity.Semiannual => "Semestral",
        BillingPeriodicity.Annual => "Anual",
        _ => periodicity.ToString()
    };

    private static string MapInvoiceType(BillingInvoiceType type) => type switch
    {
        BillingInvoiceType.Ingreso => "I",
        BillingInvoiceType.Egreso => "E",
        BillingInvoiceType.Traslado => "T",
        BillingInvoiceType.Nomina => "N",
        BillingInvoiceType.Pago => "P",
        _ => "I"
    };

    private static IEnumerable<string> ParseEmails(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        var pieces = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in pieces.Distinct(StringComparer.OrdinalIgnoreCase))
            yield return p.Trim();
    }

    private async Task AddBillingAuditAsync(Guid planId, string eventType, string details)
    {
        _db.BillingAuditLogs.Add(new BillingAuditLog
        {
            BillingPlanId = planId,
            EventType = eventType,
            UserId = User.Identity?.Name,
            UserName = User.Identity?.Name ?? "-",
            Details = details,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}






