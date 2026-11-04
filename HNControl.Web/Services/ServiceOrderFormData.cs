using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

// The initial render, validation redisplay and asynchronous selectors use the same data.
public static class ServiceOrderFormData
{
    public sealed record ProjectOption(Guid Id, string Text);
    public sealed record ContractOption(Guid Id, string Text, string Label, Guid? ProjectId, string Address);

    // Dates entered by these forms are stored as UTC midnight, without a clock time.
    // Converting that marker to local time would move the selected day backwards.
    public static DateTime EditableDate(DateTime stored) =>
        stored.TimeOfDay == TimeSpan.Zero ? stored.Date : stored.ToLocalTime().Date;

    public static async Task<List<ProjectOption>> ProjectsAsync(ApplicationDbContext db, Guid clientId) =>
        await db.Projects.AsNoTracking().Where(p => p.ClientId == clientId)
            .OrderByDescending(p => p.StartDate)
            .Select(p => new ProjectOption(p.Id, p.Title)).ToListAsync();

    public static async Task<List<ContractOption>> ContractsAsync(ApplicationDbContext db, Guid clientId, Guid? projectId)
    {
        var rows = await db.ClientServiceContracts.AsNoTracking()
            .Where(c => c.ClientId == clientId && (!projectId.HasValue || c.ProjectId == null || c.ProjectId == projectId))
            .OrderBy(c => c.ServiceType).ThenBy(c => c.Label)
            .Select(c => new { c.Id, c.ContractNumber, c.Label, c.Branch, c.ProjectId, c.BranchAddress }).ToListAsync();
        return rows.Select(c => new ContractOption(c.Id,
            (string.IsNullOrWhiteSpace(c.ContractNumber) ? "" : c.ContractNumber + " · ") + c.Label +
            (string.IsNullOrWhiteSpace(c.Branch) ? "" : " · " + c.Branch),
            c.Label, c.ProjectId, c.BranchAddress ?? "")).ToList();
    }

    public static async Task<string?> ValidateAsync(ApplicationDbContext db, Guid clientId, Guid? projectId,
        Guid? contractId, DateTime start, DateTime end)
    {
        if (end.Date < start.Date) return "La fecha de término no puede ser anterior al inicio.";
        if (projectId.HasValue && !await db.Projects.AnyAsync(p => p.Id == projectId && p.ClientId == clientId))
            return "El proyecto seleccionado no pertenece al cliente.";
        if (contractId.HasValue && !await db.ClientServiceContracts.AnyAsync(c => c.Id == contractId &&
            c.ClientId == clientId && (!projectId.HasValue || c.ProjectId == null || c.ProjectId == projectId)))
            return "El contrato seleccionado no pertenece al cliente/proyecto.";
        return null;
    }
}
