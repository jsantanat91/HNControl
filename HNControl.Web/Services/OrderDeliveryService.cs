using System.Security.Claims;
using HNControl.Shared;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public static class OrderDeliveryService
{
    public static string Version(ServiceOrder order) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(order.DeliveryJson)));
    public static bool CanAccess(ClaimsPrincipal user) => AppRoles.IsGlobalAdmin(user) || user.IsInRole(AppRoles.Employee);
    public static bool CanEdit(ServiceOrder order, ClaimsPrincipal user) => CanAccess(user)
        && order.Status is ServiceOrderStatus.Created or ServiceOrderStatus.InProgress or ServiceOrderStatus.Rejected
        && (AppRoles.IsGlobalAdmin(user) || order.ClaimedByUserId == user.FindFirstValue(ClaimTypes.NameIdentifier))
        && order.Signatures.Count == 0;

    public static async Task<string?> SaveAsync(ApplicationDbContext db, Guid id, ClaimsPrincipal user, OrderDeliveryData data, string expected)
    {
        var order = await db.ServiceOrders.AsNoTracking().Include(x => x.Signatures).SingleOrDefaultAsync(x => x.Id == id);
        if (order == null || !CanEdit(order, user)) return "Esta orden no permite modificar la entrega. Debes tomarla y no debe tener firmas ni estar en revisión o finalizada.";
        var error = data.Validate();
        if (error != null) return error;
        var json = data.Serialize();
        var affected = await db.ServiceOrders.Where(x => x.Id == id && x.DeliveryJson == expected
            && (x.Status == ServiceOrderStatus.Created || x.Status == ServiceOrderStatus.InProgress || x.Status == ServiceOrderStatus.Rejected)
            && !x.Signatures.Any() && (AppRoles.IsGlobalAdmin(user) || x.ClaimedByUserId == user.FindFirstValue(ClaimTypes.NameIdentifier)))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DeliveryJson, json).SetProperty(x => x.PdfStoragePath, (string?)null).SetProperty(x => x.PdfGeneratedAt, (DateTime?)null));
        return affected == 1 ? null : "La orden cambió mientras editabas. Vuelve a abrirla antes de guardar.";
    }
}
