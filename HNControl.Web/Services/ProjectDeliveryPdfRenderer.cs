using System.Security.Cryptography;
using System.Text;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HNControl.Web.Services;

public class ProjectDeliveryPdfRenderer : IProjectDeliveryPdfRenderer
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IConfiguration _cfg;

    public ProjectDeliveryPdfRenderer(ApplicationDbContext db, IFileStorage storage, IConfiguration cfg)
    {
        _db = db;
        _storage = storage;
        _cfg = cfg;
    }

    public async Task<byte[]> RenderAsync(ProjectDeliveryFormat delivery)
    {
        var d = await _db.ProjectDeliveryFormats
            .Include(x => x.Client)
            .Include(x => x.Project)
            .FirstAsync(x => x.Id == delivery.Id);
        var sys = await _db.SystemConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync();

        var logoBytes = await TryReadStorageBytesAsync(sys?.CompanyLogoStoragePath);
        var payload = ProjectDeliveryPayload.Parse(d.ServiceSummary);
        var servicesText = d.ServiceSummary?.StartsWith(ProjectDeliveryPayload.Prefix, StringComparison.Ordinal) == true
            ? ProjectDeliveryPayload.ServicesDisplay(payload)
            : string.IsNullOrWhiteSpace(d.ServiceSummary) ? "-" : d.ServiceSummary;
        var equipmentText = d.ServiceSummary?.StartsWith(ProjectDeliveryPayload.Prefix, StringComparison.Ordinal) == true
            ? ProjectDeliveryPayload.EquipmentDisplay(payload)
            : string.IsNullOrWhiteSpace(d.EquipmentSummary) ? "-" : d.EquipmentSummary;

        byte[]? sigBytes = null;
        if (!string.IsNullOrWhiteSpace(d.SignatureStoragePath))
        {
            try
            {
                var (stream, _, _) = await _storage.OpenAsync(d.SignatureStoragePath!, "signature.png");
                await using (stream)
                await using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms);
                    sigBytes = ms.ToArray();
                }
            }
            catch
            {
                sigBytes = null;
            }
        }

        var company = (sys?.CompanyName ?? _cfg["Branding:CompanyName"] ?? "HN Solutions").Trim();
        var basePdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(22);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(h =>
                {
                    h.RelativeItem().Column(col =>
                    {
                        col.Item().Text(company).FontSize(16).SemiBold();
                        col.Item().Text("Acta de entrega de servicios / material").FontSize(12).FontColor(Colors.Grey.Darken2);
                        col.Item().Text(d.Title).SemiBold();
                    });
                    h.ConstantItem(120).AlignMiddle().AlignRight().Element(el =>
                    {
                        if (logoBytes != null && logoBytes.Length > 0)
                            el.Height(52).Width(120).Image(logoBytes).FitArea();
                    });
                });

                page.Content().PaddingTop(10).Column(c =>
                {
                    c.Spacing(10);
                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(x =>
                    {
                        x.Item().Text("Datos generales").SemiBold();
                        x.Item().Text($"Cliente: {d.Client?.Name ?? "-"}");
                        x.Item().Text($"Proyecto: {d.Project?.Title ?? "-"}");
                        x.Item().Text($"Fecha de entrega: {d.DeliveryDate:yyyy-MM-dd}");
                        x.Item().Text($"Ubicacion: {d.DeliveryLocation}");
                        x.Item().Text($"Recibe: {d.ReceiverName} ({d.ReceiverEmail})");
                        x.Item().Text($"Segmento LAN: {ProjectDeliveryPayload.Safe(payload.SEGMENTOLAN)}");
                        x.Item().Text($"IP publica: {ProjectDeliveryPayload.Safe(payload.IPPUBLICA)}");
                    });

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(x =>
                    {
                        x.Item().Text("Servicios entregados").SemiBold();
                        x.Item().PaddingTop(5).Text(servicesText);
                    });

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(x =>
                    {
                        x.Item().Text("Equipamiento entregado").SemiBold();
                        x.Item().PaddingTop(5).Text(equipmentText);
                    });

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(x =>
                    {
                        x.Item().Text("Firma cliente").SemiBold();
                        x.Item().Text($"Firmado por: {d.SignedByName ?? "-"}");
                        x.Item().Text($"Correo: {d.SignedByEmail ?? "-"}");
                        x.Item().Text($"Fecha: {(d.SignedAt.HasValue ? d.SignedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "-")}");
                        x.Item().PaddingTop(6).Height(90).Border(1).BorderColor(Colors.Grey.Lighten2)
                            .AlignCenter().AlignMiddle()
                            .Element(el =>
                            {
                                if (sigBytes != null && sigBytes.Length > 0)
                                    el.Image(sigBytes).FitArea();
                                else
                                    el.Text("Pendiente de firma").FontColor(Colors.Grey.Darken2);
                            });
                    });
                });

                page.Footer().Column(f =>
                {
                    f.Item().AlignCenter().Text($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    f.Item().AlignCenter().Text($"Firma digital: {BuildHash(d)}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();

        return await AppendEvidencePagesAsync(d, basePdf);
    }

    public async Task<byte[]> AppendEvidencePagesAsync(ProjectDeliveryFormat delivery, byte[] basePdf)
    {
        var payload = ProjectDeliveryPayload.Parse(delivery.ServiceSummary);
        var images = new List<(byte[] Bytes, string Name)>();

        foreach (var evidence in payload.Evidences.Where(x => !string.IsNullOrWhiteSpace(x.StoragePath)).Take(ProjectDeliveryPayload.MaxEvidenceFiles))
        {
            try
            {
                var (stream, _, _) = await _storage.OpenAsync(evidence.StoragePath!, evidence.OriginalFileName ?? "evidencia");
                await using (stream)
                await using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms);
                    if (ms.Length > 0)
                        images.Add((ms.ToArray(), evidence.OriginalFileName ?? "Evidencia"));
                }
            }
            catch
            {
                // Una evidencia faltante no debe bloquear la generación del acta.
            }
        }

        if (images.Count == 0)
            return basePdf;

        byte[] evidencePdf;
        try
        {
            evidencePdf = Document.Create(container =>
            {
                for (var i = 0; i < images.Count; i += 2)
                {
                    var pageImages = images.Skip(i).Take(2).ToList();
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(28);
                        page.DefaultTextStyle(x => x.FontSize(10));
                        page.Header().Column(h =>
                        {
                            h.Item().Text("Evidencia fotografica").FontSize(18).SemiBold();
                            h.Item().Text(delivery.Title).FontColor(Colors.Grey.Darken2);
                        });
                        page.Content().PaddingTop(16).Column(c =>
                        {
                            c.Spacing(14);
                            foreach (var image in pageImages)
                            {
                                c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(card =>
                                {
                                    card.Item().Height(300).AlignCenter().AlignMiddle().Image(image.Bytes).FitArea();
                                    card.Item().PaddingTop(6).Text(image.Name).FontSize(9).FontColor(Colors.Grey.Darken1);
                                });
                            }
                        });
                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.Span("Anexo de evidencia - ");
                            x.CurrentPageNumber();
                        });
                    });
                }
            }).GeneratePdf();
        }
        catch
        {
            return basePdf;
        }

        return MergePdfs(basePdf, evidencePdf);
    }

    private static byte[] MergePdfs(params byte[][] pdfs)
    {
        using var output = new PdfDocument();
        foreach (var pdf in pdfs.Where(x => x.Length > 0))
        {
            using var ms = new MemoryStream(pdf);
            using var input = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
            for (var i = 0; i < input.PageCount; i++)
                output.AddPage(input.Pages[i]);
        }

        using var outMs = new MemoryStream();
        output.Save(outMs, false);
        return outMs.ToArray();
    }

    private static string BuildHash(ProjectDeliveryFormat d)
    {
        var payload = $"{d.Id}|{d.ClientId}|{d.ProjectId}|{d.Title}|{d.SignedByName}|{d.SignedByEmail}|{d.SignedAt:O}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes)[..18];
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
