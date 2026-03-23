using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Knowledge;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string Q { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string Category { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string Type { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string Owner { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string Tag { get; set; } = "";
    [BindProperty(SupportsGet = true)] public bool PinnedOnly { get; set; } = false;
    [BindProperty(SupportsGet = true)] public bool DueOnly { get; set; } = false;

    public record KnowledgeRow(
        Guid Id,
        string Title,
        string Category,
        KnowledgeDocType DocType,
        KnowledgeStatus Status,
        string Description,
        string OwnerName,
        string ClientName,
        string ContractLabel,
        string Tags,
        DateTime UpdatedAt,
        DateTime? ReviewDueAt,
        bool IsPinned,
        int Version,
        int ViewCount,
        bool HasAttachment,
        bool HasUrl,
        bool HasAccessData);

    public sealed class StatBox
    {
        public int Total { get; set; }
        public int Publicados { get; set; }
        public int Borradores { get; set; }
        public int Vencidos { get; set; }
        public int ActualizadosSemana { get; set; }
    }

    public StatBox Stats { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<string> Owners { get; set; } = new();
    public List<KnowledgeRow> Rows { get; set; } = new();
    public IReadOnlyList<KnowledgeDocType> TypeOptions { get; } = new[]
    {
        KnowledgeDocType.AccesoPlataforma,
        KnowledgeDocType.ManualInterno
    };

    public async Task OnGetAsync()
    {
        var baseQuery = _db.KnowledgeLinks
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.ClientServiceContract);

        Stats = new StatBox
        {
            Total = await baseQuery.CountAsync(),
            Publicados = await baseQuery.CountAsync(x => x.Status == KnowledgeStatus.Publicado),
            Borradores = await baseQuery.CountAsync(x => x.Status == KnowledgeStatus.Borrador),
            Vencidos = await baseQuery.CountAsync(x => x.ReviewDueAt != null && x.ReviewDueAt < DateTime.UtcNow && x.Status != KnowledgeStatus.Archivado),
            ActualizadosSemana = await baseQuery.CountAsync(x => x.UpdatedAt >= DateTime.UtcNow.AddDays(-7))
        };

        Categories = KnowledgeCatalog.Categories.ToList();

        Owners = await baseQuery
            .Select(x => x.OwnerName)
            .Where(x => x != "")
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        var q = baseQuery.AsQueryable();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim();
            q = q.Where(x =>
                x.Title.Contains(term) ||
                x.Description.Contains(term) ||
                x.Tags.Contains(term) ||
                x.Body.Contains(term) ||
                x.Category.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(Category))
        {
            var cat = Category.Trim();
            q = q.Where(x => x.Category == cat);
        }

        if (!string.IsNullOrWhiteSpace(Owner))
        {
            var owner = Owner.Trim();
            q = q.Where(x => x.OwnerName == owner);
        }

        if (!string.IsNullOrWhiteSpace(Tag))
        {
            var tag = Tag.Trim();
            q = q.Where(x => x.Tags.Contains(tag));
        }

        if (Enum.TryParse<KnowledgeDocType>(Type, out var t))
            q = q.Where(x => x.DocType == t);

        if (Enum.TryParse<KnowledgeStatus>(Status, out var s))
            q = q.Where(x => x.Status == s);

        if (PinnedOnly)
            q = q.Where(x => x.IsPinned);

        if (DueOnly)
            q = q.Where(x => x.ReviewDueAt != null && x.ReviewDueAt < DateTime.UtcNow && x.Status != KnowledgeStatus.Archivado);

        Rows = await q
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.UpdatedAt)
            .Select(x => new KnowledgeRow(
                x.Id,
                x.Title,
                x.Category,
                x.DocType,
                x.Status,
                x.Description,
                x.OwnerName,
                x.Client != null ? x.Client.Name : "-",
                x.ClientServiceContract != null ? x.ClientServiceContract.Label : "-",
                x.Tags,
                x.UpdatedAt,
                x.ReviewDueAt,
                x.IsPinned,
                x.Version,
                x.ViewCount,
                !string.IsNullOrWhiteSpace(x.AttachmentStoragePath),
                !string.IsNullOrWhiteSpace(x.Url),
                !string.IsNullOrWhiteSpace(x.AccessUsername) || !string.IsNullOrWhiteSpace(x.AccessNotes)
            ))
            .ToListAsync();
    }
}
