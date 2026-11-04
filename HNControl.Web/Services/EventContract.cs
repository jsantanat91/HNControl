using HNControl.Web.Models;
using System.Text.Json;

namespace HNControl.Web.Services;

public static class EventContract
{
    public static bool IsEvent(ClientServiceContract? contract) =>
        (contract?.Notes ?? "").Split('\n').Any(x => x.Trim().Equals("[META] Recurrencia=Eventual", StringComparison.OrdinalIgnoreCase));

    public static ClientServiceContract? FrozenContract(ClientLegalDocument doc) =>
        string.IsNullOrWhiteSpace(doc.SigningSnapshotJson) ? doc.ClientServiceContract
        : JsonSerializer.Deserialize<ClientSigningSnapshot>(doc.SigningSnapshotJson)?.Contract;

    public static string? Validate(DateTime? start, DateTime? end, decimal? amount)
    {
        if (!start.HasValue || !end.HasValue) return "Indique inicio y fin del evento.";
        if (end.Value.Date < start.Value.Date) return "El fin del evento no puede ser anterior al inicio.";
        // Both dates are inclusive: Nov 4 through Dec 3 is one calendar month.
        if (end.Value.Date > start.Value.Date.AddMonths(1).AddDays(-1)) return "El contrato eventual admite desde un día hasta un mes, contando ambas fechas.";
        if (!amount.HasValue || amount <= 0) return "Indique el importe único del evento, mayor a cero.";
        return null;
    }

    public static string Period(ClientServiceContract contract) =>
        $"{contract.ContractStartDate:dd/MM/yyyy} al {contract.ContractEndDate:dd/MM/yyyy} ({(contract.ContractEndDate!.Value.Date - contract.ContractStartDate!.Value.Date).Days + 1} día(s))";
}
