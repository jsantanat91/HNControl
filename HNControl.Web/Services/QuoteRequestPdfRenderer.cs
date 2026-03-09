using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HNControl.Web.Services;

public class QuoteRequestPdfRenderer : IQuoteRequestPdfRenderer
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _cfg;

    public QuoteRequestPdfRenderer(ApplicationDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    public async Task<byte[]> RenderAsync(QuoteRequest request)
    {
        var q = await _db.QuoteRequests
            .Include(x => x.Lines)
            .FirstAsync(x => x.Id == request.Id);

        var company = (_cfg["Branding:CompanyName"] ?? "HN Solutions").Trim();
        var logoPath = (_cfg["Branding:LogoPath"] ?? string.Empty).Trim();
        byte[]? logo = null;
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            logo = await File.ReadAllBytesAsync(logoPath);

        var doc = Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(24);
                p.DefaultTextStyle(x => x.FontSize(10));

                p.Header().Row(r =>
                {
                    r.RelativeItem().Column(col =>
                    {
                        col.Item().Text(company).FontSize(15).SemiBold();
                        col.Item().Text("Cotizacion a la medida").FontSize(12).FontColor(Colors.Grey.Darken2);
                        col.Item().Text($"Folio: {q.Folio}").SemiBold();
                    });
                    r.ConstantItem(150).AlignRight().AlignMiddle().Element(e =>
                    {
                        if (logo is { Length: > 0 })
                            e.Height(52).Width(150).Image(logo).FitArea();
                        else
                            e.Text("HN").FontSize(16).Bold();
                    });
                });

                p.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(x =>
                    {
                        x.Item().Text("Datos del cliente").SemiBold();
                        x.Item().Text($"Nombre: {q.CustomerName}");
                        x.Item().Text($"Correo: {q.CustomerEmail}");
                        x.Item().Text($"Telefono: {q.CustomerPhone}");
                        x.Item().Text($"Ubicacion: {q.CustomerLocation}");
                        x.Item().Text($"Empresa: {(string.IsNullOrWhiteSpace(q.CompanyName) ? "-" : q.CompanyName)}");
                        x.Item().Text($"Segmento: {LabelSegment(q.Segment)}");
                        if (!string.IsNullOrWhiteSpace(q.Notes))
                            x.Item().Text($"Comentarios: {q.Notes}");
                    });

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(x =>
                    {
                        x.Item().Text("Conceptos seleccionados").SemiBold();
                        x.Item().PaddingTop(6).Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(1.1f);
                                cd.RelativeColumn(1.1f);
                                cd.RelativeColumn(1.1f);
                                cd.ConstantColumn(40);
                                cd.ConstantColumn(72);
                                cd.ConstantColumn(82);
                            });
                            t.Header(h =>
                            {
                                h.Cell().Element(CellHead).Text("Categoria");
                                h.Cell().Element(CellHead).Text("Servicio");
                                h.Cell().Element(CellHead).Text("Subproducto");
                                h.Cell().Element(CellHead).AlignCenter().Text("Cant");
                                h.Cell().Element(CellHead).AlignRight().Text("Costo");
                                h.Cell().Element(CellHead).AlignRight().Text("Total");
                            });

                            foreach (var line in q.Lines)
                            {
                                t.Cell().Element(CellBody).Text(line.CategoryName);
                                t.Cell().Element(CellBody).Text(line.ServiceName);
                                t.Cell().Element(CellBody).Text(line.SubproductName ?? "-");
                                t.Cell().Element(CellBody).AlignCenter().Text(line.Quantity.ToString());
                                t.Cell().Element(CellBody).AlignRight()
                                    .Text(line.IsManualPrice ? "Manual" : Money(line.UnitPrice));
                                t.Cell().Element(CellBody).AlignRight()
                                    .Text(line.IsManualPrice ? "Por validar" : Money(line.LineTotal));
                            }
                        });
                    });

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(r =>
                    {
                        r.RelativeItem().Column(x =>
                        {
                            x.Item().Text("Notas de cotizacion").SemiBold();
                            x.Item().Text("Los conceptos marcados como manuales requieren validacion comercial.");
                            x.Item().Text("La cotizacion puede ajustarse despues de visita tecnica.");
                        });
                        r.ConstantItem(190).Column(x =>
                        {
                            x.Item().AlignRight().Text($"Subtotal automatico: {Money(q.SubtotalAuto)}");
                            x.Item().AlignRight().Text($"Conceptos manuales: {q.ManualItemsCount}");
                            x.Item().AlignRight().Text($"Total estimado: {Money(q.EstimatedTotal)}").SemiBold();
                        });
                    });
                });

                p.Footer().AlignCenter().Text($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm} · {company}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

        return doc.GeneratePdf();
    }

    private static IContainer CellHead(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5);

    private static IContainer CellBody(IContainer c) =>
        c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5);

    private static string LabelSegment(QuoteSegment s) => s == QuoteSegment.Business ? "Empresarial" : "Residencial";

    private static string Money(decimal? v) => (v ?? 0m).ToString("C2");
}
