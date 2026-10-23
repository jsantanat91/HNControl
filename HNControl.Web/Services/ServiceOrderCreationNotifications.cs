using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public record ServiceOrderNotificationState(bool Exists, bool EngineeringSent, bool ClientSent, bool EngineeringConfigured)
{
    public bool HasPending => Exists && (!ClientSent || (EngineeringConfigured && !EngineeringSent));
}

public record ServiceOrderNotificationResult(string UserMessage, bool HasPending);

public record ServiceOrderScheduleSnapshot(DateTime? StartDate, DateTime? ExpectedEndDate, bool ScheduledDateConfirmed);

/// <summary>Creation notices are separate from PDF/signature emails and never change the order workflow.</summary>
public sealed class ServiceOrderCreationNotifications(
    ApplicationDbContext db, IEmailSender email, IConfiguration configuration, ILogger<ServiceOrderCreationNotifications> logger)
{
    public const string NoticeKey = "ServiceOrderCreationNotification";
    private const string RequestedMarker = "service-order-created";
    private static string SentMarker(bool engineering) => RequestedMarker + (engineering ? ":engineering" : ":client");

    // Persist this with the order. StartedAt is later overwritten by the operational workflow.
    public void QueueCreated(ServiceOrder order, bool scheduledDateConfirmed = true)
    {
        db.ServiceOrderAuditLogs.Add(new ServiceOrderAuditLog
        {
            OrderId = order.Id,
            EventType = ServiceOrderAuditEvent.CreationNotificationRequested,
            ActorType = "system", ActorId = RequestedMarker, ActorName = "Notificaciones de órdenes",
            Details = JsonSerializer.Serialize(new ServiceOrderScheduleSnapshot(order.StartedAt, order.EstimatedEndDate, scheduledDateConfirmed))
        });
    }

    public async Task<ServiceOrderNotificationState> GetStateAsync(Guid orderId)
    {
        var requested = await db.ServiceOrderAuditLogs.AsNoTracking().AnyAsync(x => x.OrderId == orderId
            && x.EventType == ServiceOrderAuditEvent.CreationNotificationRequested && x.ActorId == RequestedMarker);
        if (!requested) return new(false, false, false, false);
        var sent = await db.ServiceOrderAuditLogs.AsNoTracking().Where(x => x.OrderId == orderId
            && x.EventType == ServiceOrderAuditEvent.EmailSent).Select(x => x.ActorId).ToListAsync();
        var recipients = await EngineeringRecipientsAsync();
        return new(true, sent.Contains(SentMarker(true)), sent.Contains(SentMarker(false)), recipients.Count > 0);
    }

    public async Task<ServiceOrderNotificationResult> NotifyCreatedAsync(Guid orderId)
    {
        // A problem in one delivery must not suppress the other or roll back the saved order.
        var engineering = await TrySendChannelAsync(orderId, engineering: true);
        var client = await TrySendChannelAsync(orderId, engineering: false);
        var pending = engineering == DeliveryOutcome.Pending || client == DeliveryOutcome.Pending;
        var messages = new List<string>();
        if (engineering == DeliveryOutcome.Sent) messages.Add("Ingeniería: notificación enviada.");
        if (engineering == DeliveryOutcome.Disabled) messages.Add("Ingeniería: sin destinatarios configurados.");
        if (engineering == DeliveryOutcome.Pending) messages.Add("Ingeniería: envío pendiente; revise los destinatarios y SMTP.");
        if (client == DeliveryOutcome.Sent) messages.Add("Cliente: notificación enviada.");
        if (client == DeliveryOutcome.Pending) messages.Add("Cliente: envío pendiente; revise su correo registrado y SMTP.");
        if (messages.Count == 0) messages.Add("Esta orden no tiene avisos de creación pendientes.");
        if (pending) messages.Add("Puede reintentar los avisos pendientes desde el detalle administrativo de la orden.");
        return new(string.Join(" ", messages), pending);
    }

    private async Task<DeliveryOutcome> TrySendChannelAsync(Guid orderId, bool engineering)
    {
        try { return await SendChannelAsync(orderId, engineering); }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo notificar la creación de la orden {OrderId} a {Channel}. La orden permanece guardada.",
                orderId, engineering ? "Ingeniería" : "Cliente");
            return DeliveryOutcome.Pending;
        }
    }

    private async Task<DeliveryOutcome> SendChannelAsync(Guid orderId, bool engineering)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Serialize simultaneous retries across instances. SMTP and DB cannot form one atomic transaction;
        // a process failure after SMTP acceptance but before commit can still require manual reconciliation.
        var query = db.Database.IsNpgsql()
            ? db.ServiceOrders.FromSqlInterpolated($"SELECT * FROM \"ServiceOrders\" WHERE \"Id\" = {orderId} FOR UPDATE")
            : db.ServiceOrders.Where(x => x.Id == orderId);
        var order = await query.AsNoTracking().Include(x => x.Client).Include(x => x.ClientServiceContract)
            .Include(x => x.AssignedEmployee).SingleOrDefaultAsync();
        if (order == null) return DeliveryOutcome.NotRequested;
        var requested = await db.ServiceOrderAuditLogs.AsNoTracking().Where(x => x.OrderId == orderId
            && x.EventType == ServiceOrderAuditEvent.CreationNotificationRequested && x.ActorId == RequestedMarker)
            .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync();
        if (requested == null) return DeliveryOutcome.NotRequested; // No bulk mail to historical orders.
        var marker = SentMarker(engineering);
        if (await db.ServiceOrderAuditLogs.AnyAsync(x => x.OrderId == orderId && x.EventType == ServiceOrderAuditEvent.EmailSent && x.ActorId == marker))
            return DeliveryOutcome.Sent;
        var schedule = JsonSerializer.Deserialize<ServiceOrderScheduleSnapshot>(requested.Details ?? "")
            ?? throw new InvalidOperationException("No se encontró la programación original de la orden.");
        string recipient;
        IReadOnlyCollection<string> copies = Array.Empty<string>();
        if (engineering)
        {
            var addresses = await EngineeringRecipientsAsync();
            if (addresses.Count == 0) return DeliveryOutcome.Disabled;
            recipient = addresses.First();
            copies = addresses.Skip(1).ToArray();
        }
        else
        {
            recipient = order.Client == null ? "" : ClientCommunicationTemplates.Recipient(order.Client);
            if (!ClientCommunicationTemplates.IsEmail(recipient))
                throw new InvalidOperationException("Registre un correo válido para el cliente.");
        }
        var reference = ServiceOrderNotificationTemplates.Reference(order);
        var subject = engineering ? $"Nueva orden de servicio {reference} · {order.Title}" : $"HN Solutions · Confirmación de orden de servicio {reference}";
        await email.SendAsync(recipient, subject.Replace('\r', ' ').Replace('\n', ' '),
            engineering ? ServiceOrderNotificationTemplates.Engineering(order, schedule, configuration["PublicLinks:BaseUrl"])
                : ServiceOrderNotificationTemplates.Client(order, schedule), ccEmails: copies);
        var receipt = new ServiceOrderAuditLog
        {
            OrderId = orderId, EventType = ServiceOrderAuditEvent.EmailSent,
            ActorType = "system", ActorId = marker, ActorName = "Notificaciones de órdenes",
            Details = engineering ? "Aviso de creación enviado a Ingeniería." : "Aviso de creación enviado al cliente."
        };
        db.ServiceOrderAuditLogs.Add(receipt);
        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            // A failed receipt must never be persisted by the next channel's SaveChanges.
            db.Entry(receipt).State = EntityState.Detached;
            throw;
        }
        return DeliveryOutcome.Sent;
    }

    private async Task<IReadOnlyCollection<string>> EngineeringRecipientsAsync()
    {
        var value = await db.SystemConfigurations.AsNoTracking().OrderByDescending(x => x.UpdatedAt)
            .Select(x => x.EngineeringNotificationEmails).FirstOrDefaultAsync();
        return DocumentNotificationRecipients.Parse(value ?? SystemConfiguration.DefaultEngineeringNotificationEmails);
    }

    private enum DeliveryOutcome { Sent, Pending, Disabled, NotRequested }
}
