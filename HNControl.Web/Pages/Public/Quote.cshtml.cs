using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HNControl.Web.Pages.Public;

public class QuoteModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IQuoteRequestPdfRenderer _pdf;
    private readonly IFileStorage _storage;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;
    private readonly IActionAccessService _actions;

    public QuoteModel(ApplicationDbContext db, IQuoteRequestPdfRenderer pdf, IFileStorage storage, IEmailSender email, IConfiguration cfg, IActionAccessService actions)
    {
        _db = db;
        _pdf = pdf;
        _storage = storage;
        _email = email;
        _cfg = cfg;
        _actions = actions;
    }

    [BindProperty]
    public QuoteInput Input { get; set; } = new();
    [BindProperty(SupportsGet = true, Name = "editId")]
    public Guid? EditId { get; set; }

    public object CatalogPayload { get; set; } = new { };

    public string? ErrorMessage { get; set; }
    public bool HasClientContext { get; set; }
    public bool SkipClientDataStep { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public List<ProspectOptionVm> ProspectOptions { get; set; } = [];
    public Dictionary<Guid, List<ContactOptionVm>> ClientContactsPayload { get; set; } = new();
    public bool IsEditMode { get; set; }
    public string EditFolio { get; set; } = string.Empty;
    public string InitialManualLinesJson { get; set; } = "[]";

    public record ProspectOptionVm(
        Guid Id,
        string Name,
        string Email,
        string Phone,
        string Location,
        string? CompanyName,
        bool IsTemporaryLead,
        string? MainContactName,
        string? MainContactEmail,
        string? MainContactPhone);
    public record ContactOptionVm(Guid Id, string Name, string Email, string Phone, string? Role);

    public async Task<IActionResult> OnGetAsync(string? token, Guid? editId)
    {
        if (!await CanUseInternalQuoteAsync(requireCreate: false))
            return RedirectToPage("/Account/Login");

        EditId = editId;
        Input.ClientToken = token;
        await TryLoadClientContextAsync(token);
        if (EditId.HasValue && EditId.Value != Guid.Empty)
            await TryLoadEditContextAsync(EditId.Value);
        await LoadCatalogPayloadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostPreviewAsync()
    {
        if (!await CanUseInternalQuoteAsync(requireCreate: true))
            return RedirectToPage("/Account/Login");

        var build = await BuildRequestFromInputAsync(isPreview: true);
        if (build.error != null || build.request == null)
        {
            ErrorMessage = build.error ?? "No se pudo generar el preview.";
            return Page();
        }

        var pdfBytes = await _pdf.RenderAsync(build.request);
        Response.Headers.ContentDisposition = $"inline; filename=\"{build.request.Folio}.pdf\"";
        return File(pdfBytes, "application/pdf");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await CanUseInternalQuoteAsync(requireCreate: true))
            return RedirectToPage("/Account/Login");

        var build = await BuildRequestFromInputAsync(isPreview: false);
        if (build.error != null || build.request == null)
        {
            ErrorMessage = build.error ?? "No se pudo generar la cotizacion.";
            return Page();
        }

        var request = build.request;

        if (!request.ClientId.HasValue)
        {
            var lead = await GetOrCreateTemporaryLeadAsync(request.CustomerName, request.CustomerEmail, request.CustomerPhone, request.CustomerLocation, request.CompanyName);
            request.ClientId = lead.Id;
        }

        string? oldPdfPath = null;
        var isEditing = Input.EditQuoteId.HasValue && Input.EditQuoteId.Value != Guid.Empty;
        if (isEditing)
        {
            var existing = await ResolveEditableQuoteAsync(Input.EditQuoteId!.Value);
            if (existing == null)
            {
                ErrorMessage = "La cotizacion ya no esta disponible para editar.";
                await LoadCatalogPayloadAsync();
                return Page();
            }

            oldPdfPath = existing.PdfStoragePath;
            existing.ClientId = request.ClientId;
            existing.Segment = request.Segment;
            existing.Status = QuoteRequestStatus.New;
            existing.CustomerName = request.CustomerName;
            existing.CustomerEmail = request.CustomerEmail;
            existing.CustomerPhone = request.CustomerPhone;
            existing.CustomerLocation = request.CustomerLocation;
            existing.CompanyName = request.CompanyName;
            existing.Notes = request.Notes;
            existing.GeneralTerms = request.GeneralTerms;
            existing.ContractTermMonths = request.ContractTermMonths;
            existing.SubtotalAuto = request.SubtotalAuto;
            existing.SubtotalBeforeVat = request.SubtotalBeforeVat;
            existing.VatAmount = request.VatAmount;
            existing.ManualItemsCount = request.ManualItemsCount;
            existing.EstimatedTotal = request.EstimatedTotal;
            existing.AcceptedAt = null;
            existing.AcceptedByUserId = null;
            existing.PdfStoragePath = null;

            _db.QuoteRequestLines.RemoveRange(existing.Lines);
            existing.Lines = request.Lines;
            foreach (var ln in existing.Lines)
                ln.QuoteRequestId = existing.Id;

            await _db.SaveChangesAsync();
            request = existing;
        }
        else
        {
            _db.QuoteRequests.Add(request);
            await _db.SaveChangesAsync();
            await EnsureOpportunityForCurrentUserAsync(request);
        }

        var pdfBytes = await _pdf.RenderAsync(request);
        var fileName = $"{request.Folio}.pdf";
        var save = await _storage.SaveBytesAsync(pdfBytes, "quotes", fileName, "application/pdf");
        request.PdfStoragePath = save.storagePath;
        if (!string.IsNullOrWhiteSpace(oldPdfPath))
            await _storage.DeleteIfExistsAsync(oldPdfPath);

        var subject = $"Cotizacion {request.Folio} - HN Control";
        var bodyCustomer = BuildEmailBody(request, true);
        var bodyInternal = BuildEmailBody(request, false);

        try
        {
            await _email.SendAsync(request.CustomerEmail, subject, bodyCustomer, pdfBytes, fileName, "application/pdf");

            var copy = (_cfg["Quotes:InternalCopyEmail"] ?? _cfg["SeedAdmin:Email"] ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(copy))
                await _email.SendAsync(copy, $"[Copia interna] {subject}", bodyInternal, pdfBytes, fileName, "application/pdf");

            request.Status = QuoteRequestStatus.Emailed;
            await _db.SaveChangesAsync();

            return RedirectToPage("/Public/QuoteSuccess", new { id = request.Id, folio = request.Folio, sent = 1 });
        }
        catch
        {
            request.Status = QuoteRequestStatus.EmailError;
            await _db.SaveChangesAsync();
            return RedirectToPage("/Public/QuoteSuccess", new { id = request.Id, folio = request.Folio, sent = 0 });
        }
    }

    private async Task<(QuoteRequest? request, string? error)> BuildRequestFromInputAsync(bool isPreview)
    {
        var boundClient = await TryLoadClientContextAsync(Input.ClientToken);
        if (boundClient != null)
        {
            // Si el cliente del link tiene datos incompletos, se permite que el usuario los capture.
            Input.CustomerName = string.IsNullOrWhiteSpace(Input.CustomerName)
                ? (string.IsNullOrWhiteSpace(boundClient.ContactName) ? boundClient.Name : boundClient.ContactName!)
                : Input.CustomerName;
            Input.CustomerEmail = string.IsNullOrWhiteSpace(Input.CustomerEmail) ? (boundClient.Email ?? string.Empty) : Input.CustomerEmail;
            Input.CustomerPhone = string.IsNullOrWhiteSpace(Input.CustomerPhone) ? (boundClient.Phone ?? string.Empty) : Input.CustomerPhone;
            Input.CustomerLocation = string.IsNullOrWhiteSpace(Input.CustomerLocation) ? (boundClient.Address ?? string.Empty) : Input.CustomerLocation;
            Input.CompanyName = string.IsNullOrWhiteSpace(Input.CompanyName) ? boundClient.Name : Input.CompanyName;
        }

        await LoadCatalogPayloadAsync();

        if (string.IsNullOrWhiteSpace(Input.CustomerName)
            || string.IsNullOrWhiteSpace(Input.CustomerEmail)
            || string.IsNullOrWhiteSpace(Input.CustomerPhone)
            || string.IsNullOrWhiteSpace(Input.CustomerLocation))
        {
            return (null, "Completa nombre, correo, telefono y ubicacion.");
        }

        List<LinePickVm>? picks;
        try
        {
            picks = JsonSerializer.Deserialize<List<LinePickVm>>(Input.LinesJson ?? "[]", new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return (null, "No se pudieron leer los conceptos seleccionados.");
        }

        if (picks == null || picks.Count == 0)
            picks = [];

        var activeItems = await _db.QuoteCatalogItems
            .AsNoTracking()
            .Where(x => x.IsActive && x.Segment == Input.Segment)
            .ToListAsync();

        var byId = activeItems.ToDictionary(x => x.Id, x => x);

        var request = new QuoteRequest
        {
            Folio = isPreview ? $"PREV-{DateTime.UtcNow:yyyyMMddHHmmss}" : await NextFolioAsync(),
            Segment = Input.Segment,
            Status = QuoteRequestStatus.New,
            CustomerName = Input.CustomerName.Trim(),
            CustomerEmail = Input.CustomerEmail.Trim(),
            CustomerPhone = Input.CustomerPhone.Trim(),
            CustomerLocation = Input.CustomerLocation.Trim(),
            CompanyName = string.IsNullOrWhiteSpace(Input.CompanyName) ? null : Input.CompanyName.Trim(),
            Notes = string.IsNullOrWhiteSpace(Input.Notes) ? null : Input.Notes.Trim(),
            GeneralTerms = string.IsNullOrWhiteSpace(Input.GeneralTerms) ? null : Input.GeneralTerms.Trim(),
            ContractTermMonths = Input.ContractTermMonths is 12 or 18 or 24 or 36 ? Input.ContractTermMonths : null,
            CreatedAt = DateTime.UtcNow
        };

        if (boundClient != null)
            request.ClientId = boundClient.Id;

        if (!request.ClientId.HasValue && Input.SelectedClientId.HasValue)
        {
            var selectedClient = await ResolveSelectableClientAsync(Input.SelectedClientId.Value);
            if (selectedClient == null)
                return (null, "El cliente/prospecto seleccionado no esta disponible para tu usuario.");

            request.ClientId = selectedClient.Id;
            request.CustomerName = string.IsNullOrWhiteSpace(request.CustomerName)
                ? (selectedClient.ContactName ?? selectedClient.Name)
                : request.CustomerName;
            request.CustomerEmail = string.IsNullOrWhiteSpace(request.CustomerEmail)
                ? (selectedClient.Email ?? string.Empty)
                : request.CustomerEmail;
            request.CustomerPhone = string.IsNullOrWhiteSpace(request.CustomerPhone)
                ? (selectedClient.Phone ?? string.Empty)
                : request.CustomerPhone;
            request.CustomerLocation = string.IsNullOrWhiteSpace(request.CustomerLocation)
                ? (selectedClient.Address ?? string.Empty)
                : request.CustomerLocation;
            request.CompanyName = string.IsNullOrWhiteSpace(request.CompanyName)
                ? selectedClient.Name
                : request.CompanyName;
        }

        foreach (var p in picks)
        {
            if (!byId.TryGetValue(p.CategoryId, out var cat) || cat.NodeType != QuoteNodeType.Category)
                continue;
            if (!byId.TryGetValue(p.ServiceId, out var srv) || srv.NodeType != QuoteNodeType.Service || srv.ParentId != cat.Id)
                continue;

            QuoteCatalogItem selected = srv;
            string? subName = null;
            if (p.SubproductId.HasValue && byId.TryGetValue(p.SubproductId.Value, out var sub) && sub.NodeType == QuoteNodeType.Subproduct && sub.ParentId == srv.Id)
            {
                selected = sub;
                subName = sub.Name;
            }

            var qty = p.Quantity <= 0 ? 1 : p.Quantity;
            var manual = selected.IsManualPrice || !selected.UnitPrice.HasValue;
            decimal? baseAmount = null;
            decimal? vatAmount = null;
            decimal? lineTotal = null;
            if (!manual)
            {
                var rate = 0.16m;
                var raw = selected.UnitPrice!.Value * qty;
                if (selected.UnitPriceIncludesVat)
                {
                    lineTotal = Math.Round(raw, 2);
                    baseAmount = Math.Round(lineTotal.Value / (1m + rate), 2);
                    vatAmount = Math.Round(lineTotal.Value - baseAmount.Value, 2);
                }
                else
                {
                    baseAmount = Math.Round(raw, 2);
                    vatAmount = Math.Round(baseAmount.Value * rate, 2);
                    lineTotal = Math.Round(baseAmount.Value + vatAmount.Value, 2);
                }
            }

            request.Lines.Add(new QuoteRequestLine
            {
                CategoryName = cat.Name,
                ServiceName = srv.Name,
                SubproductName = subName,
                Description = selected.Description,
                Quantity = qty,
                UnitPrice = selected.UnitPrice,
                PriceIncludesVat = selected.UnitPriceIncludesVat,
                VatRate = 0.16m,
                IsManualPrice = manual,
                OfferType = selected.OfferType,
                ItemImageUrl = selected.ImageUrl,
                BaseAmount = manual ? null : baseAmount,
                VatAmount = manual ? null : vatAmount,
                LineTotal = lineTotal,
                Recurrence = DefaultRecurrenceForOffer(selected.OfferType)
            });
        }

        List<ManualLineVm>? manualPicks;
        try
        {
            manualPicks = JsonSerializer.Deserialize<List<ManualLineVm>>(Input.ManualLinesJson ?? "[]", new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            manualPicks = [];
        }

        if (manualPicks != null)
        {
            foreach (var m in manualPicks.Where(x =>
                !string.IsNullOrWhiteSpace(x.Description) ||
                !string.IsNullOrWhiteSpace(x.ServiceName) ||
                !string.IsNullOrWhiteSpace(x.CategoryName)))
            {
                var qty = m.Quantity <= 0 ? 1 : m.Quantity;
                var desc = string.IsNullOrWhiteSpace(m.Description)
                    ? $"{(string.IsNullOrWhiteSpace(m.ServiceName) ? "Concepto libre" : m.ServiceName.Trim())} ({(string.IsNullOrWhiteSpace(m.CategoryName) ? "Libre" : m.CategoryName.Trim())})"
                    : m.Description.Trim();
                var hasUnitPrice = m.UnitPrice.HasValue && m.UnitPrice.Value > 0m;
                var baseAmount = hasUnitPrice ? Math.Round(m.UnitPrice!.Value * qty, 2) : (decimal?)null;
                var vatAmount = hasUnitPrice ? Math.Round(baseAmount!.Value * 0.16m, 2) : (decimal?)null;
                var lineTotal = hasUnitPrice ? Math.Round(baseAmount!.Value + vatAmount!.Value, 2) : (decimal?)null;
                request.Lines.Add(new QuoteRequestLine
                {
                    CategoryName = string.IsNullOrWhiteSpace(m.CategoryName) ? "Libre" : m.CategoryName.Trim(),
                    ServiceName = string.IsNullOrWhiteSpace(m.ServiceName)
                        ? (string.IsNullOrWhiteSpace(m.Description) ? "Concepto libre" : m.Description.Trim())
                        : m.ServiceName.Trim(),
                    SubproductName = null,
                    Description = desc,
                    Quantity = qty,
                    UnitPrice = m.UnitPrice,
                    PriceIncludesVat = false,
                    VatRate = 0.16m,
                    IsManualPrice = !hasUnitPrice,
                    OfferType = QuoteOfferType.Sale,
                    ItemImageUrl = null,
                    BaseAmount = baseAmount,
                    VatAmount = vatAmount,
                    LineTotal = lineTotal
                    ,
                    Recurrence = NormalizeRecurrence(m.Recurrence)
                });
            }
        }

        // En modo edición, si no llega selección de catálogo desde UI,
        // conservamos las líneas no-manuales existentes para no obligar a re-agregar.
        if (Input.EditQuoteId.HasValue && Input.EditQuoteId.Value != Guid.Empty)
        {
            var hadCatalogSelection = picks.Any();
            if (!hadCatalogSelection)
            {
                var existingCatalogLines = await _db.QuoteRequestLines
                    .AsNoTracking()
                    .Where(x => x.QuoteRequestId == Input.EditQuoteId.Value && !x.IsManualPrice)
                    .Select(x => new QuoteRequestLine
                    {
                        CategoryName = x.CategoryName,
                        ServiceName = x.ServiceName,
                        SubproductName = x.SubproductName,
                        Description = x.Description,
                        Quantity = x.Quantity,
                        UnitPrice = x.UnitPrice,
                        PriceIncludesVat = x.PriceIncludesVat,
                        VatRate = x.VatRate,
                        IsManualPrice = x.IsManualPrice,
                        OfferType = x.OfferType,
                        ItemImageUrl = x.ItemImageUrl,
                        BaseAmount = x.BaseAmount,
                        VatAmount = x.VatAmount,
                        LineTotal = x.LineTotal,
                        Recurrence = x.Recurrence
                    })
                    .ToListAsync();

                request.Lines.AddRange(existingCatalogLines);
            }
        }

        if (request.Lines.Count == 0)
            return (null, "Agrega al menos un concepto para cotizar.");

        var subtotalCatalogNoVat = request.Lines.Where(x => !x.IsManualPrice).Sum(x => x.BaseAmount ?? 0m);
        var vatCatalog = request.Lines.Where(x => !x.IsManualPrice).Sum(x => x.VatAmount ?? 0m);
        var totalCatalog = request.Lines.Where(x => !x.IsManualPrice).Sum(x => x.LineTotal ?? 0m);
        var totalManual = request.Lines.Where(x => x.IsManualPrice).Sum(x => (x.UnitPrice ?? 0m) * x.Quantity);
        var totalBeforeDiscount = totalCatalog + totalManual;
        var discount = ComputeGlobalDiscount(Input.GlobalDiscountType, Input.GlobalDiscountValue, totalBeforeDiscount);

        request.SubtotalBeforeVat = Math.Round(subtotalCatalogNoVat + totalManual - discount, 2);
        request.VatAmount = Math.Round(vatCatalog, 2);
        request.SubtotalAuto = Math.Round(totalBeforeDiscount - discount, 2);
        request.ManualItemsCount = request.Lines.Count(x => x.IsManualPrice);
        request.EstimatedTotal = request.SubtotalAuto;

        if (discount > 0m)
        {
            var discountLabel = string.Equals(Input.GlobalDiscountType, "percent", StringComparison.OrdinalIgnoreCase)
                ? $"{Input.GlobalDiscountValue:0.##}%"
                : $"{discount:C2}";
            request.Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? $"Descuento global aplicado: {discountLabel}"
                : $"{request.Notes}\nDescuento global aplicado: {discountLabel}";
        }

        return (request, null);
    }

    private async Task LoadCatalogPayloadAsync()
    {
        var items = await _db.QuoteCatalogItems
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
        var rules = await _db.QuoteCatalogRules
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync();
        object Pack(QuoteSegment seg)
        {
            var q = items.Where(x => x.Segment == seg).ToList();
            var qr = rules.Where(x => x.Segment == seg).ToList();
            return new
            {
                categories = q.Where(x => x.NodeType == QuoteNodeType.Category).Select(ToNode).ToList(),
                services = q.Where(x => x.NodeType == QuoteNodeType.Service).Select(ToNode).ToList(),
                subproducts = q.Where(x => x.NodeType == QuoteNodeType.Subproduct).Select(ToNode).ToList(),
                rules = qr.Select(x => new { targetItemId = x.TargetItemId, requiredItemId = x.RequiredItemId }).ToList()
            };
        }

        CatalogPayload = new Dictionary<string, object>
        {
            [QuoteSegment.Residential.ToString()] = Pack(QuoteSegment.Residential),
            [QuoteSegment.Business.ToString()] = Pack(QuoteSegment.Business),
            [QuoteSegment.Events.ToString()] = Pack(QuoteSegment.Events)
        };

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isGlobalAdmin = AppRoles.IsGlobalAdmin(User);

        var leads = _db.Clients
            .AsNoTracking()
            .Where(x => x.IsTemporaryLead && x.IsActive)
            .Where(x => isGlobalAdmin || x.OwnerUserId == currentUserId || x.CreatedByUserId == currentUserId);

        var clients = _db.Clients
            .AsNoTracking()
            .Where(x => !x.IsTemporaryLead && x.IsActive)
            .Where(x => isGlobalAdmin || x.OwnerUserId == currentUserId);

        var options = await leads
            .Concat(clients)
            .OrderBy(x => x.IsTemporaryLead ? 0 : 1)
            .ThenBy(x => x.Name)
            .Take(500)
            .Select(x => new ProspectOptionVm(
                x.Id,
                x.Name,
                x.Email ?? "",
                x.Phone ?? "",
                x.Address ?? "",
                x.Name,
                x.IsTemporaryLead,
                string.IsNullOrWhiteSpace(x.ContactName) ? null : x.ContactName!.Trim(),
                string.IsNullOrWhiteSpace(x.Email) ? null : x.Email!.Trim(),
                string.IsNullOrWhiteSpace(x.Phone) ? null : x.Phone!.Trim()))
            .ToListAsync();

        ProspectOptions = options
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .OrderBy(x => x.IsTemporaryLead ? 0 : 1)
            .ThenBy(x => x.Name)
            .ToList();

        var clientIds = ProspectOptions.Select(x => x.Id).Distinct().ToList();
        if (clientIds.Count > 0)
        {
            var contacts = await _db.ClientContacts
                .AsNoTracking()
                .Where(c => clientIds.Contains(c.ClientId))
                .OrderByDescending(c => c.IsPrimary)
                .ThenBy(c => c.Name)
                .Select(c => new
                {
                    c.ClientId,
                    c.Id,
                    c.Name,
                    c.Email,
                    c.Phone,
                    c.Role
                })
                .ToListAsync();

            var contactLookup = contacts
                .GroupBy(x => x.ClientId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => new ContactOptionVm(
                            x.Id,
                            x.Name ?? string.Empty,
                            x.Email ?? string.Empty,
                            x.Phone ?? string.Empty,
                            x.Role))
                        .ToList());

            ClientContactsPayload = new Dictionary<Guid, List<ContactOptionVm>>();
            foreach (var option in ProspectOptions)
            {
                // Solo contactos del modulo "Contactos del cliente"
                // (sin mezclar representante/facturacion/principal del cliente).
                if (contactLookup.TryGetValue(option.Id, out var extras) && extras.Count > 0)
                    ClientContactsPayload[option.Id] = extras;
            }
        }
    }

    private static object ToNode(QuoteCatalogItem x) => new
    {
        id = x.Id,
        parentId = x.ParentId,
        name = x.Name,
        description = x.Description,
        imageUrl = x.ImageUrl,
        unitPrice = x.UnitPrice,
        unitPriceIncludesVat = x.UnitPriceIncludesVat,
        isManualPrice = x.IsManualPrice,
        offerType = x.OfferType.ToString(),
        variantGroup = x.VariantGroup,
        variantValue = x.VariantValue,
        referenceUrl = x.ReferenceUrl
    };

    private async Task<string> NextFolioAsync()
    {
        var year = DateTime.UtcNow.Year;
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);
        var count = await _db.QuoteRequests.CountAsync(x => x.CreatedAt >= from && x.CreatedAt < to);
        return $"COT-{year}-{(count + 1).ToString("D4")}";
    }

    private static string BuildEmailBody(QuoteRequest r, bool toCustomer)
    {
        var intro = toCustomer
            ? "Gracias por tu solicitud. Adjuntamos tu cotizacion en PDF."
            : "Se genero una nueva solicitud de cotizacion. Se adjunta PDF.";

        return $@"
<div style='font-family:Inter,Arial,sans-serif'>
  <h2 style='margin:0 0 8px'>Cotizacion a la medida</h2>
  <p style='margin:0 0 14px'>{intro}</p>
  <p style='margin:0'><strong>Folio:</strong> {r.Folio}</p>
  <p style='margin:0'><strong>Cliente:</strong> {r.CustomerName}</p>
  <p style='margin:0'><strong>Segmento:</strong> {LabelSegment(r.Segment)}</p>
  <p style='margin:0'><strong>Total estimado:</strong> {r.EstimatedTotal?.ToString("C2")}</p>
  <p style='margin:12px 0 0;color:#64748b'>HN Control</p>
</div>";
    }

    public class QuoteInput
    {
        public Guid? EditQuoteId { get; set; }
        public string? ClientToken { get; set; }
        public Guid? SelectedClientId { get; set; }
        public QuoteSegment Segment { get; set; } = QuoteSegment.Residential;
        public string LinesJson { get; set; } = "[]";
        public string ManualLinesJson { get; set; } = "[]";
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerLocation { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? Notes { get; set; }
        public string? GeneralTerms { get; set; }
        public int? ContractTermMonths { get; set; }
        public string? GlobalDiscountType { get; set; } = "none";
        public decimal? GlobalDiscountValue { get; set; }
    }

    private async Task TryLoadEditContextAsync(Guid quoteId)
    {
        var quote = await ResolveEditableQuoteAsync(quoteId);
        if (quote == null)
        {
            ErrorMessage = "No puedes editar esta cotizacion.";
            return;
        }

        IsEditMode = true;
        EditFolio = quote.Folio;
        Input.EditQuoteId = quote.Id;
        Input.SelectedClientId = quote.ClientId;
        Input.Segment = quote.Segment;
        Input.CustomerName = quote.CustomerName;
        Input.CustomerEmail = quote.CustomerEmail;
        Input.CustomerPhone = quote.CustomerPhone;
        Input.CustomerLocation = quote.CustomerLocation;
        Input.CompanyName = quote.CompanyName;
        Input.Notes = quote.Notes;
        Input.GeneralTerms = quote.GeneralTerms;
        Input.ContractTermMonths = quote.ContractTermMonths;

        var manual = quote.Lines
            .Where(x => x.IsManualPrice)
            .OrderBy(x => x.ServiceName)
            .Select(l => new ManualLineVm
            {
                CategoryName = string.IsNullOrWhiteSpace(l.CategoryName) ? "Libre" : l.CategoryName,
                ServiceName = string.IsNullOrWhiteSpace(l.ServiceName)
                    ? (string.IsNullOrWhiteSpace(l.Description) ? "Servicio" : l.Description!)
                    : l.ServiceName,
                Description = l.Description ?? l.SubproductName ?? "",
                Quantity = l.Quantity <= 0 ? 1 : l.Quantity,
                UnitPrice = ResolveEditableUnitPrice(l),
                Recurrence = NormalizeRecurrence(l.Recurrence)
            })
            .ToList();

        InitialManualLinesJson = JsonSerializer.Serialize(manual);
    }

    private static decimal ResolveEditableUnitPrice(QuoteRequestLine line)
    {
        if (line.UnitPrice.HasValue && line.UnitPrice.Value > 0)
            return Math.Round(line.UnitPrice.Value, 2);
        if (line.BaseAmount.HasValue && line.Quantity > 0)
            return Math.Round(line.BaseAmount.Value / line.Quantity, 2);
        if (line.LineTotal.HasValue && line.Quantity > 0)
            return Math.Round((line.PriceIncludesVat ? (line.LineTotal.Value / 1.16m) : line.LineTotal.Value) / line.Quantity, 2);
        return 0m;
    }

    private async Task<QuoteRequest?> ResolveEditableQuoteAsync(Guid quoteId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isGlobalAdmin = AppRoles.IsGlobalAdmin(User);

        var query = _db.QuoteRequests
            .Include(x => x.Lines)
            .Where(x => x.Id == quoteId);

        if (!isGlobalAdmin)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
                return null;
            query = query.Where(x => _db.SalesOpportunities.Any(o =>
                o.QuoteRequestId == x.Id
                && o.OwnerUserId == currentUserId));
        }

        return await query.FirstOrDefaultAsync();
    }

    public class LinePickVm
    {
        public Guid CategoryId { get; set; }
        public Guid ServiceId { get; set; }
        public Guid? SubproductId { get; set; }
        public int Quantity { get; set; }
    }

    public class ManualLineVm
    {
        public string CategoryName { get; set; } = "Libre";
        public string ServiceName { get; set; } = "Concepto libre";
        public string Description { get; set; } = "";
        public int Quantity { get; set; } = 1;
        public decimal? UnitPrice { get; set; }
        public string? Recurrence { get; set; }
    }

    private static string DefaultRecurrenceForOffer(QuoteOfferType offerType) => offerType switch
    {
        QuoteOfferType.MonthlyRent => "Mensual",
        QuoteOfferType.Lease => "Mensual",
        _ => "Unica"
    };

    private static string NormalizeRecurrence(string? recurrence)
    {
        return (recurrence ?? "").Trim().ToLowerInvariant() switch
        {
            "semanal" => "Semanal",
            "mensual" => "Mensual",
            "anual" => "Anual",
            "otro" => "Otro",
            _ => "Unica"
        };
    }

    private static decimal ComputeGlobalDiscount(string? discountType, decimal? discountValue, decimal baseTotal)
    {
        if (baseTotal <= 0m || !discountValue.HasValue || discountValue.Value <= 0m)
            return 0m;

        var value = discountValue.Value;
        if (string.Equals(discountType, "percent", StringComparison.OrdinalIgnoreCase))
            return Math.Round(Math.Clamp(baseTotal * (value / 100m), 0m, baseTotal), 2);

        if (string.Equals(discountType, "amount", StringComparison.OrdinalIgnoreCase))
            return Math.Round(Math.Clamp(value, 0m, baseTotal), 2);

        return 0m;
    }

    private static string LabelSegment(QuoteSegment segment) => segment switch
    {
        QuoteSegment.Business => "Empresarial",
        QuoteSegment.Events => "Eventos",
        _ => "Residencial"
    };

    private async Task<Client?> TryLoadClientContextAsync(string? token)
    {
        HasClientContext = false;
        SkipClientDataStep = false;
        ClientName = string.Empty;

        if (string.IsNullOrWhiteSpace(token)) return null;

        var client = await _db.Clients.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PublicQuoteToken == token);
        if (client == null) return null;

        HasClientContext = true;
        ClientName = client.Name;
        Input.CustomerName = string.IsNullOrWhiteSpace(client.ContactName) ? client.Name : client.ContactName!;
        Input.CustomerEmail = client.Email ?? string.Empty;
        Input.CustomerPhone = client.Phone ?? string.Empty;
        Input.CustomerLocation = client.Address ?? string.Empty;
        Input.CompanyName = client.Name;

        // Mantener datos precargados, pero no saltar el paso 3:
        // el usuario siempre debe poder seleccionar/ajustar cliente y contacto.
        SkipClientDataStep = false;
        return client;
    }

    private async Task<Client> GetOrCreateTemporaryLeadAsync(string customerName, string email, string phone, string location, string? companyName)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var lead = await _db.Clients
            .FirstOrDefaultAsync(x => x.IsTemporaryLead && x.Email != null && x.Email.ToLower() == email);
        if (lead != null)
        {
            lead.Name = string.IsNullOrWhiteSpace(companyName) ? customerName.Trim() : companyName.Trim();
            lead.ContactName = customerName.Trim();
            lead.Phone = phone.Trim();
            lead.Address = location.Trim();
            lead.IsActive = true;
            lead.CreatedByUserId ??= currentUserId;
            lead.OwnerUserId ??= currentUserId;
            await _db.SaveChangesAsync();
            return lead;
        }

        var nextCode = await NextLeadCodeAsync();
        var client = new Client
        {
            ClientCode = nextCode,
            Name = string.IsNullOrWhiteSpace(companyName) ? customerName.Trim() : companyName.Trim(),
            Type = ClientType.Moral,
            Email = email,
            Phone = phone.Trim(),
            ContactName = customerName.Trim(),
            Address = location.Trim(),
            IsTemporaryLead = true,
            IsActive = true,
            CreatedByUserId = currentUserId,
            OwnerUserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Clients.Add(client);
        await _db.SaveChangesAsync();
        return client;
    }

    private async Task<string> NextLeadCodeAsync()
    {
        var codes = await _db.Clients
            .AsNoTracking()
            .Where(c => c.IsTemporaryLead && !string.IsNullOrWhiteSpace(c.ClientCode) && c.ClientCode.StartsWith("HN-VENTA-"))
            .Select(c => c.ClientCode)
            .ToListAsync();

        var max = 0;
        foreach (var code in codes)
        {
            var suffix = code["HN-VENTA-".Length..];
            if (int.TryParse(suffix, out var n) && n > max)
                max = n;
        }
        return $"HN-VENTA-{max + 1:00}";
    }

    private async Task<Client?> ResolveSelectableClientAsync(Guid clientId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isGlobalAdmin = AppRoles.IsGlobalAdmin(User);

        return await _db.Clients
            .AsNoTracking()
            .Where(x => x.Id == clientId && x.IsActive)
            .Where(x => isGlobalAdmin
                        || (x.IsTemporaryLead && (x.OwnerUserId == currentUserId || x.CreatedByUserId == currentUserId))
                        || (!x.IsTemporaryLead && x.OwnerUserId == currentUserId))
            .FirstOrDefaultAsync();
    }

    private async Task<bool> CanUseInternalQuoteAsync(bool requireCreate)
    {
        if (User?.Identity?.IsAuthenticated != true) return false;
        if (User.IsInRole(AppRoles.SuperAdmin)) return true;
        if (!User.IsInRole(AppRoles.Employee) && !User.IsInRole(AppRoles.Admin)) return false;

        if (requireCreate)
            return await _actions.HasActionAsync(User, AppActions.SalesQuotesManage);

        return await _actions.HasActionAsync(User, AppActions.SalesQuotesView)
            || await _actions.HasActionAsync(User, AppActions.SalesQuotesManage);
    }

    private async Task EnsureOpportunityForCurrentUserAsync(QuoteRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return;

        var seller = await _db.SalesSellerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmployeeUserId == userId && x.IsActive);
        if (seller == null)
            return;

        var exists = await _db.SalesOpportunities
            .AsNoTracking()
            .AnyAsync(x => x.QuoteRequestId == request.Id);
        if (exists)
            return;

        var pct = Math.Clamp(seller.DefaultCommissionPercent, 0m, 1m);
        var amount = Math.Round((request.EstimatedTotal ?? request.SubtotalAuto) * pct, 2);

        _db.SalesOpportunities.Add(new SalesOpportunity
        {
            QuoteRequestId = request.Id,
            SellerProfileId = seller.Id,
            ClientId = request.ClientId,
            Status = SalesOpportunityStatus.Prospect,
            WorkflowStage = SalesWorkflowStage.Quotation,
            StageChangedAt = DateTime.UtcNow,
            StageDueAt = DateTime.UtcNow.Date.AddDays(2),
            CommissionPercent = pct,
            CommissionAmount = amount,
            Notes = "Generada automaticamente desde cotizacion interna.",
            OwnerUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
