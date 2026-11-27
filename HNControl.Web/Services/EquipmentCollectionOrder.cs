using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public static class EquipmentCollectionOrder
{
    public static readonly string[] PhotoExtensions = [".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif"];
    public static bool IsPhoto(string filename) => PhotoExtensions.Contains(Path.GetExtension(filename), StringComparer.OrdinalIgnoreCase);

    public static async Task<string?> ValidateClosureAsync(ApplicationDbContext db, ServiceOrder order)
    {
        if (string.IsNullOrWhiteSpace(order.Description)) return "Captura la dirección de recolección antes de finalizar.";
        var photos = await db.ServiceOrderEvidences.Where(e => e.OrderId == order.Id && e.SizeBytes > 0)
            .Select(e => e.OriginalFileName).ToListAsync();
        return photos.Any(IsPhoto) ? null : "Adjunta al menos una foto de la recolección antes de finalizar.";
    }

    // Caller holds the order lock and commits the transaction, including the audit and final status.
    public static async Task CompleteAsync(ApplicationDbContext db, ServiceOrder order, string? actorId, string? actorName)
    {
        if (order.Type != ServiceOrderType.RecoleccionEquipos) throw new InvalidOperationException("La orden no es de recolección.");
        if (order.Status == ServiceOrderStatus.Finalized) return;
        order.PdfStoragePath = null;
        order.PdfGeneratedAt = null;
        order.TokenClosedAt = DateTime.UtcNow;
        db.ServiceOrderAuditLogs.Add(new ServiceOrderAuditLog
        {
            OrderId = order.Id, EventType = ServiceOrderAuditEvent.Completed,
            ActorType = "employee", ActorId = actorId, ActorName = actorName,
            Details = "Recolección finalizada con evidencia fotográfica. Sin bonificación."
        });
        await ServiceOrderCommissionService.FinalizeAsync(db, order, DateTime.UtcNow);
    }
}
