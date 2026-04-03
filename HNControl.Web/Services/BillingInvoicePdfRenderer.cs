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
    private readonly IFileStorage _storage;

    public BillingInvoicePdfRenderer(ApplicationDbContext db, IConfiguration cfg, IFileStorage storage)
    {
        _db = db;
        _cfg = cfg;
        _storage = storage;
    }

    public async Task<byte[]> RenderAsync(BillingInvoicePlan plan, BillingInvoiceRun run)
    {
        var item = await _db.BillingInvoicePlans
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.QuoteRequest)
            .FirstAsync(x => x.Id == plan.Id);

        var sys = await _db.SystemConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync();

        var company = (sys?.CompanyName ?? _cfg["Branding:CompanyName"] ?? "HN Solutions").Trim();
        var companyLegal = string.IsNullOrWhiteSpace(sys?.CompanyLegalName) ? company : sys!.CompanyLegalName;
        var taxId = (sys?.CompanyRfc ?? _cfg["Branding:TaxId"] ?? "XAXX010101000").Trim();
        var regime = string.IsNullOrWhiteSpace(sys?.CompanyFiscalRegimeCode) ? item.FiscalRegimeCode : sys!.CompanyFiscalRegimeCode;
        var zip = string.IsNullOrWhiteSpace(sys?.CompanyFiscalZipCode) ? (item.Client?.FiscalZipCode ?? "-") : sys!.CompanyFiscalZipCode;
        var fiscalAddress = string.IsNullOrWhiteSpace(sys?.CompanyFiscalAddress) ? "-" : sys!.CompanyFiscalAddress;
        var cfdiVersion = string.IsNullOrWhiteSpace(sys?.CfdiVersion) ? "4.0" : sys!.CfdiVersion;
        var serie = string.IsNullOrWhiteSpace(sys?.CfdiSerieDefault) ? "A" : sys!.CfdiSerieDefault;

        var logoBytes = await TryReadStorageBytesAsync(sys?.CompanyLogoStoragePath);
        var digitalSeal = BuildHash(item, run);
        var qrPayload = BuildQrPayload(item, run, taxId, digitalSeal);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(h =>
                {
                    h.RelativeItem().Column(col =>
                    {
                        col.Item().Text(company).FontSize(16).SemiBold();
                        col.Item().Text(companyLegal).FontColor(Colors.Grey.Darken2);
                        col.Item().Text("Factura (simulación / pre-timbrado)").FontColor(Colors.Blue.Darken2).SemiBold();
                        col.Item().Text($"CFDI {cfdiVersion} · Serie {serie} · Folio FAC-{run.ScheduledFor:yyyyMMdd}-{run.Id.ToString("N")[..6]}");
                    });
                    h.ConstantItem(140).AlignMiddle().AlignRight().Element(el =>
                    {
                        if (logoBytes != null && logoBytes.Length > 0)
                            el.Height(54).Width(140).Image(logoBytes).FitArea();
                        else
                            el.Text("LOGO").SemiBold().FontColor(Colors.Grey.Darken1);
                    });
                });

                page.Content().PaddingTop(8).Column(c =>
                {
                    c.Spacing(10);

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Row(r =>
                    {
                        r.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Emisor").SemiBold().FontColor(Colors.Blue.Darken2);
                            col.Item().Text(companyLegal);
                            col.Item().Text($"RFC: {taxId}");
                            col.Item().Text($"Régimen: {regime}");
                            col.Item().Text($"CP fiscal: {zip}");
                            col.Item().Text($"Domicilio: {fiscalAddress}");
                        });
                        r.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Receptor").SemiBold().FontColor(Colors.Blue.Darken2);
                            col.Item().Text(item.Client?.Name ?? "-");
                            col.Item().Text($"RFC: {item.Client?.Rfc ?? "-"}");
                            col.Item().Text($"CP: {item.Client?.FiscalZipCode ?? "-"}");
                            col.Item().Text($"Uso CFDI: {item.CfdiUseCode}");
                        });
                    });

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(col =>
                    {
                        col.Item().Text("Datos SAT / Timbrado").SemiBold().FontColor(Colors.Blue.Darken2);
                        col.Item().Text($"Tipo comprobante: {MapInvoiceType(item.InvoiceType)}");
                        col.Item().Text($"Versión CFDI: {cfdiVersion}");
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
                            def.RelativeColumn(3);
                            def.RelativeColumn(1);
                            def.RelativeColumn(1);
                            def.RelativeColumn(1);
                            def.RelativeColumn(1);
                        });

                        t.Header(h =>
                        {
                            h.Cell().Element(CellHeader).Text("Descripcion");
                            h.Cell().Element(CellHeader).AlignCenter().Text("Cant.");
                            h.Cell().Element(CellHeader).AlignRight().Text("Subtotal");
                            h.Cell().Element(CellHeader).AlignRight().Text("IVA");
                            h.Cell().Element(CellHeader).AlignRight().Text("Total");
                        });

                        t.Cell().Element(CellBody).Text(item.Concept);
                        t.Cell().Element(CellBody).AlignCenter().Text("1");
                        t.Cell().Element(CellBody).AlignRight().Text(Money(item.Subtotal));
                        t.Cell().Element(CellBody).AlignRight().Text(Money(item.VatAmount));
                        t.Cell().Element(CellBody).AlignRight().Text(Money(item.Total)).SemiBold();
                    });

                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(col =>
                        {
                            col.Item().Text("Cadena y sello digital").SemiBold().FontColor(Colors.Blue.Darken2);
                            col.Item().Text($"Sello: {digitalSeal}").FontSize(8);
                            col.Item().Text($"UUID interno: {run.Id}").FontSize(8);
                            col.Item().Text($"Cadena original: ||{cfdiVersion}|{serie}|{run.Id.ToString("N")[..10]}|{item.Total:0.00}|{run.ScheduledFor:yyyy-MM-dd}||").FontSize(8);
                        });
                        r.ConstantItem(145).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(col =>
                        {
                            col.Item().Text("QR (previo SAT)").SemiBold().FontColor(Colors.Blue.Darken2);
                            col.Item().Background(Colors.Grey.Lighten3).Height(90).AlignCenter().AlignMiddle()
                                .Text("QR").SemiBold().FontColor(Colors.Grey.Darken2);
                            col.Item().Text(qrPayload).FontSize(6).FontColor(Colors.Grey.Darken2);
                        });
                    });
                });

                page.Footer().Column(f =>
                {
                    f.Item().AlignCenter().Text($"Programada para envío: {run.ScheduledFor:yyyy-MM-dd} · Documento no timbrado").FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    f.Item().AlignCenter().Text("Representación impresa de un CFDI de prueba para control interno.").FontSize(8).FontColor(Colors.Grey.Darken1);
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

    private static string BuildQrPayload(BillingInvoicePlan plan, BillingInvoiceRun run, string rfcEmisor, string seal)
    {
        var tt = plan.Total.ToString("0.000000");
        var fe = seal.Length >= 8 ? seal[^8..] : seal;
        return $"?re={rfcEmisor}&rr={plan.Client?.Rfc ?? "XAXX010101000"}&tt={tt}&id={run.Id}&fe={fe}";
    }

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
