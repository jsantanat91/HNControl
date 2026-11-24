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
    IConfiguration config) : PageModel
{
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
        survey.Status = NewStatus;
        survey.InternalNotes = LevantamientoSurveyService.Clip(Notes, 4000);
        survey.UpdatedAt = DateTime.UtcNow;
        survey.UpdatedByUserId = users.GetUserId(User);
        await db.SaveChangesAsync();
        Message = "Factibilidad actualizada.";
        return RedirectToPage(new { id });
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
