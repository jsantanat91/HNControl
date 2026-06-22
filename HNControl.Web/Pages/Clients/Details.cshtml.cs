using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HNControl.Web.Services;
using HNControl.Web.Services.Clients;
using System.Security.Claims;

namespace HNControl.Web.Pages.Clients;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly IActionAccessService _actions;
    private readonly IClientPortalAccessService _portalAccess;
    public DetailsModel(
        ApplicationDbContext db,
        IConfiguration cfg,
        IActionAccessService actions,
        IClientPortalAccessService portalAccess)
    {
        _db = db;
        _cfg = cfg;
        _actions = actions;
        _portalAccess = portalAccess;
    }

    public Client? Client { get; set; }
    public bool CanEdit { get; set; }
    public bool CanViewAllClients { get; set; }
    public bool CanViewOwnClients { get; set; }
    public bool IsSuperAdmin { get; set; }
    public string OwnerDisplayName { get; set; } = "-";
    public ClientPortalCredentialResult? PortalCredentials { get; set; }
    public string ClientPortalLoginUrl { get; set; } = "/Portal/Login";
    public List<ClientContactRow> Contacts { get; set; } = new();

    [BindProperty]
    public NewContactInput ContactInput { get; set; } = new();

    public record ContractRow(
        Guid Id,
        string ServiceType,
        string Label,
        string Provider,
        string AccountNumber,
        string ContractNumber,
        string MonthlyAmountText,
        string BillingRecurrence,
        string ContractTermText,
        string SalesReference,
        string Branch,
        string BranchAddress,
        string ContractEndDateText,
        string StatusText,
        string StatusBadgeClass,
        bool HasContractFile,
        string ProjectTitle,
        IReadOnlyList<string> ServiceTypes,
        IReadOnlyList<string> TechnicalSummary
    );

    public List<ContractRow> Contracts { get; set; } = new();

    public record ProjectRow(Guid Id, string Title, string StartDate, string EstEnd, string Status);
    public List<ProjectRow> Projects { get; set; } = new();
    public record QuoteRow(Guid Id, string Folio, string CreatedAt, string Segment, decimal Total, int ManualItems, bool HasPdf);
    public List<QuoteRow> Quotes { get; set; } = new();
    public record TicketRow(Guid Id, string Number, string Title, string Status, string Priority, string CreatedAt, string AssignedTo);
    public List<TicketRow> Tickets { get; set; } = new();
    public record DeliveryRow(Guid Id, string Title, string Project, string DeliveryDate, string Status, string Receiver, bool HasPdf);
    public List<DeliveryRow> Deliveries { get; set; } = new();
    public string PublicQuoteUrl { get; set; } = string.Empty;
    public string PublicTicketUrl { get; set; } = string.Empty;
    public record ClientContactRow(Guid Id, string Name, string Email, string Phone, string Role, bool IsPrimary, DateTime UpdatedAt);

    public class NewContactInput
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsPrimary { get; set; }
    }

    public async Task OnGetAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        IsSuperAdmin = AppRoles.IsGlobalAdmin(User);
        CanViewAllClients = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsView);
        CanViewOwnClients = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsViewOwn);
        CanEdit = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsEdit);
        Client = await _db.Clients
            .Include(c => c.Contracts)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (Client == null) return;
        if (!CanViewAllClients && (!CanViewOwnClients || !string.Equals(Client.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase)))
        {
            Client = null;
            return;
        }

        if (!string.IsNullOrWhiteSpace(Client.OwnerUserId))
        {
            OwnerDisplayName = await _db.EmployeeProfiles
                .AsNoTracking()
                .Where(x => x.UserId == Client.OwnerUserId)
                .Select(x => string.IsNullOrWhiteSpace(x.Email) ? x.FullName : $"{x.FullName} · {x.Email}")
                .FirstOrDefaultAsync() ?? Client.OwnerUserId;
        }

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
        PublicTicketUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? "/ticket-publico"
            : $"{baseUrl}/ticket-publico";
        ClientPortalLoginUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? "/Portal/Login"
            : $"{baseUrl}/Portal/Login";

        if (IsSuperAdmin)
            PortalCredentials = await _portalAccess.EnsureForClientAsync(Client.Id, userId, false);

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

                var endText = end.HasValue ? end.Value.ToString("yyyy-MM-dd") : "-";
                var meta = ParseContractMeta(x.Notes);
                var tech = ClientServiceContractMetadata.ParseTechnical(x.Notes);
                var serviceTypes = tech.ServiceTypes.Any() ? tech.ServiceTypes : [x.ServiceType.ToString()];
                var technicalSummary = BuildTechnicalSummary(tech);

                var projTitle = (x.ProjectId.HasValue && projMap.TryGetValue(x.ProjectId.Value, out var t))
                    ? t
                    : "-";

                return new ContractRow(
                    x.Id,
                    x.ServiceType.ToString(),
                    x.Label,
                    x.Provider,
                    x.AccountNumber,
                    x.ContractNumber,
                    (x.MonthlyAmount ?? 0m).ToString("C2"),
                    meta.Recurrence,
                    meta.TermText,
                    meta.SalesReference,
                    x.Branch,
                    x.BranchAddress,
                    endText,
                    status,
                    badge,
                    !string.IsNullOrWhiteSpace(x.SignedContractStoragePath),
                    projTitle,
                    serviceTypes,
                    technicalSummary
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
        )).Take(200).ToList();

        Quotes = await _db.QuoteRequests
            .AsNoTracking()
            .Where(x => x.ClientId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new QuoteRow(
                x.Id,
                x.Folio,
                x.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                x.Segment == QuoteSegment.Business ? "Empresarial"
                    : x.Segment == QuoteSegment.Events ? "Eventos"
                    : "Residencial",
                x.EstimatedTotal ?? x.SubtotalAuto,
                x.ManualItemsCount,
                !string.IsNullOrWhiteSpace(x.PdfStoragePath)
            ))
            .ToListAsync();

        Tickets = await _db.Tickets
            .AsNoTracking()
            .Where(x => x.ClientId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new TicketRow(
                x.Id,
                x.TicketNumber,
                x.Title,
                x.Status == TicketStatus.New ? "Nuevo" :
                x.Status == TicketStatus.Assigned ? "Asignado" :
                x.Status == TicketStatus.InProgress ? "En proceso" :
                x.Status == TicketStatus.PendingCustomer ? "Pendiente cliente" :
                x.Status == TicketStatus.Resolved ? "Resuelto" :
                x.Status == TicketStatus.Closed ? "Cerrado" :
                x.Status == TicketStatus.Cancelled ? "Cancelado" : "-",
                x.Priority == TicketPriority.Low ? "Baja" :
                x.Priority == TicketPriority.Medium ? "Intermedia" :
                x.Priority == TicketPriority.High ? "Alta" :
                x.Priority == TicketPriority.Critical ? "Urge" : "-",
                x.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                string.IsNullOrWhiteSpace(x.AssignedToName) ? "Sin asignar" : x.AssignedToName
            ))
            .ToListAsync();

        Deliveries = await _db.ProjectDeliveryFormats
            .AsNoTracking()
            .Include(x => x.Project)
            .Where(x => x.ClientId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new DeliveryRow(
                x.Id,
                x.Title,
                x.Project != null ? x.Project.Title : "-",
                x.DeliveryDate.ToString("yyyy-MM-dd"),
                x.Status == ProjectDeliveryFormatStatus.Draft ? "Borrador" :
                x.Status == ProjectDeliveryFormatStatus.SentForSignature ? "En firma" : "Firmado",
                x.ReceiverName,
                !string.IsNullOrWhiteSpace(x.PdfStoragePath)
            ))
            .ToListAsync();

        await LoadContactsAsync(id);
    }

    public async Task<IActionResult> OnPostAddContactAsync(Guid id)
    {
        var canEdit = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsEdit);
        if (!canEdit) return Forbid();
        if (!await CanAccessClientAsync(id)) return Forbid();

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id);
        if (client == null) return NotFound();

        var name = (ContactInput.Name ?? "").Trim();
        var email = (ContactInput.Email ?? "").Trim();
        var phone = (ContactInput.Phone ?? "").Trim();
        var role = (ContactInput.Role ?? "").Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["ClientDetailsInfo"] = "El nombre del contacto es obligatorio.";
            TempData["ClientDetailsInfoType"] = "danger";
            return RedirectToPage(new { id });
        }

        bool exists;
        try
        {
            exists = await _db.ClientContacts.AnyAsync(c =>
                c.ClientId == id
                && c.Name.ToLower() == name.ToLower()
                && c.Email.ToLower() == email.ToLower());
        }
        catch
        {
            TempData["ClientDetailsInfo"] = "Falta aplicar migracion de base de datos para contactos.";
            TempData["ClientDetailsInfoType"] = "danger";
            return RedirectToPage(new { id });
        }

        if (exists)
        {
            TempData["ClientDetailsInfo"] = "Ese contacto ya existe en el cliente.";
            TempData["ClientDetailsInfoType"] = "warning";
            return RedirectToPage(new { id });
        }

        if (ContactInput.IsPrimary)
        {
            var primaries = await _db.ClientContacts.Where(c => c.ClientId == id && c.IsPrimary).ToListAsync();
            foreach (var p in primaries)
                p.IsPrimary = false;
        }

        _db.ClientContacts.Add(new ClientContact
        {
            ClientId = id,
            Name = name,
            Email = email,
            Phone = phone,
            Role = role,
            IsPrimary = ContactInput.IsPrimary,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        TempData["ClientDetailsInfo"] = "Contacto agregado.";
        TempData["ClientDetailsInfoType"] = "success";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteContactAsync(Guid id, Guid contactId)
    {
        var canEdit = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsEdit);
        if (!canEdit) return Forbid();
        if (!await CanAccessClientAsync(id)) return Forbid();

        ClientContact? contact;
        try
        {
            contact = await _db.ClientContacts.FirstOrDefaultAsync(c => c.Id == contactId && c.ClientId == id);
        }
        catch
        {
            TempData["ClientDetailsInfo"] = "Falta aplicar migracion de base de datos para contactos.";
            TempData["ClientDetailsInfoType"] = "danger";
            return RedirectToPage(new { id });
        }
        if (contact == null)
        {
            TempData["ClientDetailsInfo"] = "Contacto no encontrado.";
            TempData["ClientDetailsInfoType"] = "warning";
            return RedirectToPage(new { id });
        }

        _db.ClientContacts.Remove(contact);
        await _db.SaveChangesAsync();
        TempData["ClientDetailsInfo"] = "Contacto eliminado.";
        TempData["ClientDetailsInfoType"] = "success";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteContractAsync(Guid id, Guid contractId)
    {
        var canEdit = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsEdit);
        if (!canEdit) return Forbid();
        if (!await CanAccessClientAsync(id)) return Forbid();

        var contract = await _db.ClientServiceContracts
            .FirstOrDefaultAsync(c => c.Id == contractId && c.ClientId == id);

        if (contract == null)
        {
            TempData["ClientDetailsInfo"] = "Contrato no encontrado.";
            TempData["ClientDetailsInfoType"] = "warning";
            return RedirectToPage(new { id });
        }

        try
        {
            await _db.ClientLegalDocuments
                .Where(x => x.ClientServiceContractId == contractId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.ClientServiceContractId, (Guid?)null));

            await _db.BillingInvoicePlans
                .Where(x => x.ClientServiceContractId == contractId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.ClientServiceContractId, (Guid?)null));

            await _db.KnowledgeLinks
                .Where(x => x.ClientServiceContractId == contractId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.ClientServiceContractId, (Guid?)null));

            await _db.ClientCarrierServices
                .Where(x => x.ClientServiceContractId == contractId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.ClientServiceContractId, (Guid?)null));

            await _db.MonitorTargets
                .Where(x => x.ClientServiceContractId == contractId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.ClientServiceContractId, (Guid?)null));

            await _db.Tickets
                .Where(x => x.ClientServiceContractId == contractId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.ClientServiceContractId, (Guid?)null));

            await _db.ServiceOrders
                .Where(x => x.ClientServiceContractId == contractId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.ClientServiceContractId, (Guid?)null));

            _db.ClientServiceContracts.Remove(contract);
            await _db.SaveChangesAsync();

            TempData["ClientDetailsInfo"] = "Contrato eliminado. Los registros relacionados se conservaron sin liga al contrato.";
            TempData["ClientDetailsInfoType"] = "success";
        }
        catch
        {
            TempData["ClientDetailsInfo"] = "No se pudo eliminar el contrato. Revisa si existe una migracion pendiente o una relacion nueva ligada al contrato.";
            TempData["ClientDetailsInfoType"] = "danger";
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteDeliveryAsync(Guid id, Guid deliveryId)
    {
        var canEdit = AppRoles.IsGlobalAdmin(User) || await _actions.HasActionAsync(User, AppActions.ClientsEdit);
        if (!canEdit) return Forbid();
        if (!await CanAccessClientAsync(id)) return Forbid();

        var delivery = await _db.ProjectDeliveryFormats
            .FirstOrDefaultAsync(x => x.Id == deliveryId && x.ClientId == id);

        if (delivery == null)
        {
            TempData["ClientDetailsInfo"] = "Formato de entrega no encontrado.";
            TempData["ClientDetailsInfoType"] = "warning";
            return RedirectToPage(new { id });
        }

        _db.ProjectDeliveryFormats.Remove(delivery);
        await _db.SaveChangesAsync();

        TempData["ClientDetailsInfo"] = "Formato de entrega eliminado.";
        TempData["ClientDetailsInfoType"] = "success";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostResetPortalAccessAsync(Guid id)
    {
        if (!AppRoles.IsGlobalAdmin(User))
            return Forbid();

        if (!await _db.Clients.AnyAsync(c => c.Id == id))
            return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var access = await _portalAccess.EnsureForClientAsync(id, userId, forceResetPassword: true);
        if (access == null)
        {
            TempData["ClientDetailsInfo"] = "No fue posible regenerar acceso de portal. Verifica ClientCode.";
            TempData["ClientDetailsInfoType"] = "danger";
            return RedirectToPage(new { id });
        }

        TempData["ClientDetailsInfo"] = "Acceso de portal regenerado.";
        TempData["ClientDetailsInfoType"] = "success";
        return RedirectToPage(new { id });
    }

    private async Task LoadContactsAsync(Guid clientId)
    {
        try
        {
            Contacts = await _db.ClientContacts
                .AsNoTracking()
                .Where(x => x.ClientId == clientId)
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.Name)
                .Select(x => new ClientContactRow(
                    x.Id,
                    x.Name,
                    x.Email,
                    x.Phone,
                    x.Role,
                    x.IsPrimary,
                    x.UpdatedAt))
                .ToListAsync();
        }
        catch
        {
            Contacts = new();
        }
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

    private static (string Recurrence, string TermText, string SalesReference) ParseContractMeta(string? notes)
    {
        var recurrence = "Mensual";
        var term = "12";
        var sale = "-";
        foreach (var line in (notes ?? string.Empty).Split('\n'))
        {
            var clean = line.Trim().TrimEnd('\r');
            if (!clean.StartsWith("[META]", StringComparison.OrdinalIgnoreCase))
                continue;
            var payload = clean.Substring(6).Trim();
            var parts = payload.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;
            var key = parts[0];
            var value = parts[1];
            if (key.Equals("Recurrencia", StringComparison.OrdinalIgnoreCase))
                recurrence = value;
            else if (key.Equals("Plazo", StringComparison.OrdinalIgnoreCase))
                term = value;
            else if (key.Equals("VentaId", StringComparison.OrdinalIgnoreCase))
                sale = value;
        }

        var termText = term switch
        {
            "12" => "12 meses",
            "18" => "18 meses",
            "24" => "24 meses",
            "36" => "36 meses",
            _ => "Indefinido"
        };

        return (recurrence, termText, sale);
    }

    private static IReadOnlyList<string> BuildTechnicalSummary(ClientServiceTechnicalMetadata tech)
    {
        var rows = new List<string>();
        if (tech.ServiceTypes.Contains("Internet", StringComparer.OrdinalIgnoreCase))
        {
            var capacity = string.Equals(tech.InternetCapacity, "Otro", StringComparison.OrdinalIgnoreCase)
                ? tech.InternetCapacityOther
                : tech.InternetCapacity;
            if (!string.IsNullOrWhiteSpace(capacity))
                rows.Add(string.Equals(tech.InternetCapacity, "Otro", StringComparison.OrdinalIgnoreCase)
                    ? $"Internet: {capacity}"
                    : $"Internet: {capacity} MB");
        }

        if (tech.ServiceTypes.Contains("Telefonia", StringComparer.OrdinalIgnoreCase))
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(tech.TelephonyExtensions)) parts.Add($"{tech.TelephonyExtensions} extensiones");
            if (!string.IsNullOrWhiteSpace(tech.TelephonyTrunks)) parts.Add($"{tech.TelephonyTrunks} troncales");
            if (!string.IsNullOrWhiteSpace(tech.TelephonyDids)) parts.Add($"{tech.TelephonyDids} DID");
            if (parts.Any()) rows.Add($"Telefonia: {string.Join(", ", parts)}");
        }

        if (tech.ServiceTypes.Contains("CCTV", StringComparer.OrdinalIgnoreCase))
        {
            var channels = string.Equals(tech.CctvChannels, "Otro", StringComparison.OrdinalIgnoreCase)
                ? tech.CctvChannelsOther
                : tech.CctvChannels;
            if (!string.IsNullOrWhiteSpace(channels))
                rows.Add(string.Equals(tech.CctvChannels, "Otro", StringComparison.OrdinalIgnoreCase)
                    ? $"CCTV: {channels}"
                    : $"CCTV: {channels} canales");
        }

        if (tech.ServiceTypes.Contains("Seguridad", StringComparer.OrdinalIgnoreCase))
        {
            var brand = string.Equals(tech.SecurityBrand, "Otro", StringComparison.OrdinalIgnoreCase)
                ? tech.SecurityBrandOther
                : tech.SecurityBrand;
            if (!string.IsNullOrWhiteSpace(brand))
                rows.Add($"Seguridad: {brand}");
        }

        if (tech.ServiceTypes.Contains("Servidores", StringComparer.OrdinalIgnoreCase))
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(tech.ServerOs)) parts.Add(tech.ServerOs);
            if (!string.IsNullOrWhiteSpace(tech.ServerCpuCores)) parts.Add($"{tech.ServerCpuCores} nucleos");
            if (!string.IsNullOrWhiteSpace(tech.ServerRam)) parts.Add($"{tech.ServerRam} RAM");
            if (!string.IsNullOrWhiteSpace(tech.ServerDisk)) parts.Add($"{tech.ServerDisk} disco");
            if (parts.Any()) rows.Add($"Servidor: {string.Join(", ", parts)}");
        }

        if (tech.InstallationCost > 0)
            rows.Add($"Instalacion: {tech.InstallationCost.ToString("C2")}");

        return rows;
    }

    private async Task<bool> CanAccessClientAsync(Guid clientId)
    {
        if (AppRoles.IsGlobalAdmin(User))
            return true;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var canViewAll = await _actions.HasActionAsync(User, AppActions.ClientsView);
        if (canViewAll) return true;

        var canViewOwn = await _actions.HasActionAsync(User, AppActions.ClientsViewOwn);
        if (!canViewOwn) return false;

        return await _db.Clients
            .AsNoTracking()
            .AnyAsync(x => x.Id == clientId && x.OwnerUserId == userId);
    }
}


