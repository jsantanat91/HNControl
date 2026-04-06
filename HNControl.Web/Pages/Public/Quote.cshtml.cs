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

    public QuoteModel(ApplicationDbContext db, IQuoteRequestPdfRenderer pdf, IFileStorage storage, IEmailSender email, IConfiguration cfg)
    {
        _db = db;
        _pdf = pdf;
        _storage = storage;
        _email = email;
        _cfg = cfg;
    }

    [BindProperty]
    public QuoteInput Input { get; set; } = new();

    public object CatalogPayload { get; set; } = new { };

    public string? ErrorMessage { get; set; }
    public bool HasClientContext { get; set; }
    public bool SkipClientDataStep { get; set; }
    public string ClientName { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string? token)
    {
        if (!CanUseInternalQuote())
            return RedirectToPage("/Account/Login");

        Input.ClientToken = token;
        await TryLoadClientContextAsync(token);
        await LoadCatalogPayloadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostPreviewAsync()
    {
        if (!CanUseInternalQuote())
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
        if (!CanUseInternalQuote())
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

        _db.QuoteRequests.Add(request);
        await _db.SaveChangesAsync();
        await EnsureOpportunityForCurrentUserAsync(request);

        var pdfBytes = await _pdf.RenderAsync(request);
        var fileName = $"{request.Folio}.pdf";
        var save = await _storage.SaveBytesAsync(pdfBytes, "quotes", fileName, "application/pdf");
        request.PdfStoragePath = save.storagePath;

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
                request.Lines.Add(new QuoteRequestLine
                {
                    CategoryName = string.IsNullOrWhiteSpace(m.CategoryName) ? "Libre" : m.CategoryName.Trim(),
                    ServiceName = string.IsNullOrWhiteSpace(m.ServiceName) ? "Concepto libre" : m.ServiceName.Trim(),
                    SubproductName = null,
                    Description = desc,
                    Quantity = qty,
                    UnitPrice = m.UnitPrice,
                    PriceIncludesVat = false,
                    VatRate = 0.16m,
                    IsManualPrice = true,
                    OfferType = QuoteOfferType.Sale,
                    ItemImageUrl = null,
                    BaseAmount = null,
                    VatAmount = null,
                    LineTotal = null
                    ,
                    Recurrence = NormalizeRecurrence(m.Recurrence)
                });
            }
        }

        if (request.Lines.Count == 0)
            return (null, "Agrega al menos un concepto para cotizar.");

        request.SubtotalBeforeVat = request.Lines.Where(x => !x.IsManualPrice).Sum(x => x.BaseAmount ?? 0m);
        request.VatAmount = request.Lines.Where(x => !x.IsManualPrice).Sum(x => x.VatAmount ?? 0m);
        request.SubtotalAuto = request.Lines.Where(x => !x.IsManualPrice).Sum(x => x.LineTotal ?? 0m);
        request.ManualItemsCount = request.Lines.Count(x => x.IsManualPrice);
        request.EstimatedTotal = request.SubtotalAuto;

        return (request, null);
    }

    private async Task LoadCatalogPayloadAsync()
    {
        const string markerPackage = "INV_SERVICE_PACKAGE";

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
        var inventoryItems = await _db.InventoryItems
            .AsNoTracking()
            .Where(x => x.IsActive && x.QuantityOnHand > 0)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                category = x.Category,
                unit = x.Unit,
                quantityOnHand = x.QuantityOnHand
            })
            .ToListAsync();
        var serviceItems = await _db.QuoteCatalogItems
            .AsNoTracking()
            .Where(x => x.IsActive
                        && x.NodeType == QuoteNodeType.Service
                        && x.VariantGroup == markerPackage)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                category = "Servicios",
                unit = "servicio",
                unitPrice = x.UnitPrice,
                isManualPrice = x.IsManualPrice,
                offerType = x.OfferType.ToString()
            })
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
            [QuoteSegment.Events.ToString()] = Pack(QuoteSegment.Events),
            ["inventoryHardwareItems"] = inventoryItems,
            ["inventoryServiceItems"] = serviceItems
        };
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
        public string? ClientToken { get; set; }
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

        SkipClientDataStep =
            !string.IsNullOrWhiteSpace(Input.CustomerName) &&
            !string.IsNullOrWhiteSpace(Input.CustomerEmail) &&
            !string.IsNullOrWhiteSpace(Input.CustomerPhone) &&
            !string.IsNullOrWhiteSpace(Input.CustomerLocation);
        return client;
    }

    private async Task<Client> GetOrCreateTemporaryLeadAsync(string customerName, string email, string phone, string location, string? companyName)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        var lead = await _db.Clients
            .FirstOrDefaultAsync(x => x.IsTemporaryLead && x.Email != null && x.Email.ToLower() == email);
        if (lead != null)
        {
            lead.Name = string.IsNullOrWhiteSpace(companyName) ? customerName.Trim() : companyName.Trim();
            lead.ContactName = customerName.Trim();
            lead.Phone = phone.Trim();
            lead.Address = location.Trim();
            lead.IsActive = true;
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

    private bool CanUseInternalQuote()
    {
        if (User?.Identity?.IsAuthenticated != true) return false;
        return User.IsInRole(AppRoles.Employee)
            || User.IsInRole(AppRoles.Admin)
            || User.IsInRole(AppRoles.SuperAdmin);
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
