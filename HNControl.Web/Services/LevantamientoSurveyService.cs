using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

/// <summary>
/// Payload sent by the public questionnaire (enviar.php) for each submitted survey.
/// Free-text fields are clipped by the service instead of rejected: a 400 is permanent for the form's retry queue.
/// </summary>
public sealed class LevantamientoSurveyInput
{
    [Required, MaxLength(120)] public string ExternalId { get; set; } = "";
    public DateTime? ReceivedAt { get; set; }
    [Required] public string Company { get; set; } = "";
    public string? Rfc { get; set; }
    public string? ClientCode { get; set; }
    public string? ContactName { get; set; }
    public string? ContactRole { get; set; }
    [Required, MaxLength(256)] public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? BusinessLine { get; set; }
    public List<string> Services { get; set; } = [];
    public string? Urgency { get; set; }
    public string? Budget { get; set; }
    public string? Executive { get; set; }
    public string? Visit { get; set; }
    public JsonElement Sections { get; set; }
    public string? PdfFileName { get; set; }
    public string? PdfBase64 { get; set; }
    public string? SitesFileName { get; set; }
    public string? SitesBase64 { get; set; }
}

public sealed record LevantamientoSite(string Address, string Capacity, string Notes);

public sealed record LevantamientoSection(string Title, List<LevantamientoSectionItem> Items);
public sealed record LevantamientoSectionItem(string? Heading, string? Key, string? Value)
{
    public bool IsLink => Value != null && Value.StartsWith("https://www.google.com/maps?q=", StringComparison.Ordinal);
}

public sealed partial class LevantamientoSurveyService(ApplicationDbContext db)
{
    public const int MaxPdfBytes = 5 * 1024 * 1024;

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,120}$")]
    private static partial Regex ExternalIdPattern();
    [GeneratedRegex(@"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$")]
    private static partial Regex RfcPattern();

    public static string NormalizeId(string? value) =>
        new string((value ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '.').ToArray()).ToUpperInvariant();

    public static string Clip(string? value, int max)
    {
        var s = (value ?? "").Trim();
        return s.Length <= max ? s : s[..max];
    }

    /// <summary>Validates and stores a survey. Returns (survey, reused, error).</summary>
    public async Task<(LevantamientoSurvey? Survey, bool Reused, string? Error)> IngestAsync(LevantamientoSurveyInput input, CancellationToken ct = default)
    {
        var externalId = (input.ExternalId ?? "").Trim();
        if (!ExternalIdPattern().IsMatch(externalId)) return (null, false, "ExternalId inválido.");
        if (string.IsNullOrWhiteSpace(input.Company)) return (null, false, "Falta la empresa.");
        var email = (input.Email ?? "").Trim().ToLowerInvariant();
        if (!new EmailAddressAttribute().IsValid(email)) return (null, false, "Correo inválido.");
        if (input.Sections.ValueKind is not (JsonValueKind.Array or JsonValueKind.Undefined)) return (null, false, "Secciones inválidas.");
        var sectionsJson = input.Sections.ValueKind == JsonValueKind.Array ? input.Sections.GetRawText() : "[]";
        if (sectionsJson.Length > 400_000) return (null, false, "Secciones demasiado grandes.");

        byte[]? pdf = null;
        if (!string.IsNullOrWhiteSpace(input.PdfBase64))
        {
            try { pdf = Convert.FromBase64String(input.PdfBase64); }
            catch (FormatException) { return (null, false, "PDF inválido."); }
            if (pdf.Length > MaxPdfBytes || pdf.Length < 5 || Encoding.ASCII.GetString(pdf, 0, 5) != "%PDF-")
                return (null, false, "PDF inválido.");
        }

        byte[]? sites = null;
        var sitesName = Clip(input.SitesFileName, 200);
        if (!string.IsNullOrWhiteSpace(input.SitesBase64))
        {
            try { sites = Convert.FromBase64String(input.SitesBase64); }
            catch (FormatException) { return (null, false, "Archivo de sitios inválido."); }
            var ext = Path.GetExtension(sitesName).ToLowerInvariant();
            if (sites.Length > MaxPdfBytes || ext is not (".xlsx" or ".xls" or ".csv"))
                return (null, false, "Archivo de sitios inválido.");
        }

        var existing = await db.LevantamientoSurveys.FirstOrDefaultAsync(x => x.ExternalId == externalId, ct);
        if (existing != null) return (existing, true, null);

        var rfc = NormalizeId(input.Rfc);
        var survey = new LevantamientoSurvey
        {
            ExternalId = externalId,
            ReceivedAt = input.ReceivedAt?.ToUniversalTime() ?? DateTime.UtcNow,
            ClientCodeSent = Clip(input.ClientCode, 20),
            Rfc = RfcPattern().IsMatch(rfc) ? rfc : "",
            Company = Clip(input.Company, 200),
            ContactName = Clip(input.ContactName, 160),
            ContactRole = Clip(input.ContactRole, 120),
            Email = Clip(email, 256),
            Phone = Clip(input.Phone, 60),
            City = Clip(input.City, 200),
            Address = Clip(input.Address, 400),
            BusinessLine = Clip(input.BusinessLine, 180),
            Services = Clip(string.Join(", ", (input.Services ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).Take(20)), 400),
            Urgency = Clip(input.Urgency, 120),
            Budget = Clip(input.Budget, 120),
            Executive = Clip(input.Executive, 120),
            Visit = Clip(input.Visit, 120),
            SectionsJson = sectionsJson,
            PdfFileName = Clip(input.PdfFileName, 200),
            PdfBytes = pdf,
            SitesFileName = sites == null ? "" : sitesName,
            SitesBytes = sites
        };
        survey.ClientId = await MatchClientIdAsync(survey.ClientCodeSent, survey.Rfc, ct);
        db.LevantamientoSurveys.Add(survey);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Concurrent retry of the same submission: return the stored one.
            db.Entry(survey).State = EntityState.Detached;
            var stored = await db.LevantamientoSurveys.FirstOrDefaultAsync(x => x.ExternalId == externalId, ct);
            if (stored == null) throw;
            return (stored, true, null);
        }
        return (survey, false, null);
    }

    /// <summary>Client code (from a verified lookup) wins; otherwise the RFC of an active client.</summary>
    public async Task<Guid?> MatchClientIdAsync(string? clientCode, string? rfc, CancellationToken ct = default)
    {
        var code = NormalizeId(clientCode);
        if (code.Length >= 3)
        {
            var byCode = await db.Clients.AsNoTracking()
                .Where(x => x.IsActive && x.ClientCode.Replace("-", "").Replace(" ", "").ToUpper() == code)
                .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
            if (byCode != null) return byCode;
        }
        var id = NormalizeId(rfc);
        if (!RfcPattern().IsMatch(id)) return null;
        return await db.Clients.AsNoTracking()
            .Where(x => x.IsActive && (x.Rfc ?? "").Replace(" ", "").Replace("-", "").Replace(".", "").ToUpper() == id)
            .OrderBy(x => x.IsTemporaryLead).ThenBy(x => x.CreatedAt)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>Creates the client from the survey data and links every unlinked survey with the same RFC.</summary>
    public async Task<(Client? Client, string? Error)> CreateClientAsync(Guid surveyId, string? userId, CancellationToken ct = default)
    {
        var survey = await db.LevantamientoSurveys.FirstOrDefaultAsync(x => x.Id == surveyId, ct);
        if (survey == null) return (null, "No se encontró el levantamiento.");
        if (survey.ClientId != null) return (null, "El levantamiento ya está ligado a un cliente.");
        if (!string.IsNullOrEmpty(survey.Rfc) && await MatchClientIdAsync(null, survey.Rfc, ct) is Guid existingId)
        {
            survey.ClientId = existingId; survey.UpdatedAt = DateTime.UtcNow; survey.UpdatedByUserId = userId;
            await db.SaveChangesAsync(ct);
            return (await db.Clients.FirstAsync(x => x.Id == existingId, ct), null);
        }

        var (municipality, state) = SplitCity(survey.City);
        var client = new Client
        {
            ClientCode = await NextClientCodeAsync(ct),
            Kind = survey.Rfc.Length == 13 ? ClientKind.Fisica : ClientKind.Moral,
            Name = survey.Company,
            Rfc = string.IsNullOrEmpty(survey.Rfc) ? null : survey.Rfc,
            Email = survey.Email,
            BillingEmail = survey.Email,
            Phone = Clip(survey.Phone, 40),
            ContactName = Clip(survey.ContactName, 120),
            Address = survey.Address,
            Municipality = municipality,
            State = state,
            BusinessLine = survey.BusinessLine,
            OwnerUserId = userId,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        if (!string.IsNullOrWhiteSpace(survey.ContactName))
            client.Contacts.Add(new ClientContact { Name = Clip(survey.ContactName, 180), Email = survey.Email, Phone = survey.Phone, Role = survey.ContactRole, IsPrimary = true });
        db.Clients.Add(client);

        var siblings = string.IsNullOrEmpty(survey.Rfc)
            ? [survey]
            : await db.LevantamientoSurveys.Where(x => x.ClientId == null && x.Rfc == survey.Rfc).ToListAsync(ct);
        if (!siblings.Contains(survey)) siblings.Add(survey);
        foreach (var s in siblings) { s.ClientId = client.Id; s.UpdatedAt = DateTime.UtcNow; s.UpdatedByUserId = userId; }
        await db.SaveChangesAsync(ct);
        return (client, null);
    }

    public async Task<string?> LinkClientAsync(Guid surveyId, Guid clientId, string? userId, CancellationToken ct = default)
    {
        var survey = await db.LevantamientoSurveys.FirstOrDefaultAsync(x => x.Id == surveyId, ct);
        if (survey == null) return "No se encontró el levantamiento.";
        if (survey.ServiceOrderId != null) return "Ya tiene orden de servicio; no se puede cambiar el cliente.";
        if (!await db.Clients.AnyAsync(x => x.Id == clientId && x.IsActive, ct)) return "Cliente no válido.";
        survey.ClientId = clientId; survey.UpdatedAt = DateTime.UtcNow; survey.UpdatedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>Records the feasibility decision (2.6.3). Locked once a service order exists.</summary>
    public async Task<string?> DecideAsync(Guid surveyId, bool viable, IDictionary<string, string?>? answers, string? comments, string? userId, CancellationToken ct = default)
    {
        var survey = await db.LevantamientoSurveys.FirstOrDefaultAsync(x => x.Id == surveyId, ct);
        if (survey == null) return "No se encontró el levantamiento.";
        if (survey.ServiceOrderId != null) return "Ya tiene orden de servicio; el dictamen ya no se puede cambiar.";
        var results = LevantamientoReview.Normalize(answers);
        var error = LevantamientoReview.Validate(viable, results, comments);
        if (error != null) return error;
        survey.ReviewChecksJson = JsonSerializer.Serialize(results);
        survey.DecisionComments = Clip(comments, 4000);
        survey.Status = viable ? LevantamientoSurveyStatus.Feasible : LevantamientoSurveyStatus.NotFeasible;
        survey.DecidedAt = DateTime.UtcNow;
        survey.DecidedByUserId = userId;
        survey.DecisionEmailSentAt = null;
        survey.UpdatedAt = DateTime.UtcNow; survey.UpdatedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return null;
    }

    public static bool IsViable(LevantamientoSurveyStatus status) =>
        status is LevantamientoSurveyStatus.Feasible or LevantamientoSurveyStatus.Quoted;

    /// <summary>Builds the technical survey order. The caller queues/sends notifications. Requires a linked client and a viable decision.</summary>
    public async Task<(ServiceOrder? Order, string? Error)> CreateOrderAsync(Guid surveyId, string? userId, string detailUrl, CancellationToken ct = default)
    {
        var survey = await db.LevantamientoSurveys.FirstOrDefaultAsync(x => x.Id == surveyId, ct);
        if (survey == null) return (null, "No se encontró el levantamiento.");
        if (survey.ServiceOrderId != null) return (null, "Este levantamiento ya tiene orden de servicio.");
        if (survey.ClientId == null || !await db.Clients.AnyAsync(x => x.Id == survey.ClientId && x.IsActive, ct))
            return (null, "Primero crea o liga el cliente; la orden de servicio necesita un cliente existente.");
        if (survey.Status == LevantamientoSurveyStatus.NotFeasible)
            return (null, "El proyecto está dictaminado como no viable; no se puede crear la orden de servicio.");
        if (!IsViable(survey.Status))
            return (null, "Primero emite el dictamen de viabilidad; la orden solo se crea para proyectos viables.");

        var order = new ServiceOrder
        {
            Title = Clip("Levantamiento técnico · " + (survey.Services.Length > 0 ? survey.Services : survey.Company), 200),
            Type = ServiceOrderType.LevantamientoTecnico,
            Status = ServiceOrderStatus.Created,
            ClientId = survey.ClientId.Value,
            Description = Clip(BuildOrderDescription(survey, detailUrl), 2000),
            StartedAt = TimeUtil.UtcDate(DateTime.Today),
            EstimatedEndDate = TimeUtil.UtcDate(DateTime.Today.AddDays(2)),
            PublicToken = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            CurrentArea = ServiceOrderWorkflowArea.Levantamiento
        };
        db.ServiceOrders.Add(order);
        survey.ServiceOrder = order;
        survey.UpdatedAt = DateTime.UtcNow; survey.UpdatedByUserId = userId;
        return (order, null);
    }

    public static string BuildOrderDescription(LevantamientoSurvey s, string detailUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Levantamiento recibido desde el formulario web");
        sb.AppendLine($"Detalle completo, PDF y archivos: {detailUrl}");
        sb.AppendLine($"Servicios: {s.Services}");
        sb.AppendLine($"Contacto: {s.ContactName}{(s.ContactRole.Length > 0 ? " (" + s.ContactRole + ")" : "")} · {s.Phone} · {s.Email}");
        if (s.Address.Length > 0 || s.City.Length > 0) sb.AppendLine($"Sitio: {s.Address} {s.City}".Trim());
        if (s.Urgency.Length > 0) sb.AppendLine($"Urgencia: {s.Urgency}");
        if (s.Visit.Length > 0) sb.AppendLine($"Visita técnica: {s.Visit}");
        var sites = ReadSites(s.SitesFileName, s.SitesBytes);
        if (sites.Count > 0)
        {
            sb.AppendLine().AppendLine($"[Sitios: {sites.Count}]");
            foreach (var site in sites.Take(15)) sb.AppendLine($"{site.Address} · {site.Capacity} MB");
            if (sites.Count > 15) sb.AppendLine($"… y {sites.Count - 15} más en el archivo");
        }
        foreach (var sec in ParseSections(s.SectionsJson).Where(x => x.Title is not ("Datos de la empresa" or "Proyecto y seguimiento")))
        {
            sb.AppendLine().AppendLine($"[{sec.Title}]");
            foreach (var it in sec.Items.Where(i => i.Key != null && !i.IsLink))
                sb.AppendLine($"{it.Key}: {it.Value}");
        }
        return sb.ToString().TrimEnd();
    }

    public static List<LevantamientoSection> ParseSections(string? json)
    {
        var result = new List<LevantamientoSection>();
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;
            foreach (var sec in doc.RootElement.EnumerateArray())
            {
                if (sec.ValueKind != JsonValueKind.Object) continue;
                var items = new List<LevantamientoSectionItem>();
                if (sec.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    foreach (var it in arr.EnumerateArray())
                    {
                        if (it.ValueKind != JsonValueKind.Object) continue;
                        if (it.TryGetProperty("h", out var h)) items.Add(new(h.ToString(), null, null));
                        else items.Add(new(null, it.TryGetProperty("k", out var k) ? k.ToString() : "", it.TryGetProperty("v", out var v) ? v.ToString() : ""));
                    }
                result.Add(new(sec.TryGetProperty("titulo", out var t) ? t.ToString() : "", items));
            }
        }
        catch (JsonException) { }
        return result;
    }

    /// <summary>Reads the multi-site file: columns "Dirección" and "MB" (by header, otherwise A and B). Max 300 rows.</summary>
    public static List<LevantamientoSite> ReadSites(string? fileName, byte[]? bytes)
    {
        var rows = new List<string[]>();
        if (bytes == null || bytes.Length == 0) return [];
        try
        {
            if (Path.GetExtension(fileName ?? "").Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                var text = Encoding.UTF8.GetString(bytes).TrimStart('﻿');
                var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                var sep = lines.Length > 0 && lines[0].Count(c => c == ';') > lines[0].Count(c => c == ',') ? ';' : ',';
                rows.AddRange(lines.Take(301).Select(l => l.Split(sep).Select(c => c.Trim().Trim('"')).ToArray()));
            }
            else
            {
                using var ms = new MemoryStream(bytes);
                using var wb = new ClosedXML.Excel.XLWorkbook(ms);
                var ws = wb.Worksheets.FirstOrDefault();
                if (ws == null) return [];
                foreach (var r in ws.RowsUsed().Take(301))
                    rows.Add(Enumerable.Range(1, Math.Max(3, r.LastCellUsed()?.Address.ColumnNumber ?? 3)).Select(i => r.Cell(i).GetFormattedString().Trim()).ToArray());
            }
        }
        catch { return []; }
        if (rows.Count == 0) return [];

        var head = rows[0].Select(h => h.ToLowerInvariant()).ToArray();
        // Keys in priority order, matched at a word start ("mb" must not match "nombre").
        int Col(params string[] keys) => keys
            .Select(k => Array.FindIndex(head, h => Regex.IsMatch(h, @"(^|[^\p{L}])" + k)))
            .FirstOrDefault(i => i >= 0, -1);
        var addr = Col("direcc", "domicilio", "ubicac", "sitio");
        var mb = Col("mb", "capacidad", "velocidad", "ancho");
        var hasHeader = addr >= 0 || mb >= 0;
        if (addr < 0) addr = 0;
        if (mb < 0) mb = addr == 0 ? 1 : 0;
        string At(string[] r, int i) => i < r.Length ? r[i] : "";
        return rows.Skip(hasHeader ? 1 : 0)
            .Select(r => new LevantamientoSite(At(r, addr), At(r, mb),
                string.Join(" · ", r.Where((c, i) => i != addr && i != mb && !string.IsNullOrWhiteSpace(c)))))
            .Where(s => s.Address.Length > 0 || s.Capacity.Length > 0)
            .Take(300).ToList();
    }

    public static (string? Municipality, string? State) SplitCity(string? city)
    {
        var parts = (city ?? "").Split(',', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (null, null),
            1 => (Clip(parts[0], 120), null),
            _ => (Clip(parts[0], 120), Clip(parts[1], 80))
        };
    }

    private async Task<string> NextClientCodeAsync(CancellationToken ct)
    {
        var codes = await db.Clients.AsNoTracking()
            .Where(c => c.ClientCode.StartsWith("HN-")).Select(c => c.ClientCode).ToListAsync(ct);
        // Pending (not yet saved) clients in this context also count.
        codes.AddRange(db.ChangeTracker.Entries<Client>().Select(e => e.Entity.ClientCode));
        var max = 0;
        foreach (var code in codes)
            if (code.StartsWith("HN-") && int.TryParse(code.AsSpan(3), out var n) && n > max) max = n;
        return $"HN-{max + 1:0000}";
    }
}
