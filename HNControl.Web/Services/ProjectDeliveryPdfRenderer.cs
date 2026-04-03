using System.Security.Cryptography;
using System.Text;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;
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

        var company = (_cfg["Branding:CompanyName"] ?? "HN Solutions").Trim();

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
                    h.Item().Text("Acta de entrega de servicios / material").FontSize(12).FontColor(Colors.Grey.Darken2);
                    h.Item().Text(d.Title).SemiBold();
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
                    });

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(x =>
                    {
                        x.Item().Text("Servicios entregados").SemiBold();
                        x.Item().PaddingTop(5).Text(string.IsNullOrWhiteSpace(d.ServiceSummary) ? "-" : d.ServiceSummary);
                    });

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(x =>
                    {
                        x.Item().Text("Equipamiento entregado").SemiBold();
                        x.Item().PaddingTop(5).Text(string.IsNullOrWhiteSpace(d.EquipmentSummary) ? "-" : d.EquipmentSummary);
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
    }

    private static string BuildHash(ProjectDeliveryFormat d)
    {
        var payload = $"{d.Id}|{d.ClientId}|{d.ProjectId}|{d.Title}|{d.SignedByName}|{d.SignedByEmail}|{d.SignedAt:O}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes)[..18];
    }
}
