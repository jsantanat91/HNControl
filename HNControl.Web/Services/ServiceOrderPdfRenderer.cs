using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Hosting;
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
    private readonly IWebHostEnvironment _env;

    public ServiceOrderPdfRenderer(
        IConfiguration cfg,
        ApplicationDbContext db,
        IFileStorage storage,
        IWebHostEnvironment env)
    {
        _cfg = cfg;
        _db = db;
        _storage = storage;
        _env = env;
    }

    public async Task<byte[]> RenderAsync(ServiceOrder order)
    {
        var o = await _db.ServiceOrders
            .Include(x => x.Client)
            .Include(x => x.Checklist)
            .Include(x => x.WorkItems)
            .Include(x => x.Evidences)
            .Include(x => x.Signatures)
            .Include(x => x.AssignedEmployee)
            .Include(x => x.ClaimedByEmployee)
            .FirstAsync(x => x.Id == order.Id);
        var sys = await _db.SystemConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync();

        // --------------------
        // Branding / logo
        // --------------------
        byte[]? logoBytes = await TryReadStorageBytesAsync(sys?.CompanyLogoStoragePath) ?? LoadLogoBytes();

        // --------------------
        // Firmas (leer desde storage)
        // --------------------
        var techSig = o.Signatures.FirstOrDefault(s => s.Role == SignatureRole.Technician);
        var cliSig = o.Signatures.FirstOrDefault(s => s.Role == SignatureRole.Client);

        byte[]? techBytes = await TryReadStorageBytesAsync(techSig?.StoragePath);
        byte[]? cliBytes = await TryReadStorageBytesAsync(cliSig?.StoragePath);

        // --------------------
        // Evidencias (precargar imagenes)
        // --------------------
        var evidenceImages = new List<(string Name, DateTime UploadedAt, byte[] Bytes)>();
        var evidenceFiles = new List<(string Name, DateTime UploadedAt)>();

        foreach (var ev in o.Evidences.OrderByDescending(x => x.UploadedAt))
        {
            if (IsSupportedImage(ev))
            {
                var bytes = await TryReadStorageBytesAsync(ev.StoragePath);
                if (bytes != null && bytes.Length > 0)
                    evidenceImages.Add((ev.OriginalFileName, ev.UploadedAt, bytes));
                else
                    evidenceFiles.Add((ev.OriginalFileName, ev.UploadedAt));
            }
            else
            {
                evidenceFiles.Add((ev.OriginalFileName, ev.UploadedAt));
            }
        }

        var company = (sys?.CompanyName ?? _cfg["Branding:CompanyName"] ?? "HN Solutions").Trim();
        var footer = (_cfg["Branding:ReportFooter"] ?? "HN Control").Trim();
        var digitalHash = BuildOrderHash(o);

        // Labels en espanol
        var typeLabel = GetDisplayName(o.Type);
        var status = o.Status == ServiceOrderStatus.Completed ? ServiceOrderStatus.Finalized : o.Status;
        var statusLabel = GetDisplayName(status);

        try
        {
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

                    r.ConstantItem(160).AlignRight().AlignMiddle().Element(el =>
                    {
                        if (logoBytes != null && logoBytes.Length > 0)
                            el.Height(56).Width(160).Image(logoBytes).FitArea();
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
                            cc.Item().Text(o.Client?.Name ?? "-");
                            if (!string.IsNullOrWhiteSpace(o.Client?.Email))
                                cc.Item().Text(o.Client!.Email).FontColor(Colors.Grey.Darken2);
                            if (!string.IsNullOrWhiteSpace(o.Client?.Phone))
                                cc.Item().Text(o.Client!.Phone).FontColor(Colors.Grey.Darken2);
                        });

                        r.ConstantItem(270).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                        {
                            cc.Item().Text("Datos de la orden").SemiBold();
                            cc.Item().Text($"Tipo: {typeLabel}");
                            cc.Item().Text($"Estatus: {statusLabel}");
                            var techName = o.ClaimedByEmployee?.FullName ?? o.AssignedEmployee?.FullName;
                            if (!string.IsNullOrWhiteSpace(techName))
                                cc.Item().Text($"Tecnico: {techName}");
                            cc.Item().Text($"Creada: {o.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
                            if (o.EstimatedEndDate != null)
                                cc.Item().Text($"SLA: {o.EstimatedEndDate.Value:yyyy-MM-dd}");
                        });
                    });

                    // Descripcion
                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Descripcion").SemiBold();
                        cc.Item().PaddingTop(6).Text(string.IsNullOrWhiteSpace(o.Description) ? "-" : o.Description);
                    });

                    if (!string.IsNullOrWhiteSpace(o.LevantamientoNotes) || !string.IsNullOrWhiteSpace(o.MaterialesNotes))
                    {
                        c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                        {
                            cc.Item().Text("Observaciones por etapa").SemiBold();
                            if (!string.IsNullOrWhiteSpace(o.LevantamientoNotes))
                            {
                                cc.Item().PaddingTop(6).Text("Levantamiento").SemiBold();
                                cc.Item().Text(o.LevantamientoNotes).FontColor(Colors.Grey.Darken2);
                            }
                            if (!string.IsNullOrWhiteSpace(o.MaterialesNotes))
                            {
                                cc.Item().PaddingTop(6).Text("Materiales").SemiBold();
                                cc.Item().Text(o.MaterialesNotes).FontColor(Colors.Grey.Darken2);
                            }
                        });
                    }

                    // Checklist
                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Checklist").SemiBold();
                        cc.Item().PaddingTop(6);

                        void RenderChecklistTable(List<ServiceOrderChecklistItem> list)
                        {
                            if (list.Count == 0)
                            {
                                cc.Item().Text("-").FontColor(Colors.Grey.Darken2);
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
                                    t.Cell().Element(cel => CellBody(cel, zebra)).AlignCenter().Text(it.IsDone ? "OK" : "");
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

                                cc.Item().PaddingTop(8).Text($"{w.SortOrder + 1}. {w.Title} - {w.Type}").SemiBold();

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
                            return;
                        }

                        if (evidenceImages.Count > 0)
                        {
                            cc.Item().Text("Imagenes").SemiBold().FontColor(Colors.Grey.Darken2);
                            cc.Item().PaddingTop(6).Table(t =>
                            {
                                t.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn();
                                    cols.RelativeColumn();
                                });

                                foreach (var img in evidenceImages)
                                {
                                    t.Cell().Padding(4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(ic =>
                                    {
                                        ic.Item()
                                            .Height(220)
                                            .Border(1).BorderColor(Colors.Grey.Lighten2)
                                            .Background(Colors.Grey.Lighten5)
                                            .AlignCenter().AlignMiddle()
                                            .Image(img.Bytes).FitArea();

                                        ic.Item().PaddingTop(4).Text(img.Name).FontSize(9).FontColor(Colors.Grey.Darken2);
                                        ic.Item().Text(img.UploadedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")).FontSize(8).FontColor(Colors.Grey.Darken2);
                                    });
                                }
                            });
                        }

                        if (evidenceFiles.Count > 0)
                        {
                            cc.Item().PaddingTop(10).Text("Archivos").SemiBold().FontColor(Colors.Grey.Darken2);
                            foreach (var f in evidenceFiles)
                                cc.Item().Text($"- {f.Name} - {f.UploadedAt.ToLocalTime():yyyy-MM-dd HH:mm}").FontColor(Colors.Grey.Darken2);
                        }
                    });

                    // Firmas
                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("Firmas").SemiBold();
                        cc.Item().PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(x =>
                            {
                                x.Item().Text("Tecnico").SemiBold();
                                x.Item().Text(string.IsNullOrWhiteSpace(techSig?.SignedByName) ? "-" : techSig!.SignedByName).FontColor(Colors.Grey.Darken2);
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
                                x.Item().Text(string.IsNullOrWhiteSpace(cliSig?.SignedByName) ? "-" : cliSig!.SignedByName).FontColor(Colors.Grey.Darken2);
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
                            cc.Item().Text("Notas de revision").SemiBold();
                            cc.Item().PaddingTop(6).Text(o.AdminReviewNotes!).FontColor(Colors.Grey.Darken2);
                        });
                    }
                });

                // FOOTER
                page.Footer().Column(f =>
                {
                    f.Item().AlignCenter().Text($"{footer} - {DateTime.Now:yyyy-MM-dd HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                    f.Item().AlignCenter().Text($"Firma digital: {digitalHash}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
                });
            });

            return doc.GeneratePdf();
        }
        catch (Exception ex)
        {
            return BuildFallbackPdf(o, company, footer, digitalHash, ex.Message);
        }
    }

    // ===== LOGO loader (robusto para publish/docker) =====
    private byte[]? LoadLogoBytes()
    {
        var configured = (_cfg["Branding:LogoPath"] ?? "").Trim();

        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configured))
            candidates.Add(configured);

        // Defaults que tu estas usando
        candidates.Add("assets/logo.png");
        candidates.Add("wwwroot/assets/logo.png");

        // Absolutos tipicos en runtime
        if (!string.IsNullOrWhiteSpace(_env.ContentRootPath))
        {
            candidates.Add(Path.Combine(_env.ContentRootPath, "assets", "logo.png"));
            candidates.Add(Path.Combine(_env.ContentRootPath, "wwwroot", "assets", "logo.png"));
        }

        if (!string.IsNullOrWhiteSpace(_env.WebRootPath))
        {
            candidates.Add(Path.Combine(_env.WebRootPath, "assets", "logo.png"));
        }

        candidates.Add(Path.Combine(AppContext.BaseDirectory, "assets", "logo.png"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "wwwroot", "assets", "logo.png"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "assets", "logo.png"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "logo.png"));

        foreach (var raw in candidates.Where(x => !string.IsNullOrWhiteSpace(x))
                                      .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var p in ExpandToAbsolutePaths(raw))
            {
                try
                {
                    if (File.Exists(p))
                        return File.ReadAllBytes(p);
                }
                catch { /* silencio, el logo no debe tumbar el PDF */ }
            }
        }

        return null;
    }

    private IEnumerable<string> ExpandToAbsolutePaths(string path)
    {
        path = path.Replace("/", Path.DirectorySeparatorChar.ToString());

        if (Path.IsPathRooted(path))
            return new[] { path };

        var bases = new List<string>();

        if (!string.IsNullOrWhiteSpace(_env.ContentRootPath)) bases.Add(_env.ContentRootPath);
        bases.Add(AppContext.BaseDirectory);
        bases.Add(Directory.GetCurrentDirectory());

        return bases.Select(b => Path.GetFullPath(Path.Combine(b, path)));
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

    private static bool IsSupportedImage(ServiceOrderEvidence ev)
    {
        var ct = (ev.ContentType ?? "").Trim().ToLowerInvariant();
        if (ct.StartsWith("image/"))
        {
            if (ct.Contains("heic") || ct.Contains("heif")) return false;
            if (ct.Contains("webp")) return false;
            return true;
        }

        var name = (ev.OriginalFileName ?? "").ToLowerInvariant();
        return name.EndsWith(".png") || name.EndsWith(".jpg") || name.EndsWith(".jpeg");
    }

    private static string GetDisplayName(Enum value)
    {
        var type = value.GetType();
        var name = Enum.GetName(type, value) ?? value.ToString();
        var field = type.GetField(name);
        if (field == null) return name;

        var attr = field.GetCustomAttribute<DisplayAttribute>();
        return string.IsNullOrWhiteSpace(attr?.Name) ? name : attr!.Name!;
    }
    private static string BuildOrderHash(ServiceOrder o)
    {
        var payload = string.Join("|",
            o.Id,
            o.ClientId,
            o.Type,
            o.Status,
            o.CreatedAt.ToUniversalTime().ToString("yyyyMMddHHmmss"),
            (o.FinalizedAt ?? o.StartedAt ?? o.SubmittedForReviewAt ?? o.CreatedAt).ToUniversalTime().ToString("yyyyMMddHHmmss"),
            o.Title ?? "",
            o.Description ?? "",
            o.Checklist.Count,
            o.Evidences.Count,
            o.Signatures.Count);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes)[..16];
    }

    private static byte[] BuildFallbackPdf(ServiceOrder o, string company, string footer, string hash, string? error)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(c =>
                {
                    c.Item().Text(company).FontSize(16).SemiBold();
                    c.Item().Text("Orden de Servicio (respaldo)").FontSize(12).FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(10).Column(c =>
                {
                    c.Spacing(6);
                    c.Item().Text($"Folio interno: {o.Id}").SemiBold();
                    c.Item().Text($"Cliente: {o.Client?.Name ?? "-"}");
                    c.Item().Text($"Título: {o.Title}");
                    c.Item().Text($"Tipo: {o.Type}");
                    c.Item().Text($"Estatus: {o.Status}");
                    c.Item().Text($"Creada: {o.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
                    c.Item().Text($"Descripción: {(string.IsNullOrWhiteSpace(o.Description) ? "-" : o.Description)}");
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        c.Item().PaddingTop(8).Text("Nota técnica: se aplicó plantilla de respaldo para este PDF.")
                            .FontColor(Colors.Grey.Darken2);
                    }
                });

                page.Footer().Column(f =>
                {
                    f.Item().AlignCenter().Text($"{footer} - {DateTime.Now:yyyy-MM-dd HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                    f.Item().AlignCenter().Text($"Firma digital: {hash}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });
        return doc.GeneratePdf();
    }
}


