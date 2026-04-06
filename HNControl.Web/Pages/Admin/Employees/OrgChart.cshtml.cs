using System.Security.Claims;
using ClosedXML.Excel;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.Employees;

[Authorize(Roles = AppRoles.Admin)]
public class OrgChartModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IActionAccessService _actions;

    public OrgChartModel(ApplicationDbContext db, IActionAccessService actions)
    {
        _db = db;
        _actions = actions;
    }

    public List<EmployeeVm> Employees { get; set; } = [];
    public List<NodeVm> Nodes { get; set; } = [];
    public bool CanViewPermission { get; set; }
    public bool CanEdit { get; set; }
    public bool CanExport { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (!CanViewPermission) return Forbid();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync([FromForm] string? layoutJson)
    {
        await ResolvePermissionsAsync();
        if (!CanEdit) return Forbid();
        if (string.IsNullOrWhiteSpace(layoutJson))
            return new JsonResult(new { ok = false, message = "Layout vacío." });

        List<NodeVm>? incoming;
        try
        {
            incoming = global::System.Text.Json.JsonSerializer.Deserialize<List<NodeVm>>(layoutJson, new global::System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return new JsonResult(new { ok = false, message = "Layout inválido." });
        }

        incoming ??= [];

        var validUsers = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.UserId)
            .ToListAsync();
        var validSet = validUsers.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var normalized = incoming
            .Where(x => !string.IsNullOrWhiteSpace(x.UserId))
            .GroupBy(x => x.UserId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select((x, idx) => new EmployeeOrgChartNode
            {
                Id = Guid.NewGuid(),
                UserId = x.UserId.Trim(),
                ReportsToUserId = string.IsNullOrWhiteSpace(x.ReportsToUserId) ? null : x.ReportsToUserId!.Trim(),
                SortOrder = x.SortOrder == 0 ? idx : x.SortOrder,
                PositionX = Math.Max(0, x.PositionX),
                PositionY = Math.Max(0, x.PositionY),
                UpdatedAt = DateTime.UtcNow,
                UpdatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            })
            .Where(x => validSet.Contains(x.UserId))
            .ToList();

        foreach (var node in normalized)
        {
            if (!string.IsNullOrWhiteSpace(node.ReportsToUserId) && !validSet.Contains(node.ReportsToUserId))
                node.ReportsToUserId = null;
            if (string.Equals(node.UserId, node.ReportsToUserId, StringComparison.OrdinalIgnoreCase))
                node.ReportsToUserId = null;
        }

        var existing = await _db.EmployeeOrgChartNodes.ToListAsync();
        _db.EmployeeOrgChartNodes.RemoveRange(existing);
        _db.EmployeeOrgChartNodes.AddRange(normalized);
        await _db.SaveChangesAsync();

        return new JsonResult(new { ok = true, message = "Organigrama guardado." });
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        await ResolvePermissionsAsync();
        if (!CanViewPermission) return Forbid();
        if (!CanExport && !CanEdit && !AppRoles.IsGlobalAdmin(User)) return Forbid();

        var employees = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new EmployeeVm
            {
                UserId = x.UserId,
                FullName = x.FullName,
                Position = x.Position,
                Email = x.Email
            })
            .ToListAsync();

        var nodeList = await _db.EmployeeOrgChartNodes
            .AsNoTracking()
            .ToListAsync();

        var rows = BuildExportRows(employees, nodeList);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Organigrama");
        ws.Cell(1, 1).Value = "Nivel";
        ws.Cell(1, 2).Value = "Empleado";
        ws.Cell(1, 3).Value = "Cargo";
        ws.Cell(1, 4).Value = "Reporta a";
        ws.Cell(1, 5).Value = "Correo";

        var r = 2;
        foreach (var row in rows)
        {
            ws.Cell(r, 1).Value = row.Level;
            ws.Cell(r, 2).Value = row.FullName;
            ws.Cell(r, 3).Value = row.Position;
            ws.Cell(r, 4).Value = row.ReportsToName;
            ws.Cell(r, 5).Value = row.Email;
            r++;
        }
        ws.Columns().AdjustToContents();
        ws.Range(1, 1, 1, 5).Style.Font.Bold = true;
        ws.Range(1, 1, 1, 5).Style.Fill.BackgroundColor = XLColor.LightBlue;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var fileName = $"organigrama_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private async Task LoadAsync()
    {
        await ResolvePermissionsAsync();

        Employees = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new EmployeeVm
            {
                UserId = x.UserId,
                FullName = x.FullName,
                Position = x.Position,
                Email = x.Email
            })
            .ToListAsync();

        Nodes = await _db.EmployeeOrgChartNodes
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Select(x => new NodeVm
            {
                UserId = x.UserId,
                ReportsToUserId = x.ReportsToUserId,
                SortOrder = x.SortOrder,
                PositionX = x.PositionX,
                PositionY = x.PositionY
            })
            .ToListAsync();
    }

    private async Task ResolvePermissionsAsync()
    {
        if (AppRoles.IsGlobalAdmin(User))
        {
            CanViewPermission = true;
            CanEdit = true;
            CanExport = true;
            return;
        }

        CanViewPermission = await _actions.HasActionAsync(User, AppActions.EmployeesOrgChartView);
        CanEdit = await _actions.HasActionAsync(User, AppActions.EmployeesOrgChartEdit);
        CanExport = await _actions.HasActionAsync(User, AppActions.EmployeesOrgChartExport);
    }

    private static List<ExportRow> BuildExportRows(List<EmployeeVm> employees, List<EmployeeOrgChartNode> nodes)
    {
        var byUser = employees.ToDictionary(x => x.UserId, x => x, StringComparer.OrdinalIgnoreCase);
        var nodeByUser = nodes.ToDictionary(x => x.UserId, x => x, StringComparer.OrdinalIgnoreCase);

        var children = new Dictionary<string, List<EmployeeOrgChartNode>>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in nodes)
        {
            var parent = string.IsNullOrWhiteSpace(n.ReportsToUserId) ? "__ROOT__" : n.ReportsToUserId!;
            if (!children.TryGetValue(parent, out var list))
            {
                list = [];
                children[parent] = list;
            }
            list.Add(n);
        }

        foreach (var kv in children)
            kv.Value.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));

        var rows = new List<ExportRow>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Walk(string parentId, int level)
        {
            if (!children.TryGetValue(parentId, out var kids)) return;
            foreach (var child in kids)
            {
                if (!visited.Add(child.UserId)) continue;
                if (!byUser.TryGetValue(child.UserId, out var emp)) continue;

                var managerName = "-";
                if (!string.IsNullOrWhiteSpace(child.ReportsToUserId)
                    && byUser.TryGetValue(child.ReportsToUserId!, out var mgr))
                    managerName = mgr.FullName;

                rows.Add(new ExportRow(level, emp.FullName, emp.Position, managerName, emp.Email));
                Walk(child.UserId, level + 1);
            }
        }

        Walk("__ROOT__", 1);

        foreach (var emp in employees.OrderBy(x => x.FullName))
        {
            if (visited.Contains(emp.UserId)) continue;
            var n = nodeByUser.TryGetValue(emp.UserId, out var node) ? node : null;
            var managerName = "-";
            if (n != null && !string.IsNullOrWhiteSpace(n.ReportsToUserId) && byUser.TryGetValue(n.ReportsToUserId!, out var mgr))
                managerName = mgr.FullName;
            rows.Add(new ExportRow(1, emp.FullName, emp.Position, managerName, emp.Email));
        }

        return rows;
    }

    public class EmployeeVm
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Position { get; set; } = "";
        public string Email { get; set; } = "";
    }

    public class NodeVm
    {
        public string UserId { get; set; } = "";
        public string? ReportsToUserId { get; set; }
        public int SortOrder { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
    }

    public record ExportRow(int Level, string FullName, string Position, string ReportsToName, string Email);
}
