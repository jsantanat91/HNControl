using System.Text.Json;
using HNControl.Web.Models;
using static HNControl.Web.Services.ClientCommunicationTemplates;

namespace HNControl.Web.Services;

/// <summary>
/// Dictamen de viabilidad de una factibilidad (2.6.3): puntos técnicos revisados, decisión y correo al cliente.
/// "No viable" detiene el flujo (no se crea orden); el cliente sí puede crearse o ligarse.
/// </summary>
public static class LevantamientoReview
{
    public const string Yes = "si", No = "no", NotApplicable = "na";

    public sealed record Check(string Key, string Label, string Hint);
    public sealed record CheckResult(string Key, string Result);

    /// <summary>Variables técnicas que se validan antes de comprometer el proyecto.</summary>
    public static readonly IReadOnlyList<Check> Checks =
    [
        new("cobertura", "Cobertura en la ubicación", "Red propia o de proveedor llega al sitio"),
        new("capacidad", "Capacidad disponible en red y servidores", "Puertos, ancho de banda, capacidad en el nodo o servidor"),
        new("ultima_milla", "Última milla / proveedor", "Fibra, enlace o carrier disponible para el acceso"),
        new("sitio", "Condiciones del sitio", "Energía, canalización, espacio y permisos de acceso"),
        new("equipo", "Equipamiento disponible", "Equipos y materiales en inventario o con proveedor"),
        new("tiempos", "Tiempo de entrega viable", "Se puede cumplir la urgencia solicitada")
    ];

    public static string ResultLabel(string? result) => result switch
    {
        Yes => "Sí",
        No => "No",
        NotApplicable => "No aplica",
        _ => "Pendiente"
    };

    public static List<CheckResult> Parse(string? json)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<CheckResult>>(string.IsNullOrWhiteSpace(json) ? "[]" : json) ?? [];
            return items.Where(x => Checks.Any(c => c.Key == x.Key)).ToList();
        }
        catch (JsonException) { return []; }
    }

    public static string? ResultOf(IEnumerable<CheckResult> results, string key) =>
        results.FirstOrDefault(x => x.Key == key)?.Result;

    /// <summary>Keeps only known keys and valid answers, in the catalog order.</summary>
    public static List<CheckResult> Normalize(IDictionary<string, string?>? answers) =>
        Checks.Select(c => new CheckResult(c.Key, answers != null && answers.TryGetValue(c.Key, out var v) && v is Yes or No or NotApplicable ? v! : ""))
            .Where(x => x.Result.Length > 0).ToList();

    /// <summary>Validates a decision. Viable: every point reviewed and none answered "No". Not viable: the reason is required.</summary>
    public static string? Validate(bool viable, IReadOnlyList<CheckResult> results, string? comments)
    {
        if (viable)
        {
            var pending = Checks.Where(c => ResultOf(results, c.Key) == null).Select(c => c.Label).ToList();
            if (pending.Count > 0) return "Para marcarlo como viable revisa todos los puntos (Sí o No aplica). Pendientes: " + string.Join(", ", pending) + ".";
            var failed = Checks.Where(c => ResultOf(results, c.Key) == No).Select(c => c.Label).ToList();
            if (failed.Count > 0) return "No se puede marcar como viable con puntos en \"No\": " + string.Join(", ", failed) + ".";
            return null;
        }
        if (string.IsNullOrWhiteSpace(comments) || comments.Trim().Length < 10)
            return "Para marcarlo como no viable escribe la causa para el cliente (mínimo 10 caracteres).";
        return null;
    }

    public static string EmailSubject(LevantamientoSurvey s) => s.Status == LevantamientoSurveyStatus.Feasible
        ? $"HN Solutions · Su proyecto es viable: {s.Company}"
        : $"HN Solutions · Resultado de la evaluación de su proyecto: {s.Company}";

    /// <summary>Correo al cliente con el formato homologado de Control (<see cref="ClientCommunicationTemplates.Layout"/>).</summary>
    public static string EmailBody(LevantamientoSurvey s, int siteCount, bool withPdf)
    {
        var viable = s.Status == LevantamientoSurveyStatus.Feasible;
        var results = Parse(s.ReviewChecksJson);
        var greeting = string.IsNullOrWhiteSpace(s.ContactName) ? s.Company : s.ContactName;
        var rows = string.Join("", Checks
            .Select(c => (c.Label, Result: ResultOf(results, c.Key)))
            .Where(x => x.Result is Yes or No)
            .Select(x => $"""
                <tr><td style="padding:9px 14px;border-bottom:1px solid #e2e8f0">{E(x.Label)}</td>
                <td style="padding:9px 14px;border-bottom:1px solid #e2e8f0;text-align:right;font-weight:bold;color:{(x.Result == Yes ? "#15803d" : "#b91c1c")}">{(x.Result == Yes ? "Disponible" : "No disponible")}</td></tr>
                """));
        var checks = rows.Length == 0 ? "" : $"""
            <h2 style="font-size:19px;color:#086db4;margin-top:28px">02 · Puntos evaluados</h2>
            <table role="presentation" style="width:100%;background:#f8fafc;border-collapse:collapse">{rows}</table>
            """;
        var comments = string.IsNullOrWhiteSpace(s.DecisionComments) ? "" : $"""
            <p style="background:{(viable ? "#edf6fc" : "#fdf2f2")};padding:16px;border-left:3px solid {(viable ? "#086db4" : "#b91c1c")};white-space:pre-line">{E(s.DecisionComments.Trim())}</p>
            """;
        var where = string.Join(" · ", new[]
        {
            siteCount > 0 ? $"{siteCount} sitio(s) del archivo enviado" : "",
            string.Join(", ", new[] { s.Address, s.City }.Where(x => !string.IsNullOrWhiteSpace(x)))
        }.Where(x => x.Length > 0));

        return Layout(viable ? "Su proyecto es viable" : "Resultado de la evaluación de su proyecto", $"""
            <p>Estimado(a) <strong>{E(greeting)}</strong>:</p>
            <p>Gracias por enviarnos el levantamiento de <strong>{E(s.Company)}</strong>. Nuestro equipo de ingeniería revisó la información y estas son las conclusiones.</p>
            <h2 style="font-size:19px;color:#086db4;margin-top:28px">01 · Resultado</h2>
            <p style="font-size:17px;font-weight:bold;color:{(viable ? "#15803d" : "#b91c1c")};margin:0 0 6px">{(viable ? "Proyecto viable" : "Proyecto no viable por ahora")}</p>
            <p style="margin-top:0"><strong>Servicios solicitados:</strong> {E(s.Services)}<br>
            {(where.Length > 0 ? $"<strong>Ubicación:</strong> {E(where)}<br>" : "")}
            <strong>Levantamiento recibido:</strong> {s.ReceivedAt.ToLocalTime():dd/MM/yyyy}</p>
            {comments}
            {checks}
            <h2 style="font-size:19px;color:#086db4;margin-top:28px">{(rows.Length == 0 ? "02" : "03")} · Siguientes pasos</h2>
            {(viable
                ? "<p>Un ejecutivo se pondrá en contacto con usted para confirmar el alcance, agendar la visita o el levantamiento técnico en sitio y presentarle la propuesta. Si tiene dudas, puede responder a este correo.</p>"
                : "<p>Con las condiciones actuales no podemos comprometer el servicio. Si cambian las condiciones (otra ubicación, capacidad o fechas), con gusto revisamos de nuevo su proyecto: responda a este correo o envíe un nuevo levantamiento.</p>")}
            {(withPdf ? "<p>Adjuntamos el levantamiento que nos envió para su referencia.</p>" : "")}
            <p>Quedamos a sus órdenes.</p>
            """, "Ingeniería y Preventa");
    }
}
