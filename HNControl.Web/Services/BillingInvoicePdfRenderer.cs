using System.Security.Cryptography;
using System.Text;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HNControl.Web.Services;

public class BillingInvoicePdfRenderer : IBillingInvoicePdfRenderer
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _cfg;

    public BillingInvoicePdfRenderer(ApplicationDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    public async Task<byte[]> RenderAsync(BillingInvoicePlan plan, BillingInvoiceRun run)
    {
        var item = await _db.BillingInvoicePlans
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.QuoteRequest)
            .FirstAsync(x => x.Id == plan.Id);

        var company = (_cfg["Branding:CompanyName"] ?? "HN Solutions").Trim();
        var taxId = (_cfg["Branding:TaxId"] ?? "XAXX010101000").Trim();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(22);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(h =>
                {
                    h.Item().Text(company).FontSize(16).SemiBold();
                    h.Item().Text("Factura programada (simulacion sin timbrado)").FontSize(11).FontColor(Colors.Grey.Darken2);
                    h.Item().Text($"Folio interno: FAC-{run.ScheduledFor:yyyyMMdd}-{run.Id.ToString("N")[..6]}").SemiBold();
                });

                page.Content().PaddingTop(8).Column(c =>
                {
                    c.Spacing(10);

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Row(r =>
                    {
                        r.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Emisor").SemiBold();
                            col.Item().Text(company);
                            col.Item().Text($"RFC: {taxId}");
                        });
                        r.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Cliente").SemiBold();
                            col.Item().Text(item.Client?.Name ?? "-");
                            col.Item().Text($"RFC: {item.Client?.Rfc ?? "-"}");
                            col.Item().Text($"CP: {item.Client?.FiscalZipCode ?? "-"}");
                        });
                    });

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(col =>
                    {
                        col.Item().Text("Datos SAT (catalogos)").SemiBold();
                        col.Item().Text($"Tipo comprobante: {MapInvoiceType(item.InvoiceType)}");
                        col.Item().Text($"Uso CFDI: {item.CfdiUseCode}");
                        col.Item().Text($"Regimen fiscal: {item.FiscalRegimeCode}");
                        col.Item().Text($"Metodo de pago: {item.PaymentMethodCode}");
                        col.Item().Text($"Forma de pago: {item.PaymentFormCode}");
                    });

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(col =>
                    {
                        col.Item().Text("Concepto").SemiBold();
                        col.Item().Text(item.Concept);
                        col.Item().Text($"Periodo: {run.PeriodLabel}");
                        col.Item().Text($"Cliente: {item.Client?.Name ?? "-"}");
                    });

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Table(t =>
                    {
                        t.ColumnsDefinition(def =>
                        {
                            def.RelativeColumn(4);
                            def.RelativeColumn(1);
                            def.RelativeColumn(1);
                            def.RelativeColumn(1);
                        });

                        t.Header(h =>
                        {
                            h.Cell().Element(CellHeader).Text("Descripcion");
                            h.Cell().Element(CellHeader).AlignRight().Text("Subtotal");
                            h.Cell().Element(CellHeader).AlignRight().Text("IVA");
                            h.Cell().Element(CellHeader).AlignRight().Text("Total");
                        });

                        t.Cell().Element(CellBody).Text(item.Concept);
                        t.Cell().Element(CellBody).AlignRight().Text(Money(item.Subtotal));
                        t.Cell().Element(CellBody).AlignRight().Text(Money(item.VatAmount));
                        t.Cell().Element(CellBody).AlignRight().Text(Money(item.Total)).SemiBold();
                    });
                });

                page.Footer().Column(f =>
                {
                    f.Item().AlignCenter().Text($"Programada para envio: {run.ScheduledFor:yyyy-MM-dd}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    f.Item().AlignCenter().Text($"Firma digital: {BuildHash(item, run)}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();
    }

    private static IContainer CellHeader(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).PaddingVertical(5).PaddingHorizontal(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);

    private static IContainer CellBody(IContainer c) =>
        c.PaddingVertical(6).PaddingHorizontal(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);

    private static string Money(decimal value) => value.ToString("C2");

    private static string MapInvoiceType(BillingInvoiceType type) => type switch
    {
        BillingInvoiceType.Ingreso => "I",
        BillingInvoiceType.Egreso => "E",
        BillingInvoiceType.Traslado => "T",
        BillingInvoiceType.Nomina => "N",
        BillingInvoiceType.Pago => "P",
        _ => "I"
    };

    private static string BuildHash(BillingInvoicePlan plan, BillingInvoiceRun run)
    {
        var payload = $"{plan.Id}|{run.Id}|{plan.ClientId}|{plan.Total:0.00}|{run.ScheduledFor:yyyyMMdd}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes)[..20];
    }
}
