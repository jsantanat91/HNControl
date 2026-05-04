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
    private readonly IFileStorage _storage;

    public QuoteRequestPdfRenderer(ApplicationDbContext db, IConfiguration cfg, IFileStorage storage)
    {
        _db = db;
        _cfg = cfg;
        _storage = storage;
    }

    public async Task<byte[]> RenderAsync(QuoteRequest request)
    {
        // Soporte para render persistido (con Id) y preview (sin guardar en DB).
        QuoteRequest q;
        if (request.Lines.Count > 0)
        {
            q = request;
        }
        else
        {
            q = await _db.QuoteRequests
                .Include(x => x.Lines)
                .FirstAsync(x => x.Id == request.Id);
        }

        // Query only stable columns so older DB schemas (without new optional
        // MercadoPago protected fields) do not break quote PDF generation.
        var sys = await _db.SystemConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new
            {
                x.CompanyName,
                x.CompanyLegalName,
                x.CompanyLogoStoragePath
            })
            .FirstOrDefaultAsync();

        var company = (sys?.CompanyName ?? _cfg["Branding:CompanyName"] ?? "HN Solutions").Trim();
        var companyLegal = string.IsNullOrWhiteSpace(sys?.CompanyLegalName) ? company : sys!.CompanyLegalName.Trim();
        var logo = await TryReadStorageBytesAsync(sys?.CompanyLogoStoragePath);

        var created = q.CreatedAt == default ? DateTime.UtcNow : q.CreatedAt;

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
                        col.Item().Text(companyLegal).FontSize(15).SemiBold();
                        col.Item().Text(company).FontSize(10).FontColor(Colors.Grey.Darken1);
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

                    // Bloque estilo similar a Orden: datos cliente + meta.
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(x =>
                        {
                            x.Item().Text("Cliente").SemiBold();
                            x.Item().Text(q.CustomerName);
                            x.Item().Text(q.CustomerEmail).FontColor(Colors.Grey.Darken2);
                            x.Item().Text(q.CustomerPhone).FontColor(Colors.Grey.Darken2);
                            x.Item().Text(q.CustomerLocation).FontColor(Colors.Grey.Darken2);
                            if (!string.IsNullOrWhiteSpace(q.CompanyName))
                                x.Item().Text($"Empresa: {q.CompanyName}").FontColor(Colors.Grey.Darken2);
                        });

                        row.ConstantItem(260).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(x =>
                        {
                            x.Item().Text("Datos de cotizacion").SemiBold();
                            x.Item().Text($"Segmento: {LabelSegment(q.Segment)}");
                            x.Item().Text($"Estatus: {LabelStatus(q.Status)}");
                            x.Item().Text($"Fecha: {created.ToLocalTime():yyyy-MM-dd HH:mm}");
                            if (q.ManualItemsCount > 0)
                                x.Item().Text($"Conceptos manuales: {q.ManualItemsCount}");
                        });
                    });

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(x =>
                    {
                        x.Item().Text("Detalle de cotizacion").SemiBold();
                        x.Item().PaddingTop(6).Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(1.1f);
                                cd.RelativeColumn(1.1f);
                                cd.RelativeColumn(1f);
                                cd.ConstantColumn(40);
                                cd.ConstantColumn(65);
                                cd.ConstantColumn(70);
                                cd.ConstantColumn(72);
                                cd.ConstantColumn(82);
                            });
                            t.Header(h =>
                            {
                                h.Cell().Element(CellHead).Text("Categoria");
                                h.Cell().Element(CellHead).Text("Servicio");
                                h.Cell().Element(CellHead).Text("Subproducto");
                                h.Cell().Element(CellHead).AlignCenter().Text("Cant");
                                h.Cell().Element(CellHead).AlignCenter().Text("Recurr.");
                                h.Cell().Element(CellHead).AlignCenter().Text("Modalidad");
                                h.Cell().Element(CellHead).AlignRight().Text("Costo unit. (sin IVA)");
                                h.Cell().Element(CellHead).AlignRight().Text("Subtotal (sin IVA)");
                            });

                            foreach (var line in q.Lines)
                            {
                                t.Cell().Element(CellBody).Text(line.CategoryName);
                                t.Cell().Element(CellBody).Text(line.ServiceName);
                                t.Cell().Element(CellBody).Text(line.SubproductName ?? "-");
                                t.Cell().Element(CellBody).AlignCenter().Text(line.Quantity.ToString());
                                t.Cell().Element(CellBody).AlignCenter().Text(string.IsNullOrWhiteSpace(line.Recurrence) ? "Unica" : line.Recurrence);
                                t.Cell().Element(CellBody).AlignCenter().Text(LabelOffer(line.OfferType));
                                t.Cell().Element(CellBody).AlignRight()
                                    .Text(line.IsManualPrice
                                        ? "Manual"
                                        : (line.PriceIncludesVat
                                            ? Money(((line.UnitPrice ?? 0m) / 1.16m))
                                            : Money(line.UnitPrice)));
                                t.Cell().Element(CellBody).AlignRight()
                                    .Text(line.IsManualPrice
                                        ? "Por validar"
                                        : Money(line.BaseAmount ?? 0m));
                            }
                        });
                    });

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(r =>
                    {
                        r.RelativeItem().Column(x =>
                        {
                            x.Item().Text("Notas").SemiBold();
                            x.Item().Text(string.IsNullOrWhiteSpace(q.Notes)
                                ? "La cotizacion puede ajustarse despues de visita tecnica."
                                : q.Notes);
                            if (q.ContractTermMonths.HasValue)
                                x.Item().PaddingTop(4).Text($"Tiempo de contrato: {q.ContractTermMonths} meses").SemiBold();
                            if (!string.IsNullOrWhiteSpace(q.GeneralTerms))
                            {
                                x.Item().PaddingTop(6).Text("Condiciones generales").SemiBold();
                                x.Item().Text(q.GeneralTerms);
                            }
                        });
                        r.ConstantItem(190).Column(x =>
                        {
                            x.Item().AlignRight().Text($"Subtotal sin IVA: {Money(q.SubtotalBeforeVat)}");
                            x.Item().AlignRight().Text($"IVA 16%: {Money(q.VatAmount)}");
                            x.Item().AlignRight().Text($"Total estimado: {Money(q.EstimatedTotal)}").SemiBold();
                        });
                    });
                });

                p.Footer().AlignCenter().Text($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm} · {companyLegal}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

        return doc.GeneratePdf();
    }

    private static IContainer CellHead(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5);

    private static IContainer CellBody(IContainer c) =>
        c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5);

    private static string LabelSegment(QuoteSegment s) => s switch
    {
        QuoteSegment.Business => "Empresarial",
        QuoteSegment.Events => "Eventos",
        _ => "Residencial"
    };

    private static string LabelStatus(QuoteRequestStatus s) => s switch
    {
        QuoteRequestStatus.New => "Nueva",
        QuoteRequestStatus.Emailed => "Enviada",
        QuoteRequestStatus.EmailError => "Error de envio",
        QuoteRequestStatus.Accepted => "Aceptada",
        QuoteRequestStatus.Rejected => "Rechazada",
        _ => s.ToString()
    };

    private static string LabelOffer(QuoteOfferType x) => x switch
    {
        QuoteOfferType.Sale => "Venta",
        QuoteOfferType.MonthlyRent => "Renta",
        QuoteOfferType.Lease => "Arrendam.",
        _ => x.ToString()
    };

    private static string Money(decimal? v) => (v ?? 0m).ToString("C2");

    private async Task<byte[]?> TryReadStorageBytesAsync(string? storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return null;
        try
        {
            var (stream, _, _) = await _storage.OpenAsync(storagePath, "logo");
            await using (stream)
            await using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                return ms.ToArray();
            }
        }
        catch
        {
            return null;
        }
    }
}
