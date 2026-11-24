using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using HNControl.Web.Services.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Sales;

[Authorize(Policy = "EmployeeOnly")]
public class FeasibilityDetailModel(
    ApplicationDbContext db,
    IActionAccessService actions,
    UserManager<ApplicationUser> users,
    LevantamientoSurveyService surveys,
    ServiceOrderCreationNotifications notifications,
    IClientPortalAccessService portalAccess,
    LevantamientoDecisionMailer mailer,
    IConfiguration config) : PageModel
{
    public List<LevantamientoReview.CheckResult> ReviewResults { get; set; } = [];
    public bool IsDecided => Survey.DecidedAt != null && Survey.Status is LevantamientoSurveyStatus.Feasible or LevantamientoSurveyStatus.NotFeasible or LevantamientoSurveyStatus.Quoted;

    // Dictamen de viabilidad (2.6.3)
    [BindProperty] public Dictionary<string, string?> Checks { get; set; } = new();
    [BindProperty] public string? Decision { get; set; }
    [BindProperty] public string? ClientComments { get; set; }
    [BindProperty] public bool SendEmail { get; set; }
    [BindProperty] public string? EmailTo { get; set; }
    public string? DecisionError { get; set; }

    /// <summary>Manual status changes allowed in Seguimiento; Viable / No viable only come from the decision.</summary>
    public static LevantamientoSurveyStatus[] AllowedStatuses(LevantamientoSurveyStatus current) => current switch
    {
        LevantamientoSurveyStatus.New or LevantamientoSurveyStatus.InReview => [LevantamientoSurveyStatus.New, LevantamientoSurveyStatus.InReview],
        LevantamientoSurveyStatus.Feasible or LevantamientoSurveyStatus.Quoted => [LevantamientoSurveyStatus.Feasible, LevantamientoSurveyStatus.Quoted],
        _ => [current]
    };
    public LevantamientoSurvey Survey { get; set; } = default!;
    public List<LevantamientoSection> Sections { get; set; } = [];
    public List<LevantamientoSite> Sites { get; set; } = [];
    public Client? LinkedClient { get; set; }
    public ServiceOrder? Order { get; set; }
    public List<(Guid Id, string Label)> ClientChoices { get; set; } = [];
    public bool CanCreateClient { get; set; }
    public int OtherSurveysOfClient { get; set; }
    [TempData] public string? Message { get; set; }
    [TempData] public string? Error { get; set; }

    [BindProperty] public LevantamientoSurveyStatus NewStatus { get; set; }
    [BindProperty] public string? Notes { get; set; }
    [BindProperty] public Guid LinkClientId { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await FeasibilityModel.CanViewAsync(User, actions)) return Forbid();
        var survey = await db.LevantamientoSurveys.AsNoTracking()
            .Include(x => x.Client).Include(x => x.ServiceOrder)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (survey == null) return NotFound();
        Survey = survey;
        LinkedClient = survey.Client;
        Order = survey.ServiceOrder;
        NewStatus = survey.Status;
        Notes = survey.InternalNotes;
        Sections = LevantamientoSurveyService.ParseSections(survey.SectionsJson);
        Sites = LevantamientoSurveyService.ReadSites(survey.SitesFileName, survey.SitesBytes);
        ReviewResults = LevantamientoReview.Parse(survey.ReviewChecksJson);
        ClientComments = survey.DecisionComments;
        EmailTo = string.IsNullOrWhiteSpace(survey.DecisionEmailTo) ? survey.Email : survey.DecisionEmailTo;
        CanCreateClient = await CanCreateClientAsync();
        if (survey.ClientId != null)
            OtherSurveysOfClient = await db.LevantamientoSurveys.CountAsync(x => x.ClientId == survey.ClientId && x.Id != id);
        else
        {
            var clients = await db.Clients.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Name, x.ClientCode, x.IsTemporaryLead }).Take(3000).ToListAsync();
            ClientChoices = clients.Select(x => (x.Id, $"{x.Name} · {x.ClientCode}{(x.IsTemporaryLead ? " (prospecto)" : "")}")).ToList();
        }
        return Page();
    }

    public async Task<IActionResult> OnGetPdfAsync(Guid id)
    {
        if (!await FeasibilityModel.CanViewAsync(User, actions)) return Forbid();
        var row = await db.LevantamientoSurveys.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.PdfBytes, x.PdfFileName, x.ExternalId }).FirstOrDefaultAsync();
        if (row?.PdfBytes == null) return NotFound();
        Response.Headers.ContentDisposition = $"inline; filename=\"{SafeName(row.PdfFileName, row.ExternalId + ".pdf")}\"";
        return File(row.PdfBytes, "application/pdf");
    }

    public async Task<IActionResult> OnGetSitesAsync(Guid id)
    {
        if (!await FeasibilityModel.CanViewAsync(User, actions)) return Forbid();
        var row = await db.LevantamientoSurveys.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.SitesBytes, x.SitesFileName }).FirstOrDefaultAsync();
        if (row?.SitesBytes == null) return NotFound();
        var name = SafeName(row.SitesFileName, "sitios.xlsx");
        var type = Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".csv" => "text/csv",
            ".xls" => "application/vnd.ms-excel",
            _ => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
        return File(row.SitesBytes, type, name);
    }

    public async Task<IActionResult> OnPostStatusAsync(Guid id)
    {
        if (!await FeasibilityModel.CanViewAsync(User, actions)) return Forbid();
        var survey = await db.LevantamientoSurveys.FirstOrDefaultAsync(x => x.Id == id);
        if (survey == null) return NotFound();
        if (!Enum.IsDefined(NewStatus)) { Error = "Estado no válido."; return RedirectToPage(new { id }); }
        // Viable / No viable solo se asignan con el dictamen; aquí solo se mueve dentro de su etapa.
        if (AllowedStatuses(survey.Status).Contains(NewStatus)) survey.Status = NewStatus;
        survey.InternalNotes = LevantamientoSurveyService.Clip(Notes, 4000);
        survey.UpdatedAt = DateTime.UtcNow;
        survey.UpdatedByUserId = users.GetUserId(User);
        await db.SaveChangesAsync();
        Message = "Factibilidad actualizada.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDecisionAsync(Guid id)
    {
        if (!await FeasibilityModel.CanViewAsync(User, actions)) return Forbid();
        if (Decision is not ("viable" or "no_viable")) { Error = "Elige si el proyecto es viable o no viable."; return RedirectToPage(new { id }); }
        var viable = Decision == "viable";
        var error = await surveys.DecideAsync(id, viable, Checks, ClientComments, users.GetUserId(User));
        if (error != null)
        {
            // Se vuelve a mostrar el formulario con lo capturado para no perder la revisión.
            var (checks, comments, sendEmail, emailTo) = (Checks, ClientComments, SendEmail, EmailTo);
            var page = await OnGetAsync(id);
            if (page is not PageResult) return page;
            ReviewResults = LevantamientoReview.Normalize(checks);
            (ClientComments, SendEmail, EmailTo, DecisionError) = (comments, sendEmail, emailTo, error);
            return page;
        }
        var result = viable ? "Proyecto dictaminado como viable. Ya puedes crear la orden de servicio." : "Proyecto dictaminado como no viable. El flujo se detiene: no se puede crear la orden.";
        if (!SendEmail) { Message = result + " No se envió correo al cliente."; return RedirectToPage(new { id }); }
        var mailError = await mailer.SendAsync(id, EmailTo);
        if (mailError != null) { Message = result; Error = mailError + " Puedes reenviarlo desde el dictamen."; }
        else Message = result + " Se envió el correo al cliente.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSendDecisionAsync(Guid id)
    {
        if (!await FeasibilityModel.CanViewAsync(User, actions)) return Forbid();
        var mailError = await mailer.SendAsync(id, EmailTo);
        if (mailError != null) Error = mailError; else Message = "Se envió el dictamen al cliente.";
        return RedirectToPage(new { id });
    }

    /// <summary>Preview of the customer email exactly as it will be sent.</summary>
    public async Task<IActionResult> OnGetDecisionPreviewAsync(Guid id)
    {
        if (!await FeasibilityModel.CanViewAsync(User, actions)) return Forbid();
        var survey = await db.LevantamientoSurveys.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (survey?.DecidedAt == null) return NotFound();
        var sites = LevantamientoSurveyService.ReadSites(survey.SitesFileName, survey.SitesBytes).Count;
        return Content(LevantamientoReview.EmailBody(survey, sites, survey.PdfBytes is { Length: > 0 }), "text/html; charset=utf-8");
    }

    public async Task<IActionResult> OnPostCreateClientAsync(Guid id)
    {
        if (!await FeasibilityModel.CanViewAsync(User, actions) || !await CanCreateClientAsync()) return Forbid();
        var userId = users.GetUserId(User);
        var (client, error) = await surveys.CreateClientAsync(id, userId);
        if (client == null) { Error = error; return RedirectToPage(new { id }); }
        await portalAccess.EnsureForClientAsync(client.Id, userId, forceResetPassword: false);
        Message = $"Cliente {client.ClientCode} listo y ligado. Completa sus datos fiscales en la ficha del cliente antes de facturar. Ya puedes crear la orden de servicio.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostLinkAsync(Guid id)
    {
        if (!await FeasibilityModel.CanViewAsync(User, actions)) return Forbid();
        var error = await surveys.LinkClientAsync(id, LinkClientId, users.GetUserId(User));
        if (error != null) Error = error; else Message = "Cliente ligado. Ya puedes crear la orden de servicio.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreateOrderAsync(Guid id)
    {
        if (!await FeasibilityModel.CanViewAsync(User, actions)) return Forbid();
        // Double submit: go to the order already created instead of creating another one.
        var existingOrderId = await db.LevantamientoSurveys.Where(x => x.Id == id).Select(x => x.ServiceOrderId).FirstOrDefaultAsync();
        if (existingOrderId != null) return RedirectToPage("/Admin/ServiceOrders/Details", new { id = existingOrderId.Value });
        var baseUrl =(config["PublicLinks:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');
        var (order, error) = await surveys.CreateOrderAsync(id, users.GetUserId(User), $"{baseUrl}/Ventas/Factibilidad/{id}");
        if (order == null) { Error = error; return RedirectToPage(new { id }); }
        notifications.QueueCreated(order, scheduledDateConfirmed: false);
        await db.SaveChangesAsync();
        TempData[ServiceOrderCreationNotifications.NoticeKey] = (await notifications.NotifyCreatedAsync(order.Id)).UserMessage;
        return RedirectToPage("/Admin/ServiceOrders/Details", new { id = order.Id });
    }

    private async Task<bool> CanCreateClientAsync() =>
        AppRoles.IsGlobalAdmin(User) || await actions.HasActionAsync(User, AppActions.ClientsEdit);

    private static string SafeName(string? name, string fallback)
    {
        var clean = new string((name ?? "").Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.').ToArray());
        return string.IsNullOrWhiteSpace(clean) ? fallback : clean;
    }
}
