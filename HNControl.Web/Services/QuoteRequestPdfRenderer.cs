using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HNControl.Web.Services;

public class QuoteRequestPdfRenderer : IQuoteRequestPdfRenderer
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly IFileStorage _storage;
    private readonly IWebHostEnvironment _env;

    public QuoteRequestPdfRenderer(ApplicationDbContext db, IConfiguration cfg, IFileStorage storage, IWebHostEnvironment env)
    {
        _db = db;
        _cfg = cfg;
        _storage = storage;
        _env = env;
    }

    public async Task<byte[]> RenderAsync(QuoteRequest request)
    {
        QuoteRequest q;
        if (request.Lines.Count > 0)
            q = request;
        else
            q = await _db.QuoteRequests
                .Include(x => x.Lines)
                .FirstAsync(x => x.Id == request.Id);

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
        var website = (_cfg["Branding:Website"] ?? "www.hubnet-solutions.net").Trim();
        var logo = await TryReadStorageBytesAsync(sys?.CompanyLogoStoragePath);

        var created = q.CreatedAt == default ? DateTime.UtcNow : q.CreatedAt;

        var quotePdf = Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(20);
                p.DefaultTextStyle(x => x.FontSize(10));

                p.Header().Column(head =>
                {
                    head.Item().Row(r =>
                    {
                        r.RelativeItem(1).Column(col =>
                        {
                            if (logo is { Length: > 0 })
                                col.Item().Height(62).Image(logo).FitHeight();
                            else
                                col.Item().Text(company).FontSize(16).SemiBold();
                        });

                        r.RelativeItem(2).AlignRight().Column(col =>
                        {
                            col.Item().Text(companyLegal).SemiBold().AlignRight();
                            col.Item().Text(website).FontSize(10).FontColor(Colors.Grey.Darken2).AlignRight();
                            col.Item().PaddingTop(8).Text("ESTIMACION").FontSize(10).FontColor(Colors.Grey.Darken2).AlignRight();
                            col.Item().PaddingTop(6).Text($"Estimacion#  {q.Folio}").SemiBold().AlignRight();
                            col.Item().Text($"Fecha de estimacion  {created:dd MMM yyyy}").AlignRight();
                        });
                    });

                    head.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                p.Content().PaddingTop(8).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(x =>
                        {
                            x.Item().Text("Receptor").FontSize(10).FontColor(Colors.Grey.Darken2);
                            x.Item().Text(string.IsNullOrWhiteSpace(q.CompanyName) ? q.CustomerName : q.CompanyName).SemiBold().FontSize(12);
                            if (!string.IsNullOrWhiteSpace(q.CompanyName) && !string.Equals(q.CustomerName, q.CompanyName, StringComparison.OrdinalIgnoreCase))
                                x.Item().Text(q.CustomerName).FontColor(Colors.Grey.Darken2);
                            x.Item().Text(q.CustomerLocation).FontColor(Colors.Grey.Darken2);
                            x.Item().Text($"{q.CustomerEmail} · {q.CustomerPhone}").FontColor(Colors.Grey.Darken2);
                        });
                    });

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(x =>
                    {
                        x.Item().Text("Detalle de cotizacion").SemiBold();
                        x.Item().PaddingTop(6).Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.ConstantColumn(24);
                                cd.RelativeColumn(2.1f);
                                cd.ConstantColumn(55);
                                cd.ConstantColumn(90);
                                cd.ConstantColumn(90);
                            });

                            t.Header(h =>
                            {
                                h.Cell().Element(CellHead).AlignCenter().Text("#");
                                h.Cell().Element(CellHead).Text("Descripcion");
                                h.Cell().Element(CellHead).AlignCenter().Text("Cantidad");
                                h.Cell().Element(CellHead).AlignRight().Text("Costo");
                                h.Cell().Element(CellHead).AlignRight().Text("Total");
                            });

                            var index = 0;
                            foreach (var line in q.Lines)
                            {
                                index++;
                                var qty = line.Quantity <= 0 ? 1 : line.Quantity;
                                var unitNoVat = line.PriceIncludesVat ? ((line.UnitPrice ?? 0m) / 1.16m) : (line.UnitPrice ?? 0m);
                                var subtotalNoVat = line.IsManualPrice
                                    ? Math.Round((line.UnitPrice ?? 0m) * qty, 2)
                                    : (line.BaseAmount ?? Math.Round(unitNoVat * qty, 2));

                                var desc = line.ServiceName;
                                if (!string.IsNullOrWhiteSpace(line.SubproductName))
                                    desc += $" ({line.SubproductName})";
                                if (!string.IsNullOrWhiteSpace(line.Description) && !string.Equals(line.Description.Trim(), line.ServiceName.Trim(), StringComparison.OrdinalIgnoreCase))
                                    desc += $"\n{line.Description}";
                                desc += $"\n{LabelSegment(q.Segment)} · {LabelOffer(line.OfferType)} · {(string.IsNullOrWhiteSpace(line.Recurrence) ? "Unica" : line.Recurrence)}";

                                t.Cell().Element(CellBody).AlignCenter().Text(index.ToString());
                                t.Cell().Element(CellBody).Text(desc);
                                t.Cell().Element(CellBody).AlignCenter().Text(qty.ToString("0.##"));
                                t.Cell().Element(CellBody).AlignRight().Text(Money(unitNoVat));
                                t.Cell().Element(CellBody).AlignRight().Text(Money(subtotalNoVat));
                            }
                        });
                    });

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(r =>
                    {
                        r.RelativeItem().Column(x =>
                        {
                            x.Item().Text("Observaciones").SemiBold();
                            x.Item().Text(string.IsNullOrWhiteSpace(q.Notes)
                                ? "Esperamos seguir haciendo negocios con usted."
                                : q.Notes);

                            if (q.ContractTermMonths.HasValue)
                                x.Item().PaddingTop(4).Text($"Periodo de contrato: {q.ContractTermMonths} meses").SemiBold();

                            if (!string.IsNullOrWhiteSpace(q.GeneralTerms))
                            {
                                x.Item().PaddingTop(6).Text("Condiciones generales").SemiBold();
                                x.Item().Text(q.GeneralTerms);
                            }
                        });

                        r.ConstantItem(210).Column(x =>
                        {
                            x.Item().AlignRight().Text($"Subtotal sin IVA: {Money(q.SubtotalBeforeVat)}");
                            x.Item().AlignRight().Text($"IVA 16%: {Money(q.VatAmount)}");
                            x.Item().AlignRight().Text($"Total: {Money(q.EstimatedTotal)}").SemiBold().FontSize(12);
                        });
                    });
                });

                p.Footer().AlignCenter().Text($"{companyLegal} · Generado: {DateTime.Now:yyyy-MM-dd HH:mm}")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();

        return await MergeCatalogWithQuoteAsync(quotePdf);
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

    private static string LabelOffer(QuoteOfferType x) => x switch
    {
        QuoteOfferType.Sale => "Venta",
        QuoteOfferType.MonthlyRent => "Renta",
        QuoteOfferType.Lease => "Arrendamiento",
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

    private async Task<byte[]> MergeCatalogWithQuoteAsync(byte[] quotePdf)
    {
        var configured = _cfg["Quotes:CatalogPdfPath"];
        var relative = string.IsNullOrWhiteSpace(configured) ? "assets/catalog/Catalogo_2026.pdf" : configured.Trim();
        var catalogPath = Path.IsPathRooted(relative)
            ? relative
            : Path.Combine(_env.ContentRootPath, relative.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(catalogPath))
            return quotePdf;

        try
        {
            var catalogBytes = await File.ReadAllBytesAsync(catalogPath);
            using var output = new PdfDocument();

            using (var catalogDoc = PdfReader.Open(new MemoryStream(catalogBytes), PdfDocumentOpenMode.Import))
            {
                for (var i = 0; i < catalogDoc.PageCount; i++)
                    output.AddPage(catalogDoc.Pages[i]);
            }

            using (var quoteDoc = PdfReader.Open(new MemoryStream(quotePdf), PdfDocumentOpenMode.Import))
            {
                for (var i = 0; i < quoteDoc.PageCount; i++)
                    output.AddPage(quoteDoc.Pages[i]);
            }

            using var ms = new MemoryStream();
            output.Save(ms, false);
            return ms.ToArray();
        }
        catch
        {
            return quotePdf;
        }
    }
}
