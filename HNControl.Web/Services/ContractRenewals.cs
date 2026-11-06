using System.Globalization;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public sealed record RenewalTerm(int Months, DateTime End, bool InferredMonths)
{
    public DateTime NextEnd => End.AddDays(1).AddMonths(Months).AddDays(-1);
    public bool IsDue(DateTime today) => End >= today.Date && End <= today.Date.AddMonths(3);
}

public sealed record ExpiringContract(Guid Id, Guid ClientId, string ClientName, string Number, string Service,
    string Branch, RenewalTerm Term, long Revision, bool CalculatedEnd);

public sealed record ContractExpiryNotice(IReadOnlyList<ExpiringContract> Contracts, bool CanRenew,
    bool InClientFile = false, bool InPortal = false);

public static class ContractRenewals
{
    public static RenewalTerm? Term(ClientServiceContract contract)
    {
        var meta = ClientServiceContractMetadata.ParseMeta(contract.Notes);
        var recurrence = meta.GetValueOrDefault("Recurrencia", "");
        if (recurrence.Equals("Eventual", StringComparison.OrdinalIgnoreCase)
            || recurrence.Equals("Unica", StringComparison.OrdinalIgnoreCase)
            || recurrence.Equals("Única", StringComparison.OrdinalIgnoreCase)) return null;

        var start = contract.ContractStartDate?.Date;
        var end = contract.ContractEndDate?.Date;
        if (end < start || end == DateTime.MaxValue.Date) return null;
        var raw = meta.GetValueOrDefault("Plazo", "");
        int months;
        if (raw.Length > 0)
        {
            var parts = raw.Split(' ', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && !parts[1].Equals("mes", StringComparison.OrdinalIgnoreCase)
                && !parts[1].Equals("meses", StringComparison.OrdinalIgnoreCase)) return null;
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out months) || months <= 0)
                return null;
        }
        else
        {
            // Older records may identify a whole-month term only by its two dates.
            if (start == null || end == null) return null;
            var afterEnd = end.Value.AddDays(1);
            months = (afterEnd.Year - start.Value.Year) * 12 + afterEnd.Month - start.Value.Month;
            if (months <= 0 || start.Value.AddMonths(months) != afterEnd)
            {
                months = (end.Value.Year - start.Value.Year) * 12 + end.Value.Month - start.Value.Month;
                if (months <= 0 || start.Value.AddMonths(months) != end) return null;
            }
        }
        if (end == null && start == null) return null;
        // Date fields cover both the first and last day of service.
        if (end == null)
        {
            if (!CanAddMonths(start!.Value, months)) return null;
            end = start.Value.AddMonths(months).AddDays(-1);
        }
        if (!CanAddMonths(end.Value.AddDays(1), months)) return null;
        return new(months, end.Value, raw.Length == 0);
    }

    private static bool CanAddMonths(DateTime date, int months) =>
        months <= (DateTime.MaxValue.Year - date.Year) * 12 + 12 - date.Month;

    public static async Task<List<ExpiringContract>> UpcomingAsync(IQueryable<ClientServiceContract> scope, DateTime today)
    {
        var limit = today.AddMonths(3);
        var candidates = await scope.AsNoTracking()
            .Where(c => c.Client!.IsActive && !c.Client.IsTemporaryLead &&
                ((c.ContractEndDate >= today && c.ContractEndDate <= limit) ||
                 (c.ContractEndDate == null && c.ContractStartDate != null && c.ContractStartDate <= limit)))
            .Select(c => new { ClientName = c.Client!.Name, Contract = new ClientServiceContract
            {
                Id = c.Id, ClientId = c.ClientId, Label = c.Label, ContractNumber = c.ContractNumber, Branch = c.Branch,
                ServiceType = c.ServiceType, Notes = c.Notes, ContractStartDate = c.ContractStartDate,
                ContractEndDate = c.ContractEndDate, UpdatedAt = c.UpdatedAt
            } }).ToListAsync();
        return candidates.Select(c => Upcoming(c.Contract, c.ClientName, today)).OfType<ExpiringContract>()
            .OrderBy(c => c.Term.End).ThenBy(c => c.ClientName).ThenBy(c => c.Number).ToList();
    }

    public static ExpiringContract? Upcoming(ClientServiceContract c, string clientName, DateTime today) =>
        Term(c) is { } term && term.IsDue(today)
            ? new(c.Id, c.ClientId, clientName, c.ContractNumber, ClientServiceContractMetadata.ServiceName(c),
                c.Branch, term, c.UpdatedAt.Ticks, c.ContractEndDate == null)
            : null;

    public static async Task<bool> RenewAsync(IQueryable<ClientServiceContract> scope, Guid id, long revision, DateTime today)
    {
        var target = scope.Where(c => c.Id == id && c.Client!.IsActive && !c.Client.IsTemporaryLead);
        var contract = await target.AsNoTracking().SingleOrDefaultAsync();
        if (contract == null || contract.UpdatedAt.Ticks != revision || Term(contract) is not { } term || !term.IsDue(today))
            return false;

        // Persist an inferred legacy term before extending it, so later renewals use the same duration.
        var notes = contract.Notes;
        if (term.InferredMonths) notes = (notes.TrimEnd() + "\n[META] Plazo=" + term.Months).TrimStart('\n');
        if (notes.Length > 2000) return false;
        var end = term.NextEnd;
        var updated = DateTime.UtcNow;
        var changed = await target.Where(c => c.UpdatedAt == contract.UpdatedAt && c.ContractEndDate == contract.ContractEndDate
                && c.ContractStartDate == contract.ContractStartDate && c.Notes == contract.Notes)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.ContractEndDate, end)
                .SetProperty(c => c.Notes, notes).SetProperty(c => c.UpdatedAt, updated));
        return changed == 1;
    }
}
