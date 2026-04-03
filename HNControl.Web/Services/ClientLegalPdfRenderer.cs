using System.Security.Cryptography;
using System.Text;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HNControl.Web.Services;

public class ClientLegalPdfRenderer : IClientLegalPdfRenderer
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IConfiguration _cfg;

    public ClientLegalPdfRenderer(ApplicationDbContext db, IFileStorage storage, IConfiguration cfg)
    {
        _db = db;
        _storage = storage;
        _cfg = cfg;
    }

    public async Task<byte[]> RenderAsync(ClientLegalDocument document)
    {
        var doc = await _db.ClientLegalDocuments
            .Include(x => x.Client)
            .Include(x => x.ClientServiceContract)
            .FirstAsync(x => x.Id == document.Id);

        byte[]? sigBytes = null;
        if (!string.IsNullOrWhiteSpace(doc.SignatureStoragePath))
        {
            try
            {
                var (stream, _, _) = await _storage.OpenAsync(doc.SignatureStoragePath!, "signature.png");
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
        var title = doc.DocumentType == ClientLegalDocumentType.NDA ? "NDA (Confidencialidad)" : "Contrato de servicios";

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
                    h.Item().Text(title).FontSize(12).FontColor(Colors.Grey.Darken2);
                    h.Item().Text(doc.Title).SemiBold();
                });

                page.Content().PaddingTop(10).Column(c =>
                {
                    c.Spacing(10);
                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(x =>
                    {
                        x.Item().Text("Cliente").SemiBold();
                        x.Item().Text(doc.Client?.Name ?? "-");
                        x.Item().Text($"RFC: {doc.Client?.Rfc ?? "-"}");
                        x.Item().Text($"Representante legal: {doc.Client?.LegalRepresentative ?? doc.Client?.ContactName ?? "-"}");
                        x.Item().Text($"Correo legal: {doc.Client?.LegalEmail ?? doc.Client?.Email ?? "-"}");
                        x.Item().Text($"Domicilio fiscal: {doc.Client?.FiscalAddress ?? doc.Client?.Address ?? "-"}");
                    });

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(x =>
                    {
                        x.Item().Text("Datos comerciales").SemiBold();
                        if (doc.ClientServiceContract != null)
                        {
                            x.Item().Text($"Servicio: {doc.ClientServiceContract.Label}");
                            x.Item().Text($"Proveedor: {doc.ClientServiceContract.Provider}");
                            x.Item().Text($"Cuenta: {doc.ClientServiceContract.AccountNumber}");
                            x.Item().Text($"Contrato: {doc.ClientServiceContract.ContractNumber}");
                        }
                        x.Item().Text($"Costo mensual: {(doc.MonthlyAmount ?? 0m):C}");
                        x.Item().Text($"Periodo: {(doc.ContractStartDate?.ToString("yyyy-MM-dd") ?? "-")} a {(doc.ContractEndDate?.ToString("yyyy-MM-dd") ?? "-")}");
                    });

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(x =>
                    {
                        x.Item().Text("Clausulas").SemiBold();
                        x.Item().PaddingTop(5).Text(string.IsNullOrWhiteSpace(doc.TermsBody)
                            ? "Documento generado por HN Control."
                            : doc.TermsBody);
                    });

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(x =>
                    {
                        x.Item().Text("Firma digital cliente").SemiBold();
                        x.Item().Text($"Firmado por: {doc.SignedByName ?? "-"}");
                        x.Item().Text($"Correo: {doc.SignedByEmail ?? "-"}");
                        x.Item().Text($"Fecha: {(doc.SignedAt.HasValue ? doc.SignedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "-")}");
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
                    f.Item().AlignCenter().Text($"Firma digital: {BuildHash(doc)}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();
    }

    private static string BuildHash(ClientLegalDocument doc)
    {
        var payload = $"{doc.Id}|{doc.ClientId}|{doc.DocumentType}|{doc.Title}|{doc.SignedByName}|{doc.SignedByEmail}|{doc.SignedAt:O}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes)[..18];
    }
}
