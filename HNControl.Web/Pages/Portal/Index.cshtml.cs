using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using HNControl.Web.Services.Clients;
using HNControl.Web.Services.Tickets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Portal;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ITicketFlowService _tickets;
    private readonly IMercadoPagoService _mercadoPago;
    private readonly IFileStorage _storage;
    private readonly IClientPortalAccessService _portalAccess;

    public IndexModel(
        ApplicationDbContext db,
        ITicketFlowService tickets,
        IMercadoPagoService mercadoPago,
        IFileStorage storage,
        IClientPortalAccessService portalAccess)
    {
        _db = db;
        _tickets = tickets;
        _mercadoPago = mercadoPago;
        _storage = storage;
        _portalAccess = portalAccess;
    }

    public Client? Client { get; set; }
    public string ClientCode { get; set; } = "";
    public decimal TotalPendiente { get; set; }
    public decimal TotalPagado { get; set; }
    public List<AccountRunVm> AccountRuns { get; set; } = new();
    public List<ContractVm> ActiveContracts { get; set; } = new();
    public List<ContactVm> Contacts { get; set; } = new();
    public List<DomiciliationVm> Domiciliations { get; set; } = new();
    public string PayerEmailHint { get; set; } = "";
    public string PublicQuoteUrl { get; set; } = "";

    [BindProperty]
    public TicketInput TicketForm { get; set; } = new();

    [BindProperty]
    public DomiciliationInput DomiciliationForm { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public ChangePasswordInput PasswordForm { get; set; } = new();

    public record AccountRunVm(Guid Id, string Concepto, string Periodo, string Fecha, decimal Total, bool Pagada, string Estado, bool HasPdf);
    public record ContractVm(Guid Id, string Tipo, string Nombre, string Sucursal, string Vigencia, bool HasFile);
    public record ContactVm(Guid Id, string Nombre, string Correo, string Telefono, bool Principal);
    public record DomiciliationVm(Guid Id, string Fecha, string Estado, string UrlPago);

    public class TicketInput
    {
        [Required, MaxLength(220)]
        public string Titulo { get; set; } = "";

        [Required, MaxLength(3000)]
        public string Descripcion { get; set; } = "";

        [MaxLength(180)]
        public string ContactoNombre { get; set; } = "";

        [MaxLength(256)]
        [EmailAddress]
        public string ContactoCorreo { get; set; } = "";

        [MaxLength(60)]
        public string ContactoTelefono { get; set; } = "";

        public Guid? ContractId { get; set; }
        public Guid? ClientContactId { get; set; }
    }

    public class DomiciliationInput
    {
        [Range(1, 999999999)]
        public decimal MontoReferencia { get; set; } = 1m;

        [MaxLength(256)]
        [EmailAddress]
        public string CorreoPagador { get; set; } = "";
    }

    public class ChangePasswordInput
    {
        public string CurrentPassword { get; set; } = "";

        public string NewPassword { get; set; } = "";

        public string ConfirmPassword { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var auth = await EnsureClientAuthAsync();
        if (!auth.ok) return auth.redirect!;

        await LoadPageDataAsync(auth.clientId!.Value);

        // Precargamos el monto a pagar con el saldo pendiente (si hay).
        if (TotalPendiente > 0)
            DomiciliationForm.MontoReferencia = TotalPendiente;

        // Sugerimos el correo del cliente como pagador (editable por el usuario).
        if (string.IsNullOrWhiteSpace(DomiciliationForm.CorreoPagador))
            DomiciliationForm.CorreoPagador = PayerEmailHint;

        return Page();
    }

    public async Task<IActionResult> OnPostCreateTicketAsync()
    {
        var auth = await EnsureClientAuthAsync();
        if (!auth.ok) return auth.redirect!;
        if (!ModelState.IsValid)
        {
            await LoadPageDataAsync(auth.clientId!.Value);
            TempData["PortalInfo"] = "Completa los datos obligatorios para abrir ticket.";
            TempData["PortalInfoType"] = "danger";
            return Page();
        }

        var defaultContact = await _db.ClientContacts
            .AsNoTracking()
            .Where(x => x.ClientId == auth.clientId.Value && x.IsPrimary)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync();

        var name = string.IsNullOrWhiteSpace(TicketForm.ContactoNombre)
            ? (defaultContact?.Name ?? Client?.ContactName ?? Client?.Name ?? "")
            : TicketForm.ContactoNombre.Trim();
        var email = string.IsNullOrWhiteSpace(TicketForm.ContactoCorreo)
            ? (defaultContact?.Email ?? Client?.Email ?? "")
            : TicketForm.ContactoCorreo.Trim();
        var phone = string.IsNullOrWhiteSpace(TicketForm.ContactoTelefono)
            ? (defaultContact?.Phone ?? Client?.Phone ?? "")
            : TicketForm.ContactoTelefono.Trim();

        try
        {
            await _tickets.CreatePublicAsync(
                ClientCode,
                TicketForm.ContractId,
                name,
                email,
                phone,
                Client?.Address ?? "",
                TicketForm.Titulo.Trim(),
                TicketForm.Descripcion.Trim(),
                clientContactId: TicketForm.ClientContactId,
                autoCreateClientContact: true);

            TempData["PortalInfo"] = "Ticket creado correctamente.";
            TempData["PortalInfoType"] = "success";
        }
        catch (Exception ex)
        {
            TempData["PortalInfo"] = $"No fue posible crear el ticket: {ex.Message}";
            TempData["PortalInfoType"] = "danger";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateDomiciliationAsync()
    {
        var auth = await EnsureClientAuthAsync();
        if (!auth.ok) return auth.redirect!;

        if (DomiciliationForm.MontoReferencia <= 0)
            DomiciliationForm.MontoReferencia = 1m;

        // El pagador puede indicar su propio correo (distinto al del negocio). Si lo deja
        // vacio, usamos el mejor correo de contacto del cliente.
        var payerEmail = !string.IsNullOrWhiteSpace(DomiciliationForm.CorreoPagador)
            ? DomiciliationForm.CorreoPagador.Trim()
            : await ResolveBestPayerEmailAsync(auth.clientId.Value);

        if (string.IsNullOrWhiteSpace(payerEmail))
        {
            TempData["PortalInfo"] = "Define un correo pagador para crear el enlace de domiciliación.";
            TempData["PortalInfoType"] = "warning";
            return RedirectToPage();
        }

        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == auth.clientId.Value);
        if (client == null)
            return RedirectToPage("/Portal/Login");

        MercadoPagoCheckoutResult mp;
        try
        {
            mp = await _mercadoPago.CreateCardDomiciliationCheckoutAsync(
                client.ClientCode,
                client.Name,
                payerEmail,
                DomiciliationForm.MontoReferencia,
                "Pago de servicios");
        }
        catch (Exception ex)
        {
            TempData["PortalInfo"] = $"No fue posible iniciar Mercado Pago: {ex.Message}";
            TempData["PortalInfoType"] = "danger";
            return RedirectToPage();
        }

        if (!mp.Success)
        {
            TempData["PortalInfo"] = mp.Message;
            TempData["PortalInfoType"] = "danger";
            return RedirectToPage();
        }

        _db.ClientCardDomiciliations.Add(new ClientCardDomiciliation
        {
            ClientId = client.Id,
            MercadoPagoPreferenceId = mp.PreferenceId,
            MercadoPagoExternalReference = mp.ExternalReference,
            InitPointUrl = mp.Url,
            ReferenceAmount = DomiciliationForm.MontoReferencia,
            Status = ClientCardDomiciliationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return Redirect(mp.Url);
    }

    public async Task<IActionResult> OnPostDeletePaymentAsync(Guid id)
    {
        var auth = await EnsureClientAuthAsync();
        if (!auth.ok) return auth.redirect!;

        var row = await _db.ClientCardDomiciliations
            .FirstOrDefaultAsync(x => x.Id == id && x.ClientId == auth.clientId!.Value);

        if (row != null)
        {
            _db.ClientCardDomiciliations.Remove(row);
            await _db.SaveChangesAsync();
            TempData["PortalInfo"] = "Intento de pago eliminado.";
            TempData["PortalInfoType"] = "success";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetDownloadContractAsync(Guid id)
    {
        var auth = await EnsureClientAuthAsync();
        if (!auth.ok) return auth.redirect!;

        var contract = await _db.ClientServiceContracts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ClientId == auth.clientId.Value);
        if (contract == null || string.IsNullOrWhiteSpace(contract.SignedContractStoragePath))
            return NotFound();

        var ext = Path.GetExtension(contract.SignedContractOriginalFileName ?? "contrato.pdf");
        if (string.IsNullOrWhiteSpace(ext)) ext = ".pdf";
        var name = $"Contrato_{(contract.Label ?? "cliente").Replace(' ', '_')}{ext}";
        var (stream, contentType, downloadName) = await _storage.OpenAsync(contract.SignedContractStoragePath, name);
        return File(stream, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType, downloadName);
    }

    public async Task<IActionResult> OnGetDownloadInvoiceAsync(Guid id)
    {
        var auth = await EnsureClientAuthAsync();
        if (!auth.ok) return auth.redirect!;

        var run = await _db.BillingInvoiceRuns
            .AsNoTracking()
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.Plan != null &&
                x.Plan.ClientId == auth.clientId!.Value);
        if (run == null || string.IsNullOrWhiteSpace(run.PdfStoragePath))
            return NotFound();

        var downloadName = $"factura_{run.ScheduledFor:yyyyMMdd}.pdf";
        var (stream, contentType, name) = await _storage.OpenAsync(run.PdfStoragePath, downloadName);
        return File(stream, string.IsNullOrWhiteSpace(contentType) ? "application/pdf" : contentType, name);
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync("ClientPortal");
        return RedirectToPage("/Portal/Login");
    }

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        var auth = await EnsureClientAuthAsync();
        if (!auth.ok) return auth.redirect!;

        var current = (PasswordForm.CurrentPassword ?? "").Trim();
        var next = (PasswordForm.NewPassword ?? "").Trim();
        var confirm = (PasswordForm.ConfirmPassword ?? "").Trim();
        if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(next) || next.Length < 10 || !string.Equals(next, confirm, StringComparison.Ordinal))
        {
            TempData["PortalInfo"] = "Verifica la contraseña actual y la nueva contraseña.";
            TempData["PortalInfoType"] = "danger";
            return RedirectToPage();
        }

        var accessIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(accessIdRaw, out var accessId))
        {
            TempData["PortalInfo"] = "No fue posible validar tu sesión de portal.";
            TempData["PortalInfoType"] = "danger";
            return RedirectToPage("/Portal/Login");
        }

        var changed = await _portalAccess.ChangePasswordAsync(
            accessId,
            current,
            next,
            updatedByUserId: accessId.ToString());

        TempData["PortalInfo"] = changed
            ? "Contraseña actualizada correctamente."
            : "No fue posible actualizar la contraseña. Verifica tu contraseña actual.";
        TempData["PortalInfoType"] = changed ? "success" : "danger";
        return RedirectToPage();
    }

    private async Task LoadPageDataAsync(Guid clientId)
    {
        Client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
        if (Client == null) return;
        ClientCode = Client.ClientCode ?? "";
        if (string.IsNullOrWhiteSpace(Client.PublicQuoteToken))
        {
            Client.PublicQuoteToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
            await _db.SaveChangesAsync();
        }
        PublicQuoteUrl = $"{Request.Scheme}://{Request.Host}/cotizar/{Client.PublicQuoteToken}";

        TicketForm.ContactoNombre = Client.ContactName ?? "";
        TicketForm.ContactoCorreo = Client.Email ?? "";
        TicketForm.ContactoTelefono = Client.Phone ?? "";

        var runs = await _db.BillingInvoiceRuns
            .AsNoTracking()
            .Include(r => r.Plan)
            .Where(r => r.Plan != null && r.Plan.ClientId == clientId)
            .OrderByDescending(r => r.ScheduledFor)
            .Take(60)
            .ToListAsync();

        AccountRuns = runs
            .Select(r => new AccountRunVm(
                r.Id,
                r.Plan?.Concept ?? "Factura recurrente",
                r.PeriodLabel,
                r.ScheduledFor.ToString("yyyy-MM-dd"),
                r.Plan?.Total ?? 0m,
                r.IsPaid,
                r.IsPaid ? "Pagada" : "Pendiente",
                !string.IsNullOrWhiteSpace(r.PdfStoragePath)))
            .ToList();

        TotalPendiente = AccountRuns.Where(x => !x.Pagada).Sum(x => x.Total);
        TotalPagado = AccountRuns.Where(x => x.Pagada).Sum(x => x.Total);

        var nowDate = DateTime.Today;
        ActiveContracts = await _db.ClientServiceContracts
            .AsNoTracking()
            .Where(c => c.ClientId == clientId
                && (!c.ContractEndDate.HasValue || c.ContractEndDate.Value.Date >= nowDate))
            .OrderBy(c => c.ServiceType)
            .ThenBy(c => c.Label)
            .Take(80)
            .Select(c => new ContractVm(
                c.Id,
                c.ServiceType.ToString(),
                c.Label,
                string.IsNullOrWhiteSpace(c.Branch) ? "-" : c.Branch,
                c.ContractEndDate.HasValue ? c.ContractEndDate.Value.ToString("yyyy-MM-dd") : "Sin vencimiento",
                !string.IsNullOrWhiteSpace(c.SignedContractStoragePath)))
            .ToListAsync();

        Contacts = await _db.ClientContacts
            .AsNoTracking()
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.Name)
            .Take(100)
            .Select(c => new ContactVm(
                c.Id,
                c.Name,
                c.Email,
                c.Phone,
                c.IsPrimary))
            .ToListAsync();

        PayerEmailHint = await ResolveBestPayerEmailAsync(clientId);

        Domiciliations = await _db.ClientCardDomiciliations
            .AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(x => new DomiciliationVm(
                x.Id,
                x.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                x.Status.ToString(),
                x.InitPointUrl))
            .ToListAsync();
    }

    private async Task<(bool ok, Guid? clientId, IActionResult? redirect)> EnsureClientAuthAsync()
    {
        var auth = await HttpContext.AuthenticateAsync("ClientPortal");
        if (!auth.Succeeded || auth.Principal?.Identity?.IsAuthenticated != true)
            return (false, null, RedirectToPage("/Portal/Login"));

        var rawClientId = auth.Principal.FindFirstValue("ClientId");
        if (!Guid.TryParse(rawClientId, out var clientId))
            return (false, null, RedirectToPage("/Portal/Login"));

        return (true, clientId, null);
    }

    private async Task<string> ResolveBestPayerEmailAsync(Guid clientId)
    {
        var primaryContactEmail = (await _db.ClientContacts
            .AsNoTracking()
            .Where(c => c.ClientId == clientId && c.IsPrimary)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => c.Email)
            .FirstOrDefaultAsync()) ?? "";
        if (!string.IsNullOrWhiteSpace(primaryContactEmail))
            return primaryContactEmail.Trim();

        var anyContactEmail = (await _db.ClientContacts
            .AsNoTracking()
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => c.Email)
            .FirstOrDefaultAsync()) ?? "";
        if (!string.IsNullOrWhiteSpace(anyContactEmail))
            return anyContactEmail.Trim();

        var clientEmail = await _db.Clients
            .AsNoTracking()
            .Where(c => c.Id == clientId)
            .Select(c => new { c.BillingEmail, c.Email })
            .FirstOrDefaultAsync();
        if (clientEmail == null) return "";

        if (!string.IsNullOrWhiteSpace(clientEmail.BillingEmail))
            return clientEmail.BillingEmail.Trim();

        return (clientEmail.Email ?? "").Trim();
    }
}

