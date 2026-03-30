using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HNControl.Web.Pages.Admin.Inventory.Approvals;

[Authorize(Policy = "InventorySupervisor")]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DetailsModel(ApplicationDbContext db) => _db = db;

    public InventoryMovement? Anchor { get; set; }
    public List<InventoryMovement> Lines { get; set; } = new();

    public bool CanDecide { get; set; }
    public bool HasMixedStatuses { get; set; }

    [BindProperty]
    public DecisionInput Input { get; set; } = new();

    public class DecisionInput
    {
        [MaxLength(2000)]
        public string? AdminNote { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Anchor = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Include(m => m.AssignedClient)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (Anchor == null) return NotFound();

        Lines = await LoadOrderLinesAsync(Anchor);

        var statuses = Lines.Select(x => x.Status).Distinct().ToList();
        HasMixedStatuses = statuses.Count > 1;
        CanDecide = Lines.Count > 0 && Lines.All(x => x.Status == InventoryMovementStatus.Pending);

        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        if (!ModelState.IsValid) return await OnGetAsync(id);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var prof = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        var adminName = prof?.FullName ?? (User.Identity?.Name ?? "");

        // 1) ancla (sin tracking)
        var anchor = await _db.InventoryMovements
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (anchor == null) return NotFound();

        using var tx = await _db.Database.BeginTransactionAsync();

        // 2) cargar líneas con tracking
        var lines = await LoadOrderLinesTrackedAsync(anchor);

        if (lines.Count == 0) return NotFound();

        if (lines.Any(x => x.Status != InventoryMovementStatus.Pending))
        {
            ModelState.AddModelError(string.Empty, "Esta orden ya fue procesada (total o parcialmente). Para mantener consistencia, no se puede aprobar por orden.");
            await tx.RollbackAsync();
            return await OnGetAsync(id);
        }

        // 3) validación y actualización de stock
        if (lines.First().Type == InventoryMovementType.Out)
        {
            var byItem = lines.GroupBy(x => x.ItemId)
                .Select(g => new { ItemId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToList();

            foreach (var g in byItem)
            {
                var anyLine = lines.FirstOrDefault(x => x.ItemId == g.ItemId);
                var item = anyLine?.Item;
                if (item == null) continue;

                if (item.QuantityOnHand < g.Qty)
                {
                    ModelState.AddModelError(string.Empty, $"Stock insuficiente para '{item.Name}': Existencia {item.QuantityOnHand} {item.Unit}, requerido {g.Qty}.");
                    await tx.RollbackAsync();
                    return await OnGetAsync(id);
                }
            }

            foreach (var g in byItem)
            {
                var item = lines.First(x => x.ItemId == g.ItemId).Item!;
                item.QuantityOnHand -= g.Qty;
                item.UpdatedAt = DateTime.UtcNow;
            }
        }
        else
        {
            var byItem = lines.GroupBy(x => x.ItemId)
                .Select(g => new { ItemId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToList();

            foreach (var g in byItem)
            {
                var item = lines.First(x => x.ItemId == g.ItemId).Item;
                if (item == null) continue;
                item.QuantityOnHand += g.Qty;
                item.UpdatedAt = DateTime.UtcNow;
            }
        }

        // 4) aprobar líneas
        var note = (Input.AdminNote ?? "").Trim();
        var now = DateTime.UtcNow;

        foreach (var mov in lines)
        {
            mov.Status = InventoryMovementStatus.Approved;
            mov.ApprovedAt = now;
            mov.ApprovedByUserId = userId;
            mov.ApprovedByName = adminName;
            mov.AdminNote = note;
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return RedirectToPage("./Index");
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        if (!ModelState.IsValid) return await OnGetAsync(id);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var prof = await _db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        var adminName = prof?.FullName ?? (User.Identity?.Name ?? "");

        var anchor = await _db.InventoryMovements
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (anchor == null) return NotFound();

        var lines = await LoadOrderLinesTrackedAsync(anchor);
        if (lines.Count == 0) return NotFound();

        if (lines.Any(x => x.Status != InventoryMovementStatus.Pending))
        {
            ModelState.AddModelError(string.Empty, "Esta orden ya fue procesada (total o parcialmente). Para mantener consistencia, no se puede rechazar por orden.");
            return await OnGetAsync(id);
        }

        var note = (Input.AdminNote ?? "").Trim();
        var now = DateTime.UtcNow;

        foreach (var mov in lines)
        {
            mov.Status = InventoryMovementStatus.Rejected;
            mov.ApprovedAt = now;
            mov.ApprovedByUserId = userId;
            mov.ApprovedByName = adminName;
            mov.AdminNote = note;
        }

        await _db.SaveChangesAsync();
        return RedirectToPage("./Index");
    }

    public async Task<IActionResult> OnGetExportPdfAsync(Guid id)
    {
        Anchor = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Include(m => m.AssignedClient)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (Anchor == null) return NotFound();

        Lines = await LoadOrderLinesAsync(Anchor);
        if (Lines.Count == 0) return NotFound();

        var requestedAt = Anchor.RequestedAt.ToLocalTime();
        var typeLabel = Anchor.Type == InventoryMovementType.In ? "Entrada" : "Salida";
        var statusLabel = Anchor.Status switch
        {
            InventoryMovementStatus.Pending => "Pendiente",
            InventoryMovementStatus.Approved => "Aprobada",
            InventoryMovementStatus.Rejected => "Rechazada",
            _ => "Pendiente"
        };

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(c =>
                {
                    c.Item().Text("HN Control").FontSize(16).SemiBold();
                    c.Item().Text("Orden de almacen").FontSize(12).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"Fecha: {requestedAt:yyyy-MM-dd HH:mm:ss}").FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(10).Column(c =>
                {
                    c.Spacing(10);

                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                        {
                            cc.Item().Text("Resumen").SemiBold();
                            cc.Item().Text($"Tipo: {typeLabel}");
                            cc.Item().Text($"Estado: {statusLabel}");
                            cc.Item().Text($"Proyecto: {Anchor.Project?.Title ?? "-"}");
                            cc.Item().Text($"Solicito: {Anchor.RequestedByName}");
                            cc.Item().Text($"Responsable: {Anchor.ResponsibleName}");
                            cc.Item().Text($"Aprobo: {Anchor.ApprovedByName ?? "-"}");
                        });
                    });

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Detalle de lineas").SemiBold();
                        cc.Item().PaddingTop(6).Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(1);
                            });

                            t.Header(h =>
                            {
                                h.Cell().Element(CellHead).Text("Item");
                                h.Cell().Element(CellHead).Text("Cantidad");
                                h.Cell().Element(CellHead).Text("Cliente");
                                h.Cell().Element(CellHead).Text("Status");
                            });

                            foreach (var m in Lines)
                            {
                                t.Cell().Element(CellBody).Text($"{m.Item?.Name ?? "-"}\nID: {m.Item?.ModelCode ?? "-"} · SKU: {m.Item?.Sku ?? "-"}");
                                t.Cell().Element(CellBody).Text($"{m.Quantity} {m.Item?.Unit}");
                                t.Cell().Element(CellBody).Text(m.AssignedClient?.Name ?? "-");
                                t.Cell().Element(CellBody).Text(m.Status switch
                                {
                                    InventoryMovementStatus.Pending => "Pendiente",
                                    InventoryMovementStatus.Approved => "Aprobado",
                                    InventoryMovementStatus.Rejected => "Rechazado",
                                    _ => "-"
                                });
                            }
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(Anchor.AdminNote))
                    {
                        c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                        {
                            cc.Item().Text("Nota de autorizacion").SemiBold();
                            cc.Item().PaddingTop(5).Text(Anchor.AdminNote);
                        });
                    }
                });

                page.Footer().AlignCenter().Text($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken2);
            });
        }).GeneratePdf();

        var file = $"almacen_{typeLabel}_{requestedAt:yyyyMMdd_HHmmss}.pdf";
        return File(pdf, "application/pdf", file);
    }

    private static IContainer CellHead(IContainer c) =>
        c.Background(Colors.Grey.Lighten4)
         .Border(1).BorderColor(Colors.Grey.Lighten2)
         .PaddingVertical(4).PaddingHorizontal(6)
         .DefaultTextStyle(x => x.SemiBold().FontSize(9).FontColor(Colors.Grey.Darken2));

    private static IContainer CellBody(IContainer c) =>
        c.Border(1).BorderColor(Colors.Grey.Lighten3)
         .PaddingVertical(5).PaddingHorizontal(6);

    private async Task<List<InventoryMovement>> LoadOrderLinesAsync(InventoryMovement anchor)
    {
        return await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Include(m => m.AssignedClient)
            .Where(m =>
                m.RequestedAt == anchor.RequestedAt &&
                m.RequestedByUserId == anchor.RequestedByUserId &&
                m.Type == anchor.Type &&
                m.ProjectId == anchor.ProjectId &&
                m.ResponsibleUserId == anchor.ResponsibleUserId)
            .OrderBy(m => m.Item!.Name)
            .ThenBy(m => m.Item!.Sku)
            .ToListAsync();
    }

    private async Task<List<InventoryMovement>> LoadOrderLinesTrackedAsync(InventoryMovement anchor)
    {
        return await _db.InventoryMovements
            .Include(m => m.Item)
            .Where(m =>
                m.RequestedAt == anchor.RequestedAt &&
                m.RequestedByUserId == anchor.RequestedByUserId &&
                m.Type == anchor.Type &&
                m.ProjectId == anchor.ProjectId &&
                m.ResponsibleUserId == anchor.ResponsibleUserId)
            .ToListAsync();
    }
}


