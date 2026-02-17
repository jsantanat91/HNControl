using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public static class ViaticTotalsHelper
{
    public static async Task RecalcWeekAsync(ApplicationDbContext db, Guid weekId, CancellationToken ct = default)
    {
        var week = await db.ViaticWeeks
            .Include(w => w.Entries)
            .ThenInclude(e => e.Attachment)
            .FirstOrDefaultAsync(w => w.Id == weekId, ct);

        if (week == null) return;

        var total = week.Entries.Sum(e => e.Amount);
        var billable = week.Entries.Where(e => e.IsBillable).Sum(e => e.Amount);

        week.TotalAmount = total;
        week.BillableAmount = billable;
        week.UpdatedAt = DateTime.UtcNow;
    }
}
