using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

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
        var o = await _db.ServiceOrders.AsNoTracking().AsSplitQuery()
            .Include(x => x.Client)
            .Include(x => x.ClientServiceContract)
            .Include(x => x.Checklist)
            .Include(x => x.WorkItems)
            .Include(x => x.Evidences)
            .Include(x => x.Signatures)
            .Include(x => x.AssignedEmployee)
            .Include(x => x.ClaimedByEmployee)
            .FirstAsync(x => x.Id == order.Id);

        var branding = await _db.SystemConfigurations.AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new { x.CompanyName, x.CompanyLogoStoragePath })
            .FirstOrDefaultAsync();
        var logo = await TryReadStorageBytesAsync(branding?.CompanyLogoStoragePath) ?? LoadLogoBytes();
        var company = branding?.CompanyName ?? _cfg["Branding:CompanyName"] ?? "HN Solutions";
        var footer = _cfg["Branding:ReportFooter"] ?? "HN Control";

        async Task<ServiceOrderReport.Signature> ReadSignature(SignatureRole role)
        {
            var signature = o.Signatures.Where(s => s.Role == role)
                .OrderByDescending(s => s.SignedAt).ThenBy(s => s.Id).FirstOrDefault();
            var bytes = await TryReadStorageBytesAsync(signature?.StoragePath);
            return ServiceOrderReport.Signature.Capture(o.Id, role, signature, bytes);
        }

        var attachments = new List<ServiceOrderReport.Attachment>();
        foreach (var ev in o.Evidences.OrderByDescending(x => x.UploadedAt).ThenBy(x => x.Id))
        {
            var image = IsSupportedImage(ev) ? await TryReadStorageBytesAsync(ev.StoragePath) : null;
            attachments.Add(new(ev.Id, ev.OriginalFileName, ev.UploadedAt, image));
        }

        var report = new ServiceOrderReport(o, company, footer, logo, attachments,
            await ReadSignature(SignatureRole.Technician), await ReadSignature(SignatureRole.Client));
        try
        {
            return report.Render(withImages: true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A damaged stored image must not hide the checklist, contract or signature records.
            return report.Render(withImages: false);
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

    private async Task<byte[]?> TryReadStorageBytesAsync(string? storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return null;

        try
        {
            var (stream, _, _) = await _storage.OpenAsync(storagePath, "file.bin");
            using var source = stream;
            using var ms = new MemoryStream();
            await source.CopyToAsync(ms);
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

}
