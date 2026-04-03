using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Sales;

[Authorize(Policy = "EmployeeOnly")]
public class TemplatesModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IActionAccessService _actions;

    public TemplatesModel(ApplicationDbContext db, IActionAccessService actions)
    {
        _db = db;
        _actions = actions;
    }

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }

    [BindProperty] public string EventKey { get; set; } = "";
    [BindProperty] public string SubjectTemplate { get; set; } = "";
    [BindProperty] public string BodyTemplate { get; set; } = "";
    [BindProperty] public bool IsActive { get; set; } = true;

    public record RowVm(Guid Id, string EventKey, string SubjectTemplate, bool IsActive, DateTime UpdatedAt);
    public List<RowVm> Rows { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await CanManageAsync()) return Forbid();
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!await CanManageAsync()) return Forbid();

        EventKey = (EventKey ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(EventKey) || string.IsNullOrWhiteSpace(SubjectTemplate) || string.IsNullOrWhiteSpace(BodyTemplate))
        {
            Flash = "Completa evento, asunto y cuerpo.";
            FlashType = "warning";
            await LoadAsync();
            return Page();
        }

        var tpl = await _db.EventEmailTemplates.FirstOrDefaultAsync(x => x.EventKey == EventKey);
        if (tpl == null)
        {
            tpl = new EventEmailTemplate
            {
                EventKey = EventKey,
                SubjectTemplate = SubjectTemplate.Trim(),
                BodyTemplate = BodyTemplate.Trim(),
                IsActive = IsActive,
                UpdatedAt = DateTime.UtcNow
            };
            _db.EventEmailTemplates.Add(tpl);
        }
        else
        {
            tpl.SubjectTemplate = SubjectTemplate.Trim();
            tpl.BodyTemplate = BodyTemplate.Trim();
            tpl.IsActive = IsActive;
            tpl.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        Flash = "Plantilla guardada.";
        FlashType = "success";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetLoadAsync(string key)
    {
        if (!await CanManageAsync()) return Forbid();
        if (string.IsNullOrWhiteSpace(key)) return RedirectToPage();

        var tpl = await _db.EventEmailTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.EventKey == key);
        if (tpl == null) return RedirectToPage();

        EventKey = tpl.EventKey;
        SubjectTemplate = tpl.SubjectTemplate;
        BodyTemplate = tpl.BodyTemplate;
        IsActive = tpl.IsActive;
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        await EnsureDefaultsAsync();
        Rows = await _db.EventEmailTemplates
            .AsNoTracking()
            .OrderBy(x => x.EventKey)
            .Select(x => new RowVm(x.Id, x.EventKey, x.SubjectTemplate, x.IsActive, x.UpdatedAt))
            .ToListAsync();
    }

    private async Task<bool> CanManageAsync()
        => AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.TemplatesManage);

    private async Task EnsureDefaultsAsync()
    {
        var defaults = new Dictionary<string, (string Subject, string Body)>(StringComparer.OrdinalIgnoreCase)
        {
            ["commercial.daily.reminder"] = (
                "Recordatorio comercial {{Fecha}}",
                "<p>Resumen comercial del dia {{Fecha}}.</p><p>Contratos pendientes: <b>{{ContratosPendientes}}</b><br/>Facturas pendientes: <b>{{FacturasPendientes}}</b><br/>Comisiones pendientes: <b>{{ComisionesPendientes}}</b></p>"),
            ["billing.invoice.scheduled"] = (
                "Estado de facturacion {{Cliente}} · {{Periodo}}",
                "<p>Hola,</p><p>Compartimos el estado de cuenta del periodo <b>{{Periodo}}</b>.</p><p>Concepto: <b>{{Concepto}}</b><br/>Monto: <b>{{Monto}}</b></p>"),
            ["sales.commission.paid"] = (
                "Comision registrada {{Folio}}",
                "<p>Hola {{Vendedor}},</p><p>La comision de la venta <b>{{Folio}}</b> fue registrada por <b>{{MontoComision}}</b> para la siguiente nomina.</p>"),
            ["sales.contract.signature"] = (
                "Contrato listo para firma {{Folio}}",
                "<p>Hola {{Cliente}},</p><p>Tu contrato de la cotizacion <b>{{Folio}}</b> esta listo para firma.</p>")
        };

        var existing = await _db.EventEmailTemplates.AsNoTracking().Select(x => x.EventKey).ToListAsync();
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in defaults)
        {
            if (set.Contains(item.Key)) continue;
            _db.EventEmailTemplates.Add(new EventEmailTemplate
            {
                EventKey = item.Key,
                SubjectTemplate = item.Value.Subject,
                BodyTemplate = item.Value.Body,
                IsActive = true,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }
}
