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
    [BindProperty]
    public EditInputModel EditInput { get; set; } = new();

    public SelectList ClientItems { get; set; } = default!;
    public SelectList ProjectItems { get; set; } = default!;
    public List<RowVm> Rows { get; set; } = [];
    public bool CanViewAll { get; set; }
    public bool CanManage { get; set; }
    public bool OpenCreateModal { get; set; }

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

    public class EditInputModel
    {
        [Required]
        public Guid Id { get; set; }
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
        bool IsProspect,
        Guid ClientId,
        string ProjectName,
        Guid? ProjectId,
        string Title,
        string SiteAddress,
        string Coordinates,
        string MultiSites,
        string SiteContactName,
        string SiteContactPhone,
        string Notes,
        string NotesBase,
        ServiceFeasibilityStatus Status,
        DateTime CreatedAt,
        Guid? ConvertedServiceOrderId,
        Guid? ConvertedContractId,
        bool HasSitesExcel);

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

        if (Input.ClientId == Guid.Empty)
        {
            Error = "Selecciona un cliente/prospecto valido.";
            OpenCreateModal = true;
            await LoadAsync();
            return Page();
        }

        if (!ModelState.IsValid)
        {
            Error = "No se pudo guardar la factibilidad. Revisa los campos obligatorios.";
            OpenCreateModal = true;
            await LoadAsync();
            return Page();
        }

        var userId = _userMgr.GetUserId(User);
        var validClientQuery = _db.Clients
            .Where(c => c.Id == Input.ClientId && c.IsActive);
        if (!CanViewAll)
            validClientQuery = validClientQuery.Where(c => c.OwnerUserId == userId);

        var validClient = await validClientQuery.AnyAsync();
        if (!validClient)
        {
            Error = "Selecciona un cliente/prospecto activo que te pertenezca.";
            OpenCreateModal = true;
            await LoadAsync();
            return Page();
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

        try
        {
            _db.ServiceFeasibilities.Add(row);
            await _db.SaveChangesAsync();
            Message = "Factibilidad creada.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            Error = $"No se pudo guardar la factibilidad: {ex.Message}";
            OpenCreateModal = true;
            await LoadAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAcceptAsync(Guid id)
    {
        if (!await ResolvePermissionsAsync())
            return Forbid();
        if (!CanManage)
            return Forbid();

        var row = await QueryScopedRows(_userMgr.GetUserId(User) ?? string.Empty).FirstOrDefaultAsync(x => x.Id == id);
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

        var row = await QueryScopedRows(_userMgr.GetUserId(User) ?? string.Empty).FirstOrDefaultAsync(x => x.Id == id);
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

    public async Task<IActionResult> OnPostReopenAsync(Guid id)
    {
        if (!await ResolvePermissionsAsync())
            return Forbid();
        if (!CanManage)
            return Forbid();

        var row = await QueryScopedRows(_userMgr.GetUserId(User) ?? string.Empty).FirstOrDefaultAsync(x => x.Id == id);
        if (row == null) return RedirectToPage();
        if (row.Status == ServiceFeasibilityStatus.ConvertedToOrder)
        {
            Error = "No se puede revertir una factibilidad ya convertida a orden.";
            return RedirectToPage();
        }

        row.Status = ServiceFeasibilityStatus.Open;
        row.AcceptedAt = null;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        Message = "Factibilidad reabierta.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        if (!await ResolvePermissionsAsync())
            return Forbid();
        if (!CanManage)
            return Forbid();

        ModelState.Clear();
        TryValidateModel(EditInput, nameof(EditInput));
        if (!ModelState.IsValid)
        {
            Error = "No se pudo guardar cambios de factibilidad. Revisa los campos requeridos.";
            return RedirectToPage();
        }

        var userId = _userMgr.GetUserId(User) ?? string.Empty;
        var row = await QueryScopedRows(userId).FirstOrDefaultAsync(x => x.Id == EditInput.Id);
        if (row == null)
        {
            Error = "No se encontro la factibilidad.";
            return RedirectToPage();
        }
        if (row.Status != ServiceFeasibilityStatus.Open)
        {
            Error = "Solo se puede editar cuando esta abierta/reabierta.";
            return RedirectToPage();
        }
        if (row.Status == ServiceFeasibilityStatus.ConvertedToOrder || row.ConvertedServiceOrderId.HasValue)
        {
            Error = "No se puede editar una factibilidad ya convertida a orden.";
            return RedirectToPage();
        }

        var validClientQuery = _db.Clients
            .Where(c => c.Id == EditInput.ClientId && c.IsActive);
        if (!CanViewAll)
            validClientQuery = validClientQuery.Where(c => c.OwnerUserId == userId);
        var validClient = await validClientQuery.AnyAsync();
        if (!validClient)
        {
            Error = "Cliente/prospecto invalido para tu alcance.";
            return RedirectToPage();
        }

        row.ClientId = EditInput.ClientId;
        row.ProjectId = EditInput.ProjectId;
        row.Title = (EditInput.Title ?? "").Trim();
        row.SiteAddress = (EditInput.SiteAddress ?? "").Trim();
        row.Coordinates = string.IsNullOrWhiteSpace(EditInput.Coordinates) ? null : EditInput.Coordinates.Trim();
        row.SiteContactName = (EditInput.SiteContactName ?? "").Trim();
        row.SiteContactPhone = (EditInput.SiteContactPhone ?? "").Trim();
        row.Notes = BuildNotesWithSites(
            (EditInput.Notes ?? "").Trim(),
            EditInput.MultiSites,
            Request.Form.Files["EditSitesExcel"]);
        row.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        Message = "Factibilidad actualizada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConvertAsync(Guid id)
    {
        if (!await ResolvePermissionsAsync())
            return Forbid();
        if (!CanManage)
            return Forbid();

        var row = await QueryScopedRows(_userMgr.GetUserId(User) ?? string.Empty).FirstOrDefaultAsync(x => x.Id == id);
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

    public async Task<IActionResult> OnPostConvertContractAsync(Guid id)
    {
        if (!await ResolvePermissionsAsync())
            return Forbid();
        if (!CanManage)
            return Forbid();

        var row = await QueryScopedRows(_userMgr.GetUserId(User) ?? string.Empty).FirstOrDefaultAsync(x => x.Id == id);
        if (row == null) return RedirectToPage();
        if (row.Status != ServiceFeasibilityStatus.Accepted)
        {
            Error = "Primero acepta la factibilidad para convertirla en contrato.";
            return RedirectToPage();
        }

        var linkedContractId = ExtractConvertedContractId(row.Notes);
        if (linkedContractId.HasValue)
            return RedirectToPage("/Clients/Services/Details", new { id = linkedContractId.Value });

        return RedirectToPage("/Clients/Services/Create", new
        {
            clientId = row.ClientId,
            feasibilityId = row.Id
        });
    }

    public async Task<JsonResult> OnGetProjectsAsync(Guid clientId)
    {
        var userId = _userMgr.GetUserId(User) ?? string.Empty;
        var projects = _db.Projects
            .AsNoTracking()
            .Where(x => x.ClientId == clientId);
        if (!CanViewAll)
            projects = projects.Where(x => x.Client.OwnerUserId == userId);

        var rows = await projects
            .OrderByDescending(x => x.StartDate)
            .Select(x => new { id = x.Id, text = x.Title })
            .ToListAsync();
        return new JsonResult(rows);
    }

    public async Task<IActionResult> OnGetExportSitesAsync(Guid id)
    {
        if (!await ResolvePermissionsAsync())
            return Forbid();

        var row = await QueryScopedRows(_userMgr.GetUserId(User) ?? string.Empty)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (row == null) return NotFound();

        var lines = ExtractSitesForExport(row.Notes);
        if (lines.Count == 0)
        {
            Error = "Esta factibilidad no tiene sitios capturados para exportar.";
            return RedirectToPage();
        }

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sitios");
        ws.Cell(1, 1).Value = "Direccion";
        ws.Cell(1, 2).Value = "Coordenadas";
        ws.Cell(1, 3).Value = "CapacidadMB";
        ws.Row(1).Style.Font.Bold = true;

        var r = 2;
        foreach (var line in lines)
        {
            ws.Cell(r, 1).Value = line.Address;
            ws.Cell(r, 2).Value = line.Coordinates;
            ws.Cell(r, 3).Value = line.CapacityMb;
            r++;
        }
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        var fileName = $"factibilidad-sitios-{row.Id:N}.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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

        var clientsQuery = _db.Clients
            .AsNoTracking()
            .Where(c => c.IsActive);
        if (!CanViewAll)
            clientsQuery = clientsQuery.Where(c => c.OwnerUserId == userId);

        var clients = await clientsQuery
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                Label = $"[{(c.IsTemporaryLead ? "Prospecto" : "Cliente")}] {c.Name}"
            })
            .ToListAsync();
        ClientItems = new SelectList(clients, "Id", "Label");
        ProjectItems = new SelectList(Enumerable.Empty<object>(), "Id", "Title");

        var q = _db.ServiceFeasibilities
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.Project)
            .AsQueryable();

        if (!CanViewAll)
            q = q.Where(x => (x.Client != null && x.Client.OwnerUserId == userId) || x.CreatedByUserId == userId);

        var rawRows = await q
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new
            {
                x.Id,
                ClientName = x.Client != null ? x.Client.Name : "-",
                IsProspect = x.Client != null && x.Client.IsTemporaryLead,
                x.ClientId,
                ProjectName = x.Project != null ? x.Project.Title : "-",
                x.ProjectId,
                x.Title,
                x.SiteAddress,
                Coordinates = x.Coordinates ?? "-",
                x.SiteContactName,
                x.SiteContactPhone,
                x.Notes,
                x.Status,
                x.CreatedAt,
                x.ConvertedServiceOrderId
            })
            .ToListAsync();

        Rows = rawRows
            .Select(x => new RowVm(
                x.Id,
                x.ClientName,
                x.IsProspect,
                x.ClientId,
                x.ProjectName,
                x.ProjectId,
                x.Title,
                x.SiteAddress,
                x.Coordinates,
                ExtractMultiSites(x.Notes),
                x.SiteContactName,
                x.SiteContactPhone,
                x.Notes,
                ExtractBaseNotes(x.Notes),
                x.Status,
                x.CreatedAt,
                x.ConvertedServiceOrderId,
                ExtractConvertedContractId(x.Notes),
                HasExcelSites(x.Notes)))
            .ToList();
    }

    private IQueryable<ServiceFeasibility> QueryScopedRows(string userId)
    {
        var q = _db.ServiceFeasibilities
            .Include(x => x.Client)
            .AsQueryable();
        if (!CanViewAll)
            q = q.Where(x => (x.Client != null && x.Client.OwnerUserId == userId) || x.CreatedByUserId == userId);
        return q;
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

    private static string ExtractBaseNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return "";

        var text = notes!;
        var tags = new[] { "[SITIOS]", "[EXCEL SITIOS]" };
        var cutAt = text.Length;
        foreach (var tag in tags)
        {
            var idx = text.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && idx < cutAt)
                cutAt = idx;
        }

        return (cutAt <= 0 ? "" : text[..cutAt]).Trim();
    }

    private static bool HasExcelSites(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return false;
        return notes.Contains("[EXCEL SITIOS]", StringComparison.OrdinalIgnoreCase);
    }

    private static Guid? ExtractConvertedContractId(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return null;

        var lines = notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (!line.StartsWith("[META] ContratoId=", StringComparison.OrdinalIgnoreCase))
                continue;

            var raw = line["[META] ContratoId=".Length..].Trim();
            if (Guid.TryParse(raw, out var id))
                return id;
        }

        return null;
    }

    private static List<(string Address, string Coordinates, string CapacityMb)> ExtractSitesForExport(string notes)
    {
        var result = new List<(string Address, string Coordinates, string CapacityMb)>();
        if (string.IsNullOrWhiteSpace(notes)) return result;

        var lines = notes
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !l.StartsWith("[", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var line in lines)
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length == 0) continue;
            var address = parts.ElementAtOrDefault(0) ?? "";
            var coords = parts.ElementAtOrDefault(1) ?? "";
            var cap = parts.ElementAtOrDefault(2) ?? "";
            if (string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(coords) && string.IsNullOrWhiteSpace(cap))
                continue;
            result.Add((address, coords, cap));
        }

        return result;
    }
}
