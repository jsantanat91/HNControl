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

namespace HNControl.Web.Pages.Inventory;

[Authorize(Policy = "EmployeeOnly")]
public class RequestDetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public RequestDetailsModel(ApplicationDbContext db) => _db = db;

    public InventoryMovement? Anchor { get; set; }
    public List<InventoryMovement> Lines { get; set; } = new();

    public string StatusLabel { get; set; } = "-";
    public string StatusCss { get; set; } = "text-bg-light";

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        Anchor = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Include(m => m.AssignedClient)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (Anchor == null) return NotFound();

        if (!CanUserAccessOrder(Anchor, userId))
            return Forbid();

        Lines = await LoadOrderLinesAsync(Anchor);
        ComputeOrderStatus();

        return Page();
    }

    public async Task<IActionResult> OnGetExportPdfAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        Anchor = await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Project)
            .Include(m => m.AssignedClient)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (Anchor == null) return NotFound();

        if (!CanUserAccessOrder(Anchor, userId))
            return Forbid();

        Lines = await LoadOrderLinesAsync(Anchor);
        if (Lines.Count == 0) return NotFound();

        ComputeOrderStatus();

        var requestedAt = Anchor.RequestedAt.ToLocalTime();
        var typeLabel = Anchor.Type == InventoryMovementType.In ? "Entrada" : "Salida";

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
                    c.Item().Text("Detalle de solicitud de almacen").FontSize(12).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"Fecha: {requestedAt:yyyy-MM-dd HH:mm:ss}").FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(10).Column(c =>
                {
                    c.Spacing(10);

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Resumen").SemiBold();
                        cc.Item().Text($"Tipo: {typeLabel}");
                        cc.Item().Text($"Estado: {StatusLabel}");
                        cc.Item().Text($"Proyecto: {Anchor.Project?.Title ?? "-"}");
                        cc.Item().Text($"Solicito: {Anchor.RequestedByName ?? "-"}");
                        cc.Item().Text($"Responsable: {Anchor.ResponsibleName ?? "-"}");
                        cc.Item().Text($"Fecha solicitud: {requestedAt:yyyy-MM-dd HH:mm}");
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
                                h.Cell().Element(CellHead).Text("Estatus");
                            });

                            foreach (var m in Lines)
                            {
                                t.Cell().Element(CellBody).Text($"{m.Item?.Name ?? "-"}\nID: {m.Item?.ModelCode ?? "-"} - SKU: {m.Item?.Sku ?? "-"}");
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
                });

                page.Footer().AlignCenter().Text($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken2);
            });
        }).GeneratePdf();

        var file = $"solicitud_almacen_{typeLabel}_{requestedAt:yyyyMMdd_HHmmss}.pdf";
        return File(pdf, "application/pdf", file);
    }

    private void ComputeOrderStatus()
    {
        var statuses = Lines.Select(x => x.Status).Distinct().ToList();

        if (statuses.Count == 1)
        {
            (StatusLabel, StatusCss) = statuses[0] switch
            {
                InventoryMovementStatus.Pending => ("Pendiente", "text-bg-warning"),
                InventoryMovementStatus.Approved => ("Aprobado", "text-bg-success"),
                InventoryMovementStatus.Rejected => ("Rechazado", "text-bg-danger"),
                _ => ("-", "text-bg-light")
            };
            return;
        }

        if (Lines.Any(x => x.Status == InventoryMovementStatus.Pending))
        {
            StatusLabel = "Parcial (pendiente)";
            StatusCss = "text-bg-warning";
        }
        else
        {
            StatusLabel = "Parcial";
            StatusCss = "text-bg-secondary";
        }
    }

    private static IContainer CellHead(IContainer c) =>
        c.Background(Colors.Grey.Lighten4)
            .Border(1).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(4).PaddingHorizontal(6)
            .DefaultTextStyle(x => x.SemiBold().FontSize(9).FontColor(Colors.Grey.Darken2));

    private static IContainer CellBody(IContainer c) =>
        c.Border(1).BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(5).PaddingHorizontal(6);

    private static bool CanUserAccessOrder(InventoryMovement movement, string userId)
    {
        return movement.RequestedByUserId == userId || movement.ResponsibleUserId == userId;
    }

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
}
