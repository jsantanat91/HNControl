using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Sales;

[Authorize(Policy = "EmployeeOnly")]
public class FeasibilityModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IActionAccessService _actions;

    public FeasibilityModel(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IActionAccessService actions)
    {
        _db = db;
        _userMgr = userMgr;
        _actions = actions;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public SelectList ClientItems { get; set; } = default!;
    public SelectList ProjectItems { get; set; } = default!;
    public List<RowVm> Rows { get; set; } = [];
    public bool CanViewAll { get; set; }
    public bool CanManage { get; set; }

    [TempData] public string? Message { get; set; }
    [TempData] public string? Error { get; set; }

    public class InputModel
    {
        [Required]
        public Guid ClientId { get; set; }
        public Guid? ProjectId { get; set; }
        [Required, MaxLength(200)]
        public string Title { get; set; } = "";
        [Required, MaxLength(400)]
        public string SiteAddress { get; set; } = "";
        [MaxLength(64)]
        public string? Coordinates { get; set; }
        [MaxLength(4000)]
        public string? MultiSites { get; set; }
        [Required, MaxLength(160)]
        public string SiteContactName { get; set; } = "";
        [Required, MaxLength(60)]
        public string SiteContactPhone { get; set; } = "";
        [MaxLength(2000)]
        public string Notes { get; set; } = "";
    }

    public record RowVm(
        Guid Id,
        string ClientName,
        string ProjectName,
        string Title,
        string SiteAddress,
        string Coordinates,
        string MultiSites,
        string SiteContactName,
        string SiteContactPhone,
        string Notes,
        ServiceFeasibilityStatus Status,
        DateTime CreatedAt,
        Guid? ConvertedServiceOrderId);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await ResolvePermissionsAsync())
            return Forbid();
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!await ResolvePermissionsAsync())
            return Forbid();
        if (!CanManage)
            return Forbid();

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var validClient = await _db.Clients
            .AnyAsync(c => c.Id == Input.ClientId && c.IsActive && !c.IsTemporaryLead);
        if (!validClient)
        {
            Error = "Solo se permiten clientes activos (no prospectos).";
            return RedirectToPage();
        }

        var row = new ServiceFeasibility
        {
            ClientId = Input.ClientId,
            ProjectId = Input.ProjectId,
            Title = (Input.Title ?? "").Trim(),
            SiteAddress = (Input.SiteAddress ?? "").Trim(),
            Coordinates = string.IsNullOrWhiteSpace(Input.Coordinates) ? null : Input.Coordinates.Trim(),
            SiteContactName = (Input.SiteContactName ?? "").Trim(),
            SiteContactPhone = (Input.SiteContactPhone ?? "").Trim(),
            Notes = BuildNotesWithSites((Input.Notes ?? "").Trim(), Input.MultiSites, Request.Form.Files["SitesExcel"]),
            Status = ServiceFeasibilityStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = _userMgr.GetUserId(User)
        };

        _db.ServiceFeasibilities.Add(row);
        await _db.SaveChangesAsync();
        Message = "Factibilidad creada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAcceptAsync(Guid id)
    {
        if (!await ResolvePermissionsAsync())
            return Forbid();
        if (!CanManage)
            return Forbid();

        var row = await _db.ServiceFeasibilities.FirstOrDefaultAsync(x => x.Id == id);
        if (row == null) return RedirectToPage();
        if (row.Status == ServiceFeasibilityStatus.ConvertedToOrder)
        {
            Error = "La factibilidad ya fue convertida a orden.";
            return RedirectToPage();
        }

        row.Status = ServiceFeasibilityStatus.Accepted;
        row.AcceptedAt = DateTime.UtcNow;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        Message = "Factibilidad aceptada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        if (!await ResolvePermissionsAsync())
            return Forbid();
        if (!CanManage)
            return Forbid();

        var row = await _db.ServiceFeasibilities.FirstOrDefaultAsync(x => x.Id == id);
        if (row == null) return RedirectToPage();
        if (row.Status == ServiceFeasibilityStatus.ConvertedToOrder)
        {
            Error = "La factibilidad ya fue convertida a orden.";
            return RedirectToPage();
        }

        row.Status = ServiceFeasibilityStatus.Rejected;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        Message = "Factibilidad rechazada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConvertAsync(Guid id)
    {
        if (!await ResolvePermissionsAsync())
            return Forbid();
        if (!CanManage)
            return Forbid();

        var row = await _db.ServiceFeasibilities.FirstOrDefaultAsync(x => x.Id == id);
        if (row == null) return RedirectToPage();
        if (row.ConvertedServiceOrderId.HasValue)
        {
            Message = "Ya estaba convertida.";
            return RedirectToPage("/Admin/ServiceOrders/Details", new { id = row.ConvertedServiceOrderId.Value });
        }
        if (row.Status != ServiceFeasibilityStatus.Accepted)
        {
            Error = "Primero acepta la factibilidad para convertirla en orden de servicio.";
            return RedirectToPage();
        }

        var order = new ServiceOrder
        {
            Title = row.Title,
            Type = ServiceOrderType.LevantamientoTecnico,
            Status = ServiceOrderStatus.Created,
            ClientId = row.ClientId,
            ProjectId = row.ProjectId,
            Description = $"Factibilidad en sitio\nDirección: {row.SiteAddress}\nCoordenadas: {row.Coordinates ?? "-"}\nContacto: {row.SiteContactName} ({row.SiteContactPhone})\nNotas: {row.Notes}".Trim(),
            StartedAt = TimeUtil.UtcDate(DateTime.Today),
            EstimatedEndDate = TimeUtil.UtcDate(DateTime.Today.AddDays(2)),
            PublicToken = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            CurrentArea = ServiceOrderWorkflowArea.Levantamiento
        };
        _db.ServiceOrders.Add(order);
        await _db.SaveChangesAsync();

        row.ConvertedServiceOrderId = order.Id;
        row.Status = ServiceFeasibilityStatus.ConvertedToOrder;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Message = "Factibilidad convertida a orden.";
        return RedirectToPage("/Admin/ServiceOrders/Details", new { id = order.Id });
    }

    public async Task<JsonResult> OnGetProjectsAsync(Guid clientId)
    {
        var rows = await _db.Projects
            .AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.StartDate)
            .Select(x => new { id = x.Id, text = x.Title })
            .ToListAsync();
        return new JsonResult(rows);
    }

    private async Task<bool> ResolvePermissionsAsync()
    {
        var canView = AppRoles.IsGlobalAdmin(User)
            || await _actions.HasActionAsync(User, AppActions.SalesFeasibilityView);
        CanViewAll = AppRoles.IsGlobalAdmin(User);
        CanManage = canView;
        return canView;
    }

    private async Task LoadAsync()
    {
        var userId = _userMgr.GetUserId(User) ?? string.Empty;

        var clients = await _db.Clients
            .AsNoTracking()
            .Where(c => c.IsActive && !c.IsTemporaryLead)
            .OrderBy(c => c.Name)
            .ToListAsync();
        ClientItems = new SelectList(clients, "Id", "Name");
        ProjectItems = new SelectList(Enumerable.Empty<object>(), "Id", "Title");

        var q = _db.ServiceFeasibilities
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.Project)
            .AsQueryable();

        if (!CanViewAll)
            q = q.Where(x => x.CreatedByUserId == userId);

        Rows = await q
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new RowVm(
                x.Id,
                x.Client != null ? x.Client.Name : "-",
                x.Project != null ? x.Project.Title : "-",
                x.Title,
                x.SiteAddress,
                x.Coordinates ?? "-",
                ExtractMultiSites(x.Notes),
                x.SiteContactName,
                x.SiteContactPhone,
                x.Notes,
                x.Status,
                x.CreatedAt,
                x.ConvertedServiceOrderId))
            .ToListAsync();
    }

    private static string BuildNotesWithSites(string baseNotes, string? multiSites, IFormFile? excelFile)
    {
        var chunks = new List<string>();
        if (!string.IsNullOrWhiteSpace(baseNotes))
            chunks.Add(baseNotes.Trim());

        var manualRows = (multiSites ?? "")
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(30)
            .ToList();
        if (manualRows.Count > 0)
            chunks.Add("[SITIOS]\n" + string.Join('\n', manualRows));

        if (excelFile != null && excelFile.Length > 0)
        {
            try
            {
                using var stream = excelFile.OpenReadStream();
                using var wb = new XLWorkbook(stream);
                var ws = wb.Worksheets.FirstOrDefault();
                if (ws != null)
                {
                    var excelRows = new List<string>();
                    foreach (var row in ws.RowsUsed().Skip(1).Take(50))
                    {
                        var address = row.Cell(1).GetString().Trim();
                        var coords = row.Cell(2).GetString().Trim();
                        var mb = row.Cell(3).GetString().Trim();
                        if (string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(coords) && string.IsNullOrWhiteSpace(mb))
                            continue;
                        excelRows.Add($"{address} | {coords} | {mb} MB");
                    }

                    if (excelRows.Count > 0)
                        chunks.Add("[EXCEL SITIOS]\n" + string.Join('\n', excelRows));
                }
            }
            catch
            {
                chunks.Add("[EXCEL SITIOS]\nNo se pudo leer el archivo cargado.");
            }
        }

        var text = string.Join("\n\n", chunks);
        return text.Length <= 2000 ? text : text[..2000];
    }

    private static string ExtractMultiSites(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return "-";

        var sitIdx = notes.IndexOf("[SITIOS]", StringComparison.OrdinalIgnoreCase);
        var excelIdx = notes.IndexOf("[EXCEL SITIOS]", StringComparison.OrdinalIgnoreCase);
        var idx = sitIdx >= 0 ? sitIdx : excelIdx;
        if (idx < 0) return "-";

        var block = notes[idx..];
        var lines = block
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !l.StartsWith("[", StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToList();

        return lines.Count == 0 ? "-" : string.Join(" · ", lines);
    }
}
