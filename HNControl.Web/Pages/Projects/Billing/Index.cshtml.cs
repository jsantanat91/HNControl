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
    private readonly IEventEmailTemplateService _templates;

    public IndexModel(
        ApplicationDbContext db,
        IFileStorage storage,
        IEmailSender email,
        IBillingInvoicePdfRenderer pdf,
        IEventEmailTemplateService templates)
    {
        _db = db;
        _storage = storage;
        _email = email;
        _pdf = pdf;
        _templates = templates;
    }

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

    public SelectList ClientItems { get; set; } = default!;
    public SelectList OpportunityItems { get; set; } = default!;
    public SelectList QuoteItems { get; set; } = default!;

    public List<PlanVm> Plans { get; set; } = new();
    public List<RunVm> Runs { get; set; } = new();
    public List<AuditVm> Audits { get; set; } = new();

    public int ActivePlans { get; set; }
    public int PendingRuns { get; set; }
    public decimal MonthlyProjection { get; set; }

    public record PlanVm(
        Guid Id,
        string Client,
        string Concept,
        decimal Total,
        string Periodicity,
        string Status,
        DateTime NextRunDate,
        string SendToEmail,
        string SatSummary);

    public record RunVm(
        Guid Id,
        Guid PlanId,
        string Client,
        string Concept,
        string PeriodLabel,
        DateTime ScheduledFor,
        string Status,
        string Email,
        bool HasPdf);

    public record AuditVm(DateTime CreatedAt, string Client, string EventType, string Details, string UserName);

    public class InputModel
    {
        [Required] public Guid ClientId { get; set; }
        public Guid? SalesOpportunityId { get; set; }
        public Guid? QuoteRequestId { get; set; }

        [Required, MaxLength(220)]
        public string Concept { get; set; } = "Servicio mensual";

        [Range(0.01, 999999999)] public decimal Subtotal { get; set; } = 0m;
        [Range(0, 1)] public decimal VatRate { get; set; } = 0.16m;

        [Required] public BillingPeriodicity Periodicity { get; set; } = BillingPeriodicity.Monthly;
        [Required] public DateTime StartDate { get; set; } = DateTime.Today;
        public int? NumberOfRuns { get; set; } = null;

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

    public async Task OnGetAsync(Guid? opportunityId = null, Guid? quoteId = null)
    {
        if (opportunityId.HasValue) Input.SalesOpportunityId = opportunityId;
        if (quoteId.HasValue) Input.QuoteRequestId = quoteId;
        await PrefillFromOriginAsync();
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreatePlanAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Input.ClientId);
        if (client == null)
        {
            Flash = "Cliente no encontrado.";
            FlashType = "danger";
            await LoadAsync();
            return Page();
        }

        var subtotal = Math.Round(Math.Max(0.01m, Input.Subtotal), 2);
        var vat = Math.Round(subtotal * Math.Clamp(Input.VatRate, 0m, 1m), 2);
        var total = Math.Round(subtotal + vat, 2);

        var plan = new BillingInvoicePlan
        {
            ClientId = client.Id,
            QuoteRequestId = Input.QuoteRequestId,
            SalesOpportunityId = Input.SalesOpportunityId,
            Concept = (Input.Concept ?? "").Trim(),
            Currency = "MXN",
            Subtotal = subtotal,
            VatRate = Math.Clamp(Input.VatRate, 0m, 1m),
            VatAmount = vat,
            Total = total,
            InvoiceType = Input.InvoiceType,
            CfdiUseCode = (Input.CfdiUseCode ?? "G03").Trim().ToUpperInvariant(),
            FiscalRegimeCode = (Input.FiscalRegimeCode ?? "601").Trim().ToUpperInvariant(),
            PaymentMethodCode = (Input.PaymentMethodCode ?? "PUE").Trim().ToUpperInvariant(),
            PaymentFormCode = (Input.PaymentFormCode ?? "03").Trim().ToUpperInvariant(),
            Periodicity = Input.Periodicity,
            StartDate = Input.StartDate.Date,
            NextRunDate = Input.StartDate.Date,
            RemainingRuns = Input.NumberOfRuns.HasValue && Input.NumberOfRuns.Value > 0 ? Input.NumberOfRuns.Value : null,
            SendToEmail = (Input.SendToEmail ?? "").Trim(),
            CcEmails = (Input.CcEmails ?? "").Trim(),
            Notes = (Input.Notes ?? "").Trim(),
            Status = BillingPlanStatus.Active,
            CreatedByUserId = User.Identity?.Name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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

        plan.LastSentAt = DateTime.UtcNow;
        plan.UpdatedAt = DateTime.UtcNow;

        AdvancePlan(plan);

        await _db.SaveChangesAsync();
        await AddBillingAuditAsync(plan.Id, "billing.run.sent", $"Enviado {run.PeriodLabel} a {plan.SendToEmail}.");
        Flash = "Factura enviada y ciclo actualizado.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleStatusAsync(Guid planId)
    {
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

    public async Task<IActionResult> OnGetDownloadPdfAsync(Guid runId)
    {
        var run = await _db.BillingInvoiceRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == runId);
        if (run == null || string.IsNullOrWhiteSpace(run.PdfStoragePath)) return NotFound();
        var (stream, contentType, downloadName) = await _storage.OpenAsync(run.PdfStoragePath, $"factura_{run.ScheduledFor:yyyyMMdd}.pdf");
        return File(stream, contentType, downloadName);
    }

    private async Task LoadAsync()
    {
        var clients = await _db.Clients
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsTemporaryLead)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, Label = x.ClientCode + " · " + x.Name })
            .ToListAsync();
        ClientItems = new SelectList(clients, "Id", "Label");

        var opportunities = await _db.SalesOpportunities
            .AsNoTracking()
            .Include(x => x.QuoteRequest)
            .Where(x => x.Status == SalesOpportunityStatus.ClosedWon || x.Status == SalesOpportunityStatus.ContractSigned || x.Status == SalesOpportunityStatus.CommissionApplied)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new { x.Id, Label = (x.QuoteRequest != null ? x.QuoteRequest.Folio : "-") + " · " + (x.QuoteRequest != null ? x.QuoteRequest.CustomerName : "-") })
            .ToListAsync();
        OpportunityItems = new SelectList(opportunities, "Id", "Label");

        var quotes = await _db.QuoteRequests
            .AsNoTracking()
            .Where(x => x.Status == QuoteRequestStatus.Accepted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(300)
            .Select(x => new { x.Id, Label = x.Folio + " · " + x.CustomerName })
            .ToListAsync();
        QuoteItems = new SelectList(quotes, "Id", "Label");

        Plans = await _db.BillingInvoicePlans
            .AsNoTracking()
            .Include(x => x.Client)
            .OrderByDescending(x => x.CreatedAt)
            .Take(300)
            .Select(x => new PlanVm(
                x.Id,
                x.Client != null ? x.Client.Name : "-",
                x.Concept,
                x.Total,
                x.Periodicity.ToString(),
                x.Status.ToString(),
                x.NextRunDate,
                x.SendToEmail,
                $"Tipo {MapInvoiceType(x.InvoiceType)} · Uso {x.CfdiUseCode} · Régimen {x.FiscalRegimeCode}"))
            .ToListAsync();

        Runs = await _db.BillingInvoiceRuns
            .AsNoTracking()
            .Include(x => x.Plan!).ThenInclude(x => x.Client)
            .OrderByDescending(x => x.ScheduledFor)
            .Take(300)
            .Select(x => new RunVm(
                x.Id,
                x.PlanId,
                x.Plan != null && x.Plan.Client != null ? x.Plan.Client.Name : "-",
                x.Plan != null ? x.Plan.Concept : "-",
                x.PeriodLabel,
                x.ScheduledFor,
                x.Status.ToString(),
                x.SentToEmail,
                !string.IsNullOrWhiteSpace(x.PdfStoragePath)))
            .ToListAsync();

        Audits = await _db.BillingAuditLogs
            .AsNoTracking()
            .Include(x => x.BillingPlan!).ThenInclude(x => x.Client)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new AuditVm(
                x.CreatedAt,
                x.BillingPlan != null && x.BillingPlan.Client != null ? x.BillingPlan.Client.Name : "-",
                x.EventType,
                x.Details,
                x.UserName))
            .ToListAsync();

        ActivePlans = Plans.Count(x => x.Status == nameof(BillingPlanStatus.Active));
        PendingRuns = Runs.Count(x => x.Status == nameof(BillingRunStatus.Scheduled));
        MonthlyProjection = Plans.Where(x => x.Status == nameof(BillingPlanStatus.Active)).Sum(x => x.Total);
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





