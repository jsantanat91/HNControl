using HNControl.Web.Models;

namespace HNControl.Web.Services.Tickets;

public interface ITicketFlowService
{
    Task<Ticket> CreatePublicAsync(
        string clientCode,
        Guid? contractId,
        string requesterName,
        string requesterEmail,
        string requesterPhone,
        string requesterLocation,
        string title,
        string description,
        TicketPriority? priority = null,
        CancellationToken ct = default);

    Task<Ticket> CreateInternalAsync(
        Guid clientId,
        Guid? contractId,
        string requesterName,
        string requesterEmail,
        string requesterPhone,
        string requesterLocation,
        string title,
        string description,
        TicketPriority priority,
        string createdByUserId,
        string createdByName,
        string? assignedToUserId = null,
        string? assignedToName = null,
        CancellationToken ct = default);

    Task<Ticket> CreateMonitoringAutoAsync(Guid targetId, string title, string description, CancellationToken ct = default);
    Task<bool> TryTakeAsync(Guid ticketId, string userId, string userName, bool isAdmin, CancellationToken ct = default);
    Task<bool> TryStartAsync(Guid ticketId, string userId, string userName, bool isAdmin, CancellationToken ct = default);
    Task<bool> TryResolveAsync(Guid ticketId, string userId, string userName, string summary, bool isAdmin, CancellationToken ct = default);
    Task<bool> TryCloseAsync(Guid ticketId, string userId, string userName, bool isAdmin, CancellationToken ct = default);
    Task<bool> AddNoteAsync(Guid ticketId, string userId, string userName, string note, CancellationToken ct = default);
}
