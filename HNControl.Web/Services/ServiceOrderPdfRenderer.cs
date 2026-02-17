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

    public ServiceOrderPdfRenderer(IConfiguration cfg, ApplicationDbContext db)
    {
        _cfg = cfg;
        _db = db;
    }

    public async Task<byte[]> RenderAsync(ServiceOrder order)
    {
        // Recargar completo para PDF
        var o = await _db.ServiceOrders
            .Include(x => x.Client)
            .Include(x => x.Checklist)
            .Include(x => x.Evidences)
            .Include(x => x.Signatures)
            .FirstAsync(x => x.Id == order.Id);

        var logoPath = _cfg["Branding:LogoPath"] ?? "wwwroot/images/hn-logo.png";

        var techSig = o.Signatures.FirstOrDefault(s => s.Role == SignatureRole.Technician);
        var cliSig = o.Signatures.FirstOrDefault(s => s.Role == SignatureRole.Client);

        var basePath = _cfg["Storage:BasePath"] ?? "App_Data/uploads";

        byte[]? techBytes = TryReadFile(basePath, techSig?.StoragePath);
        byte[]? cliBytes = TryReadFile(basePath, cliSig?.StoragePath);
        byte[]? logoBytes = File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(_cfg["Branding:CompanyName"] ?? "HN Solutions").FontSize(14).SemiBold();
                        c.Item().Text("Orden de Servicio").FontSize(12);
                        c.Item().Text($"{o.Title}").FontSize(11).SemiBold();
                    });

                    r.ConstantItem(120).AlignRight().AlignMiddle().Element(el =>
                    {
                        if (logoBytes != null)
                            el.Height(48).Image(logoBytes);
                        else
                            el.Text("LOGO").FontSize(16).Light();
                    });
                });

                page.Content().Column(c =>
                {
                    c.Item().PaddingTop(10).Row(r =>
                    {
                        r.RelativeItem().Border(1).Padding(8).Column(cc =>
                        {
                            cc.Item().Text("Cliente").SemiBold();
                            cc.Item().Text(o.Client?.Name ?? "");
                            cc.Item().Text(o.Client?.Email ?? "").FontColor(Colors.Grey.Darken2);
                            cc.Item().Text(o.Client?.Phone ?? "").FontColor(Colors.Grey.Darken2);
                        });

                        r.ConstantItem(220).Border(1).Padding(8).Column(cc =>
                        {
                            cc.Item().Text("Datos de la orden").SemiBold();
                            cc.Item().Text($"Tipo: {o.Type}");
                            cc.Item().Text($"Status: {o.Status}");
                            cc.Item().Text($"Creada: {o.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
                            if (o.EstimatedEndDate != null)
                                cc.Item().Text($"SLA: {o.EstimatedEndDate.Value:yyyy-MM-dd}");
                        });
                    });

                    c.Item().PaddingTop(10).Text("Descripción").SemiBold();
                    c.Item().Border(1).Padding(8).Text(o.Description ?? "");

                    c.Item().PaddingTop(10).Text("Checklist").SemiBold();
                    c.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(30);
                            cols.RelativeColumn();
                            cols.ConstantColumn(60);
                            cols.RelativeColumn();
                        });

                        t.Header(h =>
                        {
                            h.Cell().Element(CellHead).Text("#");
                            h.Cell().Element(CellHead).Text("Item");
                            h.Cell().Element(CellHead).AlignCenter().Text("Hecho");
                            h.Cell().Element(CellHead).Text("Notas");
                        });

                        foreach (var it in o.Checklist.OrderBy(x => x.SortOrder))
                        {
                            t.Cell().Element(CellBody).Text(it.SortOrder.ToString());
                            t.Cell().Element(CellBody).Text(it.Title);
                            t.Cell().Element(CellBody).AlignCenter().Text(it.IsDone ? "Sí" : "No");
                            t.Cell().Element(CellBody).Text(it.Notes ?? "");
                        }
                    });

                    c.Item().PaddingTop(10).Text("Evidencias").SemiBold();
                    if (!o.Evidences.Any())
                    {
                        c.Item().Text("Sin evidencias.");
                    }
                    else
                    {
                        foreach (var ev in o.Evidences.OrderByDescending(x => x.UploadedAt))
                            c.Item().Text($"• {ev.OriginalFileName} ({ev.UploadedAt.ToLocalTime():yyyy-MM-dd HH:mm})");
                    }

                    c.Item().PaddingTop(15).Row(r =>
                    {
                        r.RelativeItem().Border(1).Padding(8).Column(cc =>
                        {
                            cc.Item().Text("Firma técnico").SemiBold();
                            cc.Item().Text(techSig?.SignedByName ?? "—").FontColor(Colors.Grey.Darken2);
                            cc.Item().Text(techSig?.SignedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "").FontColor(Colors.Grey.Darken2);
                            cc.Item().PaddingTop(5).Height(80).Element(el =>
                            {
                                if (techBytes != null) el.Image(techBytes);
                                else el.Text("Sin firma");
                            });
                        });

                        r.RelativeItem().Border(1).Padding(8).Column(cc =>
                        {
                            cc.Item().Text("Firma cliente").SemiBold();
                            cc.Item().Text(cliSig?.SignedByName ?? "—").FontColor(Colors.Grey.Darken2);
                            cc.Item().Text(cliSig?.SignedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "").FontColor(Colors.Grey.Darken2);
                            cc.Item().PaddingTop(5).Height(80).Element(el =>
                            {
                                if (cliBytes != null) el.Image(cliBytes);
                                else el.Text("Sin firma");
                            });
                        });
                    });
                });

                page.Footer().AlignCenter().Text(_cfg["Branding:ReportFooter"] ?? "HN Control").FontSize(9).FontColor(Colors.Grey.Darken2);
            });
        });

        return doc.GeneratePdf();
    }

    private static IContainer CellHead(IContainer c) =>
        c.DefaultTextStyle(x => x.SemiBold()).Background(Colors.Grey.Lighten3).Padding(4).Border(1);

    private static IContainer CellBody(IContainer c) =>
        c.Padding(4).Border(1);

    private static byte[]? TryReadFile(string basePath, string? storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return null;
        var full = Path.Combine(basePath, storagePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        return File.Exists(full) ? File.ReadAllBytes(full) : null;
    }
}
