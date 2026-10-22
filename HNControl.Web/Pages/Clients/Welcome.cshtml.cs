using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients;

[Authorize]
public class WelcomeModel(ApplicationDbContext db, IActionAccessService actions, IEmailSender email,
    IConfiguration configuration, ILogger<WelcomeModel> logger, DocumentNotificationRecipients recipients) : PageModel
{
    public Client? Client { get; private set; }
    public string Recipient { get; private set; } = "";
    public string HtmlBody { get; private set; } = "";
    public string? Error { get; private set; }
    public IReadOnlyCollection<string> InternalCopies { get; private set; } = Array.Empty<string>();
    [BindProperty(SupportsGet = true)] public Guid? ContractId { get; set; }
    public List<ContractOption> ContractOptions { get; private set; } = new();
    public string SelectedContractLabel { get; private set; } = "";
    public bool CanSend => Error == null && ContractId.HasValue && documents.Count == 1 && HtmlBody.Length > 0;
    public record ContractOption(Guid Id, string Label);
    private List<ClientLegalDocument> documents = new();

    public async Task<IActionResult> OnGetAsync(Guid clientId)
    {
        var result = await PrepareAsync(clientId);
        return result ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid clientId)
    {
        var result = await PrepareAsync(clientId, sending: true);
        if (result != null) return result;
        if (!CanSend) return Page();
        foreach (var doc in documents.Where(x => !x.SignedAt.HasValue))
        {
            doc.Status = ClientLegalDocumentStatus.SentForSignature;
            doc.TokenExpiresAt = DateTime.UtcNow.AddMonths(2);
            doc.UpdatedAt = DateTime.UtcNow;
        }
        // Persist the usable links before attempting delivery, so a delivered message never has an inactive link.
        await db.SaveChangesAsync();
        try
        {
            await email.SendAsync(Recipient, ClientCommunicationTemplates.WelcomeSubject, HtmlBody,
                ccEmails: InternalCopies);
            foreach (var doc in documents) doc.SentAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo enviar bienvenida al cliente {ClientId}", clientId);
            Error = "No se pudo confirmar el envío del correo. Revise la configuración SMTP y los correos recibidos antes de reintentar.";
            return Page();
        }
        TempData["ClientDetailsInfo"] = $"Correo de bienvenida de {SelectedContractLabel} enviado a {Recipient}. Copias internas: {InternalCopies.Count}.";
        TempData["ClientDetailsInfoType"] = "success";
        return RedirectToPage("Details", new { id = clientId });
    }

    private async Task<IActionResult?> PrepareAsync(Guid clientId, bool sending = false)
    {
        Error = null;
        HtmlBody = "";
        documents.Clear();
        SelectedContractLabel = "";
        var admin = AppRoles.IsGlobalAdmin(User);
        if (!admin && !await actions.HasActionAsync(User, AppActions.ClientsEdit)) return Forbid();
        Client = await db.Clients.FirstOrDefaultAsync(x => x.Id == clientId);
        if (Client == null) return NotFound();
        if (!admin && !await actions.HasActionAsync(User, AppActions.ClientsView)
            && !(await actions.HasActionAsync(User, AppActions.ClientsViewOwn)
                && Client.OwnerUserId == User.FindFirstValue(ClaimTypes.NameIdentifier))) return Forbid();

        Recipient = ClientCommunicationTemplates.Recipient(Client);
        InternalCopies = await recipients.GetAsync();
        var contracts = await db.ClientServiceContracts.AsNoTracking()
            .Where(x => x.ClientId == clientId).OrderBy(x => x.ContractNumber).ThenBy(x => x.Label).ToListAsync();
        ContractOptions = contracts.Select(x => new ContractOption(x.Id, string.Join(" · ",
            new[] { x.ContractNumber, x.Label, string.IsNullOrWhiteSpace(x.Branch) ? null : $"Sucursal: {x.Branch}" }
                .Where(value => !string.IsNullOrWhiteSpace(value))))).ToList();
        var availableDocuments = db.ClientLegalDocuments.Include(x => x.ClientServiceContract)
            .Where(x => x.ClientId == clientId && x.DocumentType == ClientLegalDocumentType.Contract);
        // Keep older general contracts selectable, but never mix them into a service's welcome.
        if (await availableDocuments.AnyAsync(x => x.ClientServiceContractId == null))
            ContractOptions.Add(new ContractOption(Guid.Empty, "Contrato general (sin servicio asociado)"));
        if (!ModelState.IsValid)
        {
            Error = "Seleccione un contrato válido antes de enviar la bienvenida.";
            return null;
        }
        if (!ContractId.HasValue && ContractOptions.Count == 1) ContractId = ContractOptions[0].Id;
        if (!ContractId.HasValue)
        {
            if (ContractOptions.Count == 0) Error = "Agregue un contrato y genere su PDF en Contrato / NDA antes de enviar la bienvenida.";
            else if (sending) Error = "Seleccione el contrato al que corresponde esta bienvenida.";
            return null;
        }
        var selected = ContractOptions.SingleOrDefault(x => x.Id == ContractId.Value);
        if (selected == null)
        {
            Error = "El contrato seleccionado no pertenece a este cliente o ya no está disponible.";
            return null;
        }
        SelectedContractLabel = selected.Label;
        var serviceContractId = ContractId == Guid.Empty ? (Guid?)null : ContractId;
        var document = await availableDocuments.Where(x => x.ClientServiceContractId == serviceContractId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync();
        if (document != null) documents.Add(document);
        if (!ClientCommunicationTemplates.IsEmail(Recipient)) Error = "Registre un correo válido del cliente antes de enviar la bienvenida.";
        else if (string.IsNullOrWhiteSpace(Client.ClientCode)) Error = "Asigne el número de cliente desde su ficha antes de enviar la bienvenida.";
        else if (documents.Count == 0 || documents.Any(x => string.IsNullOrWhiteSpace(x.PublicToken) || string.IsNullOrWhiteSpace(x.PdfStoragePath)))
            Error = "Genere el contrato y su PDF en Contrato / NDA para el servicio seleccionado antes de enviar la bienvenida.";
        if (Error != null) return null;

        var baseUrl = configuration["PublicLinks:BaseUrl"]?.Trim().TrimEnd('/');
        var links = documents.Select(doc => (doc, string.IsNullOrWhiteSpace(baseUrl)
            ? Url.Page("/Public/ClientDocument", null, new { token = doc.PublicToken }, Request.Scheme) ?? ""
            : $"{baseUrl}/Public/ClientDocument/{doc.PublicToken}"));
        HtmlBody = ClientCommunicationTemplates.Welcome(Client, links);
        return null;
    }
}
