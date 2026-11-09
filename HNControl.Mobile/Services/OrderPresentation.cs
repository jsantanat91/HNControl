using HNControl.Mobile.Models;

namespace HNControl.Mobile.Services;

public static class OrderPresentation
{
    public static List<ServiceOrderListItemDto> Filter(IEnumerable<ServiceOrderListItemDto> items, string filter, string? query)
    {
        var search = (query ?? "").Trim();
        return items.Where(x => filter switch
            {
                "mine" => x.IsMine && x.Status != 4,
                "available" => x.CanTake,
                "closed" => x.Status == 4,
                _ => true
            })
            .Where(x => search.Length == 0
                || x.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || x.Client.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || x.ClaimedBy.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(x => x.Status == 4)
            .ThenByDescending(x => x.IsOverdue)
            .ThenByDescending(x => x.IsMine)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();
    }
}
