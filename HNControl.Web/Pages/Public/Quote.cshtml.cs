using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Public;

[AllowAnonymous]
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
    public string ClientName { get; set; } = string.Empty;

    public async Task OnGetAsync(string? token)
    {
        Input.ClientToken = token;
        await TryLoadClientContextAsync(token);
        await LoadCatalogPayloadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var boundClient = await TryLoadClientContextAsync(Input.ClientToken);
        if (boundClient != null)
        {
            Input.CustomerName = string.IsNullOrWhiteSpace(boundClient.ContactName) ? boundClient.Name : boundClient.ContactName!;
            Input.CustomerEmail = boundClient.Email ?? string.Empty;
            Input.CustomerPhone = boundClient.Phone ?? string.Empty;
            Input.CustomerLocation = boundClient.Address ?? string.Empty;
            Input.CompanyName = boundClient.Name;
        }

        await LoadCatalogPayloadAsync();

        if (string.IsNullOrWhiteSpace(Input.CustomerName)
            || string.IsNullOrWhiteSpace(Input.CustomerEmail)
            || string.IsNullOrWhiteSpace(Input.CustomerPhone)
            || string.IsNullOrWhiteSpace(Input.CustomerLocation))
        {
            ErrorMessage = "Completa nombre, correo, telefono y ubicacion.";
            return Page();
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
            ErrorMessage = "No se pudieron leer los conceptos seleccionados.";
            return Page();
        }

        if (picks == null || picks.Count == 0)
        {
            ErrorMessage = "Agrega al menos un concepto para cotizar.";
            return Page();
        }

        var activeItems = await _db.QuoteCatalogItems
            .AsNoTracking()
            .Where(x => x.IsActive && x.Segment == Input.Segment)
            .ToListAsync();

        var byId = activeItems.ToDictionary(x => x.Id, x => x);

        var request = new QuoteRequest
        {
            Folio = await NextFolioAsync(),
            Segment = Input.Segment,
            Status = QuoteRequestStatus.New,
            CustomerName = Input.CustomerName.Trim(),
            CustomerEmail = Input.CustomerEmail.Trim(),
            CustomerPhone = Input.CustomerPhone.Trim(),
            CustomerLocation = Input.CustomerLocation.Trim(),
            CompanyName = string.IsNullOrWhiteSpace(Input.CompanyName) ? null : Input.CompanyName.Trim(),
            Notes = string.IsNullOrWhiteSpace(Input.Notes) ? null : Input.Notes.Trim(),
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
            BaseAmount = manual ? null : baseAmount,
            VatAmount = manual ? null : vatAmount,
            LineTotal = lineTotal
        });
        }

        if (request.Lines.Count == 0)
        {
            ErrorMessage = "No hay conceptos validos en la seleccion.";
            return Page();
        }

        request.SubtotalBeforeVat = request.Lines.Where(x => !x.IsManualPrice).Sum(x => x.BaseAmount ?? 0m);
        request.VatAmount = request.Lines.Where(x => !x.IsManualPrice).Sum(x => x.VatAmount ?? 0m);
        request.SubtotalAuto = request.Lines.Where(x => !x.IsManualPrice).Sum(x => x.LineTotal ?? 0m);
        request.ManualItemsCount = request.Lines.Count(x => x.IsManualPrice);
        request.EstimatedTotal = request.SubtotalAuto;

        _db.QuoteRequests.Add(request);
        await _db.SaveChangesAsync();

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

            return RedirectToPage("/Public/QuoteSuccess", new { folio = request.Folio, sent = 1 });
        }
        catch
        {
            request.Status = QuoteRequestStatus.EmailError;
            await _db.SaveChangesAsync();
            return RedirectToPage("/Public/QuoteSuccess", new { folio = request.Folio, sent = 0 });
        }
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
            [QuoteSegment.Business.ToString()] = Pack(QuoteSegment.Business)
        };
    }

    private static object ToNode(QuoteCatalogItem x) => new
    {
        id = x.Id,
        parentId = x.ParentId,
        name = x.Name,
        description = x.Description,
        unitPrice = x.UnitPrice,
        unitPriceIncludesVat = x.UnitPriceIncludesVat,
        isManualPrice = x.IsManualPrice,
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
  <p style='margin:0'><strong>Segmento:</strong> {(r.Segment == QuoteSegment.Business ? "Empresarial" : "Residencial")}</p>
  <p style='margin:0'><strong>Total estimado:</strong> {r.EstimatedTotal?.ToString("C2")}</p>
  <p style='margin:12px 0 0;color:#64748b'>HN Control</p>
</div>";
    }

    public class QuoteInput
    {
        public string? ClientToken { get; set; }
        public QuoteSegment Segment { get; set; } = QuoteSegment.Residential;
        public string LinesJson { get; set; } = "[]";
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerLocation { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? Notes { get; set; }
    }

    public class LinePickVm
    {
        public Guid CategoryId { get; set; }
        public Guid ServiceId { get; set; }
        public Guid? SubproductId { get; set; }
        public int Quantity { get; set; }
    }

    private async Task<Client?> TryLoadClientContextAsync(string? token)
    {
        HasClientContext = false;
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
        return client;
    }
}
