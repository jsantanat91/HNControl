using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HNControl.Web.Services;

public class ServiceOrderPdfRenderer : IServiceOrderPdfRenderer
{
    private readonly IConfiguration _cfg;
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public ServiceOrderPdfRenderer(IConfiguration cfg, ApplicationDbContext db, IFileStorage storage)
    {
        _cfg = cfg;
        _db = db;
        _storage = storage;
    }

    public async Task<byte[]> RenderAsync(ServiceOrder order)
    {
        var o = await _db.ServiceOrders
            .Include(x => x.Client)
            .Include(x => x.Checklist)
            .Include(x => x.WorkItems)
            .Include(x => x.Evidences)
            .Include(x => x.Signatures)
            .FirstAsync(x => x.Id == order.Id);

        // Logo
        var logoPath = (_cfg["Branding:LogoPath"] ?? "wwwroot/images/hn-logo.png").Trim();
        if (!Path.IsPathRooted(logoPath))
            logoPath = Path.Combine(Directory.GetCurrentDirectory(), logoPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        byte[]? logoBytes = File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null;

        // Firmas (leer desde storage, no “a mano” con basePath)
        var techSig = o.Signatures.FirstOrDefault(s => s.Role == SignatureRole.Technician);
        var cliSig = o.Signatures.FirstOrDefault(s => s.Role == SignatureRole.Client);

        byte[]? techBytes = await TryReadStorageBytesAsync(techSig?.StoragePath);
        byte[]? cliBytes = await TryReadStorageBytesAsync(cliSig?.StoragePath);

        var company = (_cfg["Branding:CompanyName"] ?? "HN Solutions").Trim();
        var footer = (_cfg["Branding:ReportFooter"] ?? "HN Control").Trim();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(26);
                page.DefaultTextStyle(x => x.FontSize(10));

                // HEADER
                page.Header().Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(company).FontSize(16).SemiBold();
                        c.Item().Text("Orden de Servicio").FontSize(12).FontColor(Colors.Grey.Darken2);
                        c.Item().Text(o.Title).FontSize(12).SemiBold();
                    });

                    r.ConstantItem(140).AlignRight().AlignMiddle().Element(el =>
                    {
                        if (logoBytes != null && logoBytes.Length > 0)
                            el.Height(46).Image(logoBytes).FitHeight();
                        else
                            el.Text("LOGO").FontSize(14).FontColor(Colors.Grey.Darken2);
                    });
                });

                // CONTENT
                page.Content().PaddingTop(12).Column(c =>
                {
                    c.Spacing(10);

                    // Cliente + Datos
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                        {
                            cc.Item().Text("Cliente").SemiBold();
                            cc.Item().Text(o.Client?.Name ?? "—");
                            if (!string.IsNullOrWhiteSpace(o.Client?.Email))
                                cc.Item().Text(o.Client!.Email).FontColor(Colors.Grey.Darken2);
                            if (!string.IsNullOrWhiteSpace(o.Client?.Phone))
                                cc.Item().Text(o.Client!.Phone).FontColor(Colors.Grey.Darken2);
                        });

                        r.ConstantItem(255).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                        {
                            cc.Item().Text("Datos de la orden").SemiBold();
                            cc.Item().Text($"Tipo: {o.Type}");
                            cc.Item().Text($"Status: {o.Status}");
                            cc.Item().Text($"Creada: {o.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
                            if (o.EstimatedEndDate != null)
                                cc.Item().Text($"SLA: {o.EstimatedEndDate.Value:yyyy-MM-dd}");
                        });
                    });

                    // Descripción
                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Descripción").SemiBold();
                        cc.Item().PaddingTop(6).Text(string.IsNullOrWhiteSpace(o.Description) ? "—" : o.Description);
                    });

                    // Checklist (por actividad si es Global)
                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Checklist").SemiBold();
                        cc.Item().PaddingTop(6);

                        void RenderChecklistTable(List<ServiceOrderChecklistItem> list)
                        {
                            if (list.Count == 0)
                            {
                                cc.Item().Text("—").FontColor(Colors.Grey.Darken2);
                                return;
                            }

                            cc.Item().Table(t =>
                            {
                                t.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(26);
                                    cols.RelativeColumn();
                                    cols.ConstantColumn(34);
                                    cols.RelativeColumn();
                                });

                                t.Header(h =>
                                {
                                    h.Cell().Element(CellHead).Text("#");
                                    h.Cell().Element(CellHead).Text("Item");
                                    h.Cell().Element(CellHead).AlignCenter().Text("OK");
                                    h.Cell().Element(CellHead).Text("Notas");
                                });

                                for (int i = 0; i < list.Count; i++)
                                {
                                    var it = list[i];
                                    bool zebra = i % 2 == 1;

                                    t.Cell().Element(cel => CellBody(cel, zebra)).Text(it.SortOrder.ToString()).FontColor(Colors.Grey.Darken2);
                                    t.Cell().Element(cel => CellBody(cel, zebra)).Text(it.Title);
                                    t.Cell().Element(cel => CellBody(cel, zebra)).AlignCenter().Text(it.IsDone ? "✓" : "");
                                    t.Cell().Element(cel => CellBody(cel, zebra)).Text(it.Notes ?? "").FontColor(Colors.Grey.Darken2);
                                }
                            });
                        }

                        if (o.WorkItems != null && o.WorkItems.Count > 0)
                        {
                            var general = o.Checklist.Where(x => x.WorkItemId == null).OrderBy(x => x.SortOrder).ToList();
                            if (general.Count > 0)
                            {
                                cc.Item().Text("Checklist general").SemiBold();
                                RenderChecklistTable(general);
                            }

                            foreach (var w in o.WorkItems.OrderBy(x => x.SortOrder))
                            {
                                var items = o.Checklist.Where(x => x.WorkItemId == w.Id).OrderBy(x => x.SortOrder).ToList();
                                if (items.Count == 0) continue;

                                cc.Item().PaddingTop(8).Text($"{w.SortOrder + 1}. {w.Title} · {w.Type}").SemiBold();

                                if (!string.IsNullOrWhiteSpace(w.WorkPerformed))
                                    cc.Item().Text($"Trabajo: {w.WorkPerformed}").FontColor(Colors.Grey.Darken2);
                                if (!string.IsNullOrWhiteSpace(w.MaterialsUsed))
                                    cc.Item().Text($"Material: {w.MaterialsUsed}").FontColor(Colors.Grey.Darken2);
                                if (!string.IsNullOrWhiteSpace(w.TechnicianNotes))
                                    cc.Item().Text($"Obs: {w.TechnicianNotes}").FontColor(Colors.Grey.Darken2);

                                RenderChecklistTable(items);
                            }
                        }
                        else
                        {
                            RenderChecklistTable(o.Checklist.OrderBy(x => x.SortOrder).ToList());
                        }
                    });

                    // Evidencias
                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Evidencias").SemiBold();
                        cc.Item().PaddingTop(6);

                        if (!o.Evidences.Any())
                        {
                            cc.Item().Text("Sin evidencias.").FontColor(Colors.Grey.Darken2);
                        }
                        else
                        {
                            foreach (var ev in o.Evidences.OrderByDescending(x => x.UploadedAt))
                                cc.Item().Text($"• {ev.OriginalFileName} — {ev.UploadedAt.ToLocalTime():yyyy-MM-dd HH:mm}")
                                    .FontColor(Colors.Grey.Darken2);
                        }
                    });

                    // Firmas (FIX layout)
                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Firmas").SemiBold();
                        cc.Item().PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(x =>
                            {
                                x.Item().Text("Técnico").SemiBold();
                                x.Item().Text(string.IsNullOrWhiteSpace(techSig?.SignedByName) ? "—" : techSig!.SignedByName).FontColor(Colors.Grey.Darken2);
                                if (techSig?.SignedAt != null)
                                    x.Item().Text(techSig.SignedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")).FontSize(9).FontColor(Colors.Grey.Darken2);

                                x.Item().PaddingTop(6)
                                    .Height(90)
                                    .Border(1).BorderColor(Colors.Grey.Lighten2)
                                    .Background(Colors.Grey.Lighten5)
                                    .AlignCenter().AlignMiddle()
                                    .Element(el =>
                                    {
                                        if (techBytes != null && techBytes.Length > 0)
                                            el.Image(techBytes).FitArea();
                                        else
                                            el.Text("Sin firma").FontColor(Colors.Grey.Darken2);
                                    });
                            });

                            r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(x =>
                            {
                                x.Item().Text("Cliente").SemiBold();
                                x.Item().Text(string.IsNullOrWhiteSpace(cliSig?.SignedByName) ? "—" : cliSig!.SignedByName).FontColor(Colors.Grey.Darken2);
                                if (cliSig?.SignedAt != null)
                                    x.Item().Text(cliSig.SignedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")).FontSize(9).FontColor(Colors.Grey.Darken2);

                                x.Item().PaddingTop(6)
                                    .Height(90)
                                    .Border(1).BorderColor(Colors.Grey.Lighten2)
                                    .Background(Colors.Grey.Lighten5)
                                    .AlignCenter().AlignMiddle()
                                    .Element(el =>
                                    {
                                        if (cliBytes != null && cliBytes.Length > 0)
                                            el.Image(cliBytes).FitArea();
                                        else
                                            el.Text("Sin firma").FontColor(Colors.Grey.Darken2);
                                    });
                            });
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(o.AdminReviewNotes))
                    {
                        c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                        {
                            cc.Item().Text("Notas de revisión").SemiBold();
                            cc.Item().PaddingTop(6).Text(o.AdminReviewNotes!).FontColor(Colors.Grey.Darken2);
                        });
                    }
                });

                // FOOTER
                page.Footer().AlignCenter().Text($"{footer} · {DateTime.Now:yyyy-MM-dd HH:mm}")
                    .FontSize(9).FontColor(Colors.Grey.Darken2);
            });
        });

        return doc.GeneratePdf();
    }

    private static IContainer CellHead(IContainer c) =>
        c.Background(Colors.Grey.Lighten4)
         .Border(1).BorderColor(Colors.Grey.Lighten2)
         .PaddingVertical(4).PaddingHorizontal(6)
         .DefaultTextStyle(x => x.SemiBold().FontSize(9).FontColor(Colors.Grey.Darken2));

    private static IContainer CellBody(IContainer c, bool zebra) =>
        c.Background(zebra ? Colors.Grey.Lighten5 : Colors.White)
         .Border(1).BorderColor(Colors.Grey.Lighten3)
         .PaddingVertical(5).PaddingHorizontal(6);

    private async Task<byte[]?> TryReadStorageBytesAsync(string? storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return null;

        try
        {
            var (stream, _, _) = await _storage.OpenAsync(storagePath, "file.bin");
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
