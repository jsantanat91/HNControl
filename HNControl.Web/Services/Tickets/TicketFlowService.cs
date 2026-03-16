using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services.Tickets;

public class TicketFlowService : ITicketFlowService
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;

    public TicketFlowService(ApplicationDbContext db, IFileStorage storage, IEmailSender email, IConfiguration cfg)
    {
        _db = db;
        _storage = storage;
        _email = email;
        _cfg = cfg;
    }

    public async Task<Ticket> CreatePublicAsync(
        string clientCode,
        Guid? contractId,
        string requesterName,
        string requesterEmail,
        string requesterPhone,
        string requesterLocation,
        string title,
        string description,
        TicketPriority? priority = null,
        CancellationToken ct = default)
    {
        var code = (clientCode ?? "").Trim().ToUpperInvariant();
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.ClientCode == code, ct)
            ?? throw new InvalidOperationException("No existe un cliente con ese codigo.");

        ClientServiceContract? contract = null;
        if (contractId.HasValue)
        {
            contract = await _db.ClientServiceContracts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == contractId.Value && x.ClientId == client.Id, ct)
                ?? throw new InvalidOperationException("Contrato invalido para el cliente.");
        }

        var (autoPriority, autoImpact, autoUrgency) = CalculatePriorityFromText(title, description);
        var finalPriority = priority ?? autoPriority;
        var finalImpact = ToImpact(finalPriority);
        var finalUrgency = ToUrgency(finalPriority);
        var now = DateTime.UtcNow;
        var location = !string.IsNullOrWhiteSpace(contract?.BranchAddress)
            ? contract!.BranchAddress
            : (!string.IsNullOrWhiteSpace(contract?.Branch) ? contract!.Branch : requesterLocation);
        var ticket = new Ticket
        {
            TicketNumber = await NextTicketNumberAsync(ct),
            ClientId = client.Id,
            ClientServiceContractId = contract?.Id,
            Title = (title ?? "").Trim(),
            Description = (description ?? "").Trim(),
            Source = TicketSource.PublicPortal,
            Category = "Incidente",
            Subcategory = "Portal cliente",
            Priority = finalPriority,
            Impact = finalImpact,
            Urgency = finalUrgency,
            Status = TicketStatus.New,
            RequesterName = (requesterName ?? "").Trim(),
            RequesterEmail = (requesterEmail ?? "").Trim(),
            RequesterPhone = (requesterPhone ?? "").Trim(),
            RequesterLocation = (location ?? "").Trim(),
            CreatedByName = "Portal publico",
            CreatedAt = now,
            UpdatedAt = now
        };
        SetSla(ticket, now);

        ticket.Events.Add(new TicketEvent
        {
            EventType = "Created",
            UserName = "Portal publico",
            Message = $"Ticket levantado por cliente {client.ClientCode}.",
            CreatedAt = now
        });

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct);
        await SendCreatedEmailAsync(ticket, client.Name, contract?.Branch, ct);
        return ticket;
    }

    public async Task<Ticket> CreateInternalAsync(
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
        CancellationToken ct = default)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == clientId, ct)
            ?? throw new InvalidOperationException("Cliente no encontrado.");

        ClientServiceContract? contract = null;
        if (contractId.HasValue)
        {
            contract = await _db.ClientServiceContracts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == contractId.Value && x.ClientId == clientId, ct)
                ?? throw new InvalidOperationException("Contrato invalido para el cliente.");
        }

        var now = DateTime.UtcNow;
        var location = !string.IsNullOrWhiteSpace(contract?.BranchAddress)
            ? contract!.BranchAddress
            : (!string.IsNullOrWhiteSpace(contract?.Branch) ? contract!.Branch : requesterLocation);
        var ticket = new Ticket
        {
            TicketNumber = await NextTicketNumberAsync(ct),
            ClientId = client.Id,
            ClientServiceContractId = contract?.Id,
            Title = (title ?? "").Trim(),
            Description = (description ?? "").Trim(),
            Source = TicketSource.InternalManual,
            Category = "Incidente",
            Subcategory = "Operacion interna",
            Priority = priority,
            Impact = ToImpact(priority),
            Urgency = ToUrgency(priority),
            Status = string.IsNullOrWhiteSpace(assignedToUserId) ? TicketStatus.New : TicketStatus.Assigned,
            RequesterName = (requesterName ?? "").Trim(),
            RequesterEmail = (requesterEmail ?? "").Trim(),
            RequesterPhone = (requesterPhone ?? "").Trim(),
            RequesterLocation = (location ?? "").Trim(),
            CreatedByUserId = string.IsNullOrWhiteSpace(createdByUserId) ? null : createdByUserId.Trim(),
            CreatedByName = string.IsNullOrWhiteSpace(createdByName) ? "Admin" : createdByName.Trim(),
            AssignedToUserId = string.IsNullOrWhiteSpace(assignedToUserId) ? null : assignedToUserId.Trim(),
            AssignedToName = string.IsNullOrWhiteSpace(assignedToName) ? "" : assignedToName.Trim(),
            AssignedAt = string.IsNullOrWhiteSpace(assignedToUserId) ? null : now,
            CreatedAt = now,
            UpdatedAt = now
        };
        SetSla(ticket, now);

        ticket.Events.Add(new TicketEvent
        {
            EventType = "Created",
            UserId = ticket.CreatedByUserId ?? "",
            UserName = ticket.CreatedByName,
            Message = "Ticket creado manualmente por admin.",
            CreatedAt = now
        });

        if (!string.IsNullOrWhiteSpace(ticket.AssignedToUserId))
        {
            ticket.Events.Add(new TicketEvent
            {
                EventType = "Assigned",
                UserId = ticket.CreatedByUserId ?? "",
                UserName = ticket.CreatedByName,
                Message = $"Asignado a {ticket.AssignedToName}.",
                CreatedAt = now
            });
        }

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct);
        return ticket;
    }

    public async Task<Ticket> CreateMonitoringAutoAsync(Guid targetId, string title, string description, CancellationToken ct = default)
    {
        var target = await _db.MonitorTargets
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.ClientServiceContract)
            .FirstOrDefaultAsync(x => x.Id == targetId, ct)
            ?? throw new InvalidOperationException("Target de monitoreo no encontrado.");

        // Evita duplicados: si ya hay ticket abierto por el mismo target, reusa.
        var existing = await _db.Tickets
            .FirstOrDefaultAsync(t => t.MonitorTargetId == targetId
                && t.Status != TicketStatus.Closed
                && t.Status != TicketStatus.Cancelled, ct);
        if (existing != null) return existing;

        var now = DateTime.UtcNow;
        var location = !string.IsNullOrWhiteSpace(target.ClientServiceContract?.BranchAddress)
            ? target.ClientServiceContract!.BranchAddress
            : (!string.IsNullOrWhiteSpace(target.ClientServiceContract?.Branch)
                ? target.ClientServiceContract!.Branch
                : (target.Client?.Address ?? ""));
        var ticket = new Ticket
        {
            TicketNumber = await NextTicketNumberAsync(ct),
            ClientId = target.ClientId,
            ClientServiceContractId = target.ClientServiceContractId,
            MonitorTargetId = target.Id,
            Title = string.IsNullOrWhiteSpace(title) ? $"Caida detectada: {target.Name}" : title.Trim(),
            Description = string.IsNullOrWhiteSpace(description)
                ? $"Monitoreo detecto falla en {target.Name}. Ultimo error: {target.LastError}"
                : description.Trim(),
            Source = TicketSource.MonitoringAuto,
            Category = "Incidente",
            Subcategory = "Monitoreo",
            Priority = TicketPriority.Critical,
            Impact = TicketImpact.High,
            Urgency = TicketUrgency.High,
            Status = TicketStatus.New,
            RequesterName = target.Client?.Name ?? "Monitoreo",
            RequesterEmail = target.Client?.Email ?? "",
            RequesterPhone = target.Client?.Phone ?? "",
            RequesterLocation = location,
            CreatedByName = "MonitorWorker",
            CreatedAt = now,
            UpdatedAt = now
        };
        SetSla(ticket, now);

        ticket.Events.Add(new TicketEvent
        {
            EventType = "CreatedAuto",
            UserName = "MonitorWorker",
            Message = $"Ticket automatico por caida en target {target.Name}.",
            CreatedAt = now
        });

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct);
        return ticket;
    }

    public async Task<bool> TryTakeAsync(Guid ticketId, string userId, string userName, bool isAdmin, CancellationToken ct = default)
    {
        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
        if (t == null) return false;
        if (t.Status is TicketStatus.Resolved or TicketStatus.Closed or TicketStatus.Cancelled) return false;

        var now = DateTime.UtcNow;
        var previousOwner = string.IsNullOrWhiteSpace(t.AssignedToName) ? "Sin asignar" : t.AssignedToName;
        t.AssignedToUserId = userId;
        t.AssignedToName = userName;
        t.AssignedAt ??= now;
        t.FirstResponseAt ??= now;
        if (t.Status == TicketStatus.New) t.Status = TicketStatus.Assigned;
        t.UpdatedAt = now;
        EvaluateSla(t, now);

        _db.TicketEvents.Add(new TicketEvent
        {
            TicketId = t.Id,
            EventType = "Taken",
            UserId = userId,
            UserName = userName,
            Message = $"Ticket tomado por tecnico. Antes: {previousOwner}.",
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        var clientName = await _db.Clients
            .AsNoTracking()
            .Where(c => c.Id == t.ClientId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct);

        var branch = await _db.ClientServiceContracts
            .AsNoTracking()
            .Where(c => c.Id == t.ClientServiceContractId)
            .Select(c => c.Branch)
            .FirstOrDefaultAsync(ct);

        await SendClosedEmailAsync(t, clientName, branch, ct);
        return true;
    }

    private async Task SendCreatedEmailAsync(Ticket ticket, string? clientName, string? branch, CancellationToken ct)
    {
        var to = (ticket.RequesterEmail ?? "").Trim();
        if (string.IsNullOrWhiteSpace(to)) return;

        var baseUrl = (_cfg["PublicLinks:BaseUrl"] ?? "").Trim().TrimEnd('/');
        var portalUrl = string.IsNullOrWhiteSpace(baseUrl) ? "/ticket-publico" : $"{baseUrl}/ticket-publico";
        var sucursal = string.IsNullOrWhiteSpace(branch) ? "No especificada" : branch.Trim();

        var subject = $"Ticket recibido: {ticket.TicketNumber}";
        var body = $@"
            <div style='font-family:Segoe UI,Arial,sans-serif;line-height:1.5;color:#0f172a'>
                <h2 style='margin:0 0 10px 0'>HN Control · Ticket recibido</h2>
                <p>Hola <strong>{System.Net.WebUtility.HtmlEncode(ticket.RequesterName)}</strong>,</p>
                <p>Hemos recibido tu solicitud y ya fue registrada con el folio <strong>{ticket.TicketNumber}</strong>.</p>
                <p>
                    <strong>Cliente:</strong> {System.Net.WebUtility.HtmlEncode(clientName ?? "-")}<br/>
                    <strong>Sucursal:</strong> {System.Net.WebUtility.HtmlEncode(sucursal)}<br/>
                    <strong>Asunto:</strong> {System.Net.WebUtility.HtmlEncode(ticket.Title)}<br/>
                    <strong>Prioridad:</strong> {ToPriorityLabel(ticket.Priority)}<br/>
                    <strong>Estado inicial:</strong> Nuevo
                </p>
                <p>El equipo de soporte dará seguimiento y te contactará en cuanto avancemos con el caso.</p>
                <p>Puedes consultar el portal en: <a href='{portalUrl}'>{portalUrl}</a></p>
                <hr style='border:none;border-top:1px solid #e2e8f0'/>
                <small>Mensaje automático de HN Control.</small>
            </div>";

        try
        {
            await _email.SendAsync(to, subject, body);
        }
        catch
        {
            // No interrumpir el flujo principal por errores de email.
        }
    }

    private async Task SendClosedEmailAsync(Ticket ticket, string? clientName, string? branch, CancellationToken ct)
    {
        var to = (ticket.RequesterEmail ?? "").Trim();
        if (string.IsNullOrWhiteSpace(to)) return;

        var baseUrl = (_cfg["PublicLinks:BaseUrl"] ?? "").Trim().TrimEnd('/');
        var portalUrl = string.IsNullOrWhiteSpace(baseUrl) ? "/ticket-publico" : $"{baseUrl}/ticket-publico";
        var sucursal = string.IsNullOrWhiteSpace(branch) ? "No especificada" : branch.Trim();
        var closedAt = (ticket.ClosedAt ?? DateTime.UtcNow).ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        var subject = $"Ticket cerrado: {ticket.TicketNumber}";
        var body = $@"
            <div style='font-family:Segoe UI,Arial,sans-serif;line-height:1.5;color:#0f172a'>
                <h2 style='margin:0 0 10px 0'>HN Control · Ticket cerrado</h2>
                <p>Hola <strong>{System.Net.WebUtility.HtmlEncode(ticket.RequesterName)}</strong>,</p>
                <p>Te confirmamos que tu ticket <strong>{ticket.TicketNumber}</strong> ha sido cerrado.</p>
                <p>
                    <strong>Cliente:</strong> {System.Net.WebUtility.HtmlEncode(clientName ?? "-")}<br/>
                    <strong>Sucursal:</strong> {System.Net.WebUtility.HtmlEncode(sucursal)}<br/>
                    <strong>Asunto:</strong> {System.Net.WebUtility.HtmlEncode(ticket.Title)}<br/>
                    <strong>Fecha de cierre:</strong> {closedAt}
                </p>
                <p><strong>Resumen:</strong> {System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(ticket.ResolutionSummary) ? "Cierre de ticket registrado." : ticket.ResolutionSummary)}</p>
                <p>Para consultar historial, entra a: <a href='{portalUrl}'>{portalUrl}</a></p>
                <hr style='border:none;border-top:1px solid #e2e8f0'/>
                <small>Mensaje automático de HN Control.</small>
            </div>";

        try
        {
            await _email.SendAsync(to, subject, body);
        }
        catch
        {
            // No interrumpir el flujo principal por errores de email.
        }
    }

    private static string ToPriorityLabel(TicketPriority p) => p switch
    {
        TicketPriority.Low => "Baja",
        TicketPriority.Medium => "Intermedia",
        TicketPriority.High => "Alta",
        TicketPriority.Critical => "Crítica",
        _ => "Intermedia"
    };

    public async Task<bool> TryStartAsync(Guid ticketId, string userId, string userName, bool isAdmin, CancellationToken ct = default)
    {
        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
        if (t == null) return false;
        if (!CanOperate(t, userId, isAdmin)) return false;

        var now = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(t.AssignedToUserId))
        {
            t.AssignedToUserId = userId;
            t.AssignedToName = userName;
            t.AssignedAt ??= now;
        }

        t.FirstResponseAt ??= now;
        t.StartedAt ??= now;
        t.Status = TicketStatus.InProgress;
        t.UpdatedAt = now;
        EvaluateSla(t, now);

        _db.TicketEvents.Add(new TicketEvent
        {
            TicketId = t.Id,
            EventType = "Started",
            UserId = userId,
            UserName = userName,
            Message = "Trabajo tecnico iniciado.",
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> TryResolveAsync(Guid ticketId, string userId, string userName, string summary, bool isAdmin, CancellationToken ct = default)
    {
        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
        if (t == null) return false;
        if (!CanOperate(t, userId, isAdmin)) return false;

        var now = DateTime.UtcNow;
        t.Status = TicketStatus.Resolved;
        t.ResolvedAt = now;
        t.UpdatedAt = now;
        t.ResolutionSummary = (summary ?? "").Trim();
        EvaluateSla(t, now);

        _db.TicketEvents.Add(new TicketEvent
        {
            TicketId = t.Id,
            EventType = "Resolved",
            UserId = userId,
            UserName = userName,
            Message = string.IsNullOrWhiteSpace(summary) ? "Ticket resuelto." : summary.Trim(),
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> TryCloseAsync(Guid ticketId, string userId, string userName, bool isAdmin, CancellationToken ct = default)
    {
        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
        if (t == null) return false;
        if (t.Status is TicketStatus.Closed or TicketStatus.Cancelled) return false;

        var now = DateTime.UtcNow;
        var previousOwner = string.IsNullOrWhiteSpace(t.AssignedToName) ? "Sin asignar" : t.AssignedToName;
        t.AssignedToUserId = userId;
        t.AssignedToName = userName;
        t.AssignedAt ??= now;
        t.Status = TicketStatus.Closed;
        t.ClosedAt = now;
        t.UpdatedAt = now;
        EvaluateSla(t, now);

        _db.TicketEvents.Add(new TicketEvent
        {
            TicketId = t.Id,
            EventType = "Closed",
            UserId = userId,
            UserName = userName,
            Message = $"Ticket cerrado por {userName}. Asignado previo: {previousOwner}.",
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AddNoteAsync(Guid ticketId, string userId, string userName, string note, CancellationToken ct = default)
    {
        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
        if (t == null || string.IsNullOrWhiteSpace(note)) return false;

        var now = DateTime.UtcNow;
        t.UpdatedAt = now;
        EvaluateSla(t, now);
        _db.TicketEvents.Add(new TicketEvent
        {
            TicketId = t.Id,
            EventType = "Note",
            UserId = userId,
            UserName = userName,
            Message = note.Trim(),
            CreatedAt = now
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AddNoteWithEvidenceAsync(
        Guid ticketId,
        string userId,
        string userName,
        string note,
        IFormFile? evidence,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
        if (t == null) return false;
        if (t.Status is TicketStatus.Closed or TicketStatus.Cancelled) return false;
        if (string.IsNullOrWhiteSpace(note) && (evidence == null || evidence.Length <= 0)) return false;

        var now = DateTime.UtcNow;
        t.UpdatedAt = now;
        EvaluateSla(t, now);

        if (!string.IsNullOrWhiteSpace(note))
        {
            _db.TicketEvents.Add(new TicketEvent
            {
                TicketId = t.Id,
                EventType = "Note",
                UserId = userId,
                UserName = userName,
                Message = note.Trim(),
                CreatedAt = now
            });
        }

        if (evidence != null && evidence.Length > 0)
        {
            var allowed = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".heic" };
            var att = new TicketAttachment
            {
                TicketId = t.Id,
                OriginalFileName = Path.GetFileName(evidence.FileName),
                UploadedAt = now,
                UploadedByUserId = userId,
                UploadedByName = userName
            };
            var (path, size, contentType, originalName) = await _storage.SaveFileAsync(
                evidence,
                $"tickets/{t.Id:N}",
                att.Id.ToString("N"),
                allowed,
                25 * 1024 * 1024);

            att.StoragePath = path;
            att.SizeBytes = size;
            att.ContentType = contentType;
            att.OriginalFileName = string.IsNullOrWhiteSpace(originalName) ? att.OriginalFileName : originalName;
            _db.TicketAttachments.Add(att);

            _db.TicketEvents.Add(new TicketEvent
            {
                TicketId = t.Id,
                EventType = "Evidence",
                UserId = userId,
                UserName = userName,
                Message = $"Adjunto: {att.OriginalFileName}",
                CreatedAt = now
            });
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static bool CanOperate(Ticket t, string userId, bool isAdmin)
    {
        if (t.Status is TicketStatus.Closed or TicketStatus.Cancelled) return false;
        if (isAdmin) return true;
        if (string.IsNullOrWhiteSpace(t.AssignedToUserId)) return true;
        return t.AssignedToUserId == userId;
    }

    private static (TicketPriority priority, TicketImpact impact, TicketUrgency urgency) CalculatePriorityFromText(string title, string description)
    {
        var text = $"{title} {description}".ToLowerInvariant();
        if (text.Contains("caida") || text.Contains("down") || text.Contains("sin servicio"))
            return (TicketPriority.Critical, TicketImpact.High, TicketUrgency.High);
        if (text.Contains("intermitente") || text.Contains("lento"))
            return (TicketPriority.High, TicketImpact.Medium, TicketUrgency.High);
        return (TicketPriority.Medium, TicketImpact.Medium, TicketUrgency.Medium);
    }

    private static TicketImpact ToImpact(TicketPriority priority) => priority switch
    {
        TicketPriority.Critical => TicketImpact.High,
        TicketPriority.High => TicketImpact.High,
        TicketPriority.Medium => TicketImpact.Medium,
        _ => TicketImpact.Low
    };

    private static TicketUrgency ToUrgency(TicketPriority priority) => priority switch
    {
        TicketPriority.Critical => TicketUrgency.High,
        TicketPriority.High => TicketUrgency.High,
        TicketPriority.Medium => TicketUrgency.Medium,
        _ => TicketUrgency.Low
    };

    private static void SetSla(Ticket t, DateTime now)
    {
        const int responseHours = 8;
        const int resolutionHours = 8;

        t.SlaResponseDueAt = now.AddHours(responseHours);
        t.SlaResolutionDueAt = now.AddHours(resolutionHours);
        t.SlaBreachedResponse = false;
        t.SlaBreachedResolution = false;
    }

    private static void EvaluateSla(Ticket t, DateTime now)
    {
        if (!t.FirstResponseAt.HasValue && now > t.SlaResponseDueAt)
            t.SlaBreachedResponse = true;
        if (!t.ResolvedAt.HasValue && now > t.SlaResolutionDueAt)
            t.SlaBreachedResolution = true;
    }

    private async Task<string> NextTicketNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);
        var count = await _db.Tickets.CountAsync(x => x.CreatedAt >= from && x.CreatedAt < to, ct);
        return $"TKT-{year}-{(count + 1):D5}";
    }
}
