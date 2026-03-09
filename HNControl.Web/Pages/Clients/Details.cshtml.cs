using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients;

[Authorize(Roles = AppRoles.Admin)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _cfg;
    public DetailsModel(ApplicationDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    public Client? Client { get; set; }

    public record ContractRow(
        Guid Id,
        string ServiceType,
        string Label,
        string Provider,
        string AccountNumber,
        string ContractNumber,
        decimal? MonthlyAmount,
        string ContractEndDateText,
        string StatusText,
        string StatusBadgeClass,
        bool HasPortalAccess,
        bool HasContractFile,
        string ProjectTitle
    );

    public List<ContractRow> Contracts { get; set; } = new();

    public record ProjectRow(Guid Id, string Title, string StartDate, string EstEnd, string Status);
    public List<ProjectRow> Projects { get; set; } = new();
    public string PublicQuoteUrl { get; set; } = string.Empty;

    public async Task OnGetAsync(Guid id)
    {
        Client = await _db.Clients
            .Include(c => c.Contracts)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (Client == null) return;

        if (string.IsNullOrWhiteSpace(Client.ClientCode))
        {
            Client.ClientCode = await NextClientCodeAsync();
            await _db.SaveChangesAsync();
        }

        if (string.IsNullOrWhiteSpace(Client.PublicQuoteToken))
        {
            Client.PublicQuoteToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
            await _db.SaveChangesAsync();
        }

        var baseUrl = (_cfg["PublicLinks:BaseUrl"] ?? "").Trim().TrimEnd('/');
        PublicQuoteUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? $"/cotizar/{Client.PublicQuoteToken}"
            : $"{baseUrl}/cotizar/{Client.PublicQuoteToken}";

        var projMap = await _db.Projects
            .Where(p => p.ClientId == id)
            .Select(p => new { p.Id, p.Title })
            .ToDictionaryAsync(x => x.Id, x => x.Title);

        var today = DateTime.Today;
        var soon = today.AddDays(30);

        Contracts = Client.Contracts
            .OrderBy(x => x.ServiceType)
            .ThenBy(x => x.Label)
            .Select(x =>
            {
                var end = x.ContractEndDate?.Date;
                var status = "Activo";
                var badge = "text-bg-success";

                if (end.HasValue && end.Value < today)
                {
                    status = "Vencido";
                    badge = "text-bg-danger";
                }
                else if (end.HasValue && end.Value <= soon)
                {
                    status = "Por vencer";
                    badge = "text-bg-warning";
                }

                var endText = end.HasValue ? end.Value.ToString("yyyy-MM-dd") : "—";

                var projTitle = (x.ProjectId.HasValue && projMap.TryGetValue(x.ProjectId.Value, out var t))
                    ? t
                    : "—";

                return new ContractRow(
                    x.Id,
                    x.ServiceType.ToString(),
                    x.Label,
                    x.Provider,
                    x.AccountNumber,
                    x.ContractNumber,
                    x.MonthlyAmount,
                    endText,
                    status,
                    badge,
                    !string.IsNullOrWhiteSpace(x.PortalUrl) || !string.IsNullOrWhiteSpace(x.PortalUsername),
                    !string.IsNullOrWhiteSpace(x.SignedContractStoragePath),
                    projTitle
                );
            })
            .ToList();

        var projs = await _db.Projects
            .Include(p => p.AssignedEmployee)
            .Where(p => p.ClientId == id)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();

        Projects = projs.Select(p => new ProjectRow(
            p.Id,
            p.Title,
            p.StartDate.ToString("yyyy-MM-dd"),
            p.EstimatedEndDate.ToString("yyyy-MM-dd"),
            p.Status.ToString()
        )).ToList();
    }

    private async Task<string> NextClientCodeAsync()
    {
        var codes = await _db.Clients
            .AsNoTracking()
            .Where(c => !string.IsNullOrWhiteSpace(c.ClientCode) && c.ClientCode.StartsWith("HN-"))
            .Select(c => c.ClientCode)
            .ToListAsync();

        var max = 0;
        foreach (var code in codes)
        {
            if (int.TryParse(code.AsSpan(3), out var n) && n > max)
                max = n;
        }

        return $"HN-{max + 1:0000}";
    }
}
