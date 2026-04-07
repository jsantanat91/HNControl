using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data.Common;

namespace HNControl.Web.Pages.Projects;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.SuperAdmin)]
public class CreateModel : PageModel
{
    public record ContractOption(Guid Id, string Name);

    private readonly ApplicationDbContext _db;

    public CreateModel(ApplicationDbContext db) => _db = db;

    public SelectList ClientItems { get; set; } = default!;
    public SelectList EmployeeItems { get; set; } = default!;
    public SelectList ContractItems { get; set; } = default!;

    [BindProperty] public InputModel Input { get; set; } = new();

    public string? Info { get; set; }
    public string? Error { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Cliente es requerido.")]
        public Guid ClientId { get; set; }

        [Required(ErrorMessage = "Responsable es requerido.")]
        public string ResponsibleUserId { get; set; } = "";

        [Required(ErrorMessage = "El nombre del proyecto es requerido.")]
        [MaxLength(200)]
        public string Title { get; set; } = "";

        [DataType(DataType.DateTime)]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        [DataType(DataType.DateTime)]
        public DateTime? EstimatedEndDate { get; set; } = DateTime.UtcNow.AddDays(7);

        [MaxLength(400)]
        public string Objective { get; set; } = "";

        [MaxLength(1200)]
        public string Scope { get; set; } = "";

        public string Description { get; set; } = "";
        public string AccessNotes { get; set; } = "";
        public string Comments { get; set; } = "";
        public List<Guid> ContractIds { get; set; } = new();

        [MaxLength(200)]
        public string InitialActivityAssignedTo { get; set; } = "";
        [MaxLength(1000)]
        public string InitialActivityDescription { get; set; } = "";
        [Range(1, 365)]
        public int? InitialActivityDays { get; set; }
    }

    public async Task OnGetAsync(Guid? clientId = null)
    {
        await LoadListsAsync();
        if (clientId.HasValue)
            Input.ClientId = clientId.Value;
        await LoadContractItemsAsync(Input.ClientId);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();
        await LoadContractItemsAsync(Input.ClientId, Input.ContractIds);

        if (string.IsNullOrWhiteSpace(Input.Title))
            ModelState.AddModelError("Input.Title", "El nombre del proyecto es requerido.");

        if (!ModelState.IsValid)
        {
            // Diagnostico util para saber exactamente que campo fallo.
            Error = "Validacion fallo: " + string.Join(" | ",
                ModelState.Where(x => x.Value?.Errors.Count > 0)
                          .Select(x => $"{x.Key} => {string.Join(", ", x.Value!.Errors.Select(e => e.ErrorMessage))}")
            );
            return Page();
        }

        var end = Input.EstimatedEndDate ?? Input.StartDate.AddDays(7);

        var startUtc = TimeUtil.UtcDateTime(Input.StartDate);
        var endUtc = TimeUtil.UtcDateTime(end);

        if (endUtc < startUtc)
        {
            Error = "La fecha estimada no puede ser menor al inicio.";
            return Page();
        }

        var p = new Project
        {
            ClientId = Input.ClientId,
            AssignedUserId = Input.ResponsibleUserId,
            Title = (Input.Title ?? "").Trim(),

            StartDate = startUtc,
            EstimatedEndDate = endUtc,

            Objective = (Input.Objective ?? "").Trim(),
            Scope = (Input.Scope ?? "").Trim(),

            ActivityDescription = (Input.Description ?? "").Trim(),
            AdditionalComments = (Input.Comments ?? "").Trim(),
            AccessNotes = (Input.AccessNotes ?? "").Trim(),

            Status = ProjectStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Projects.Add(p);
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(Input.InitialActivityDescription))
        {
            try
            {
                // En despliegues legacy puede faltar AssignedToUserId.
                // Si no existe, omitimos alta inicial para evitar que truene la creacion del proyecto.
                if (await HasProjectActivityColumnAsync("AssignedToUserId"))
                {
                    _db.ProjectActivities.Add(new ProjectActivity
                    {
                        ProjectId = p.Id,
                        AssignedToName = string.IsNullOrWhiteSpace(Input.InitialActivityAssignedTo)
                            ? (await _db.EmployeeProfiles.AsNoTracking().Where(x => x.UserId == Input.ResponsibleUserId).Select(x => x.FullName).FirstOrDefaultAsync() ?? Input.ResponsibleUserId)
                            : Input.InitialActivityAssignedTo.Trim(),
                        Description = Input.InitialActivityDescription.Trim(),
                        PlannedDays = Math.Max(1, Input.InitialActivityDays ?? 1),
                        SortOrder = 1
                    });
                    await _db.SaveChangesAsync();
                }
                else
                {
                    Info = "Proyecto creado. La actividad inicial se omitio porque la base no tiene la columna nueva de actividades.";
                }
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "42703")
            {
                Error = "Proyecto creado, pero la actividad inicial no se guardo por columna faltante en ProjectActivities.";
            }
            catch
            {
                Error = "Proyecto creado, pero la actividad inicial no se guardo por incompatibilidad de esquema en ProjectActivities.";
            }
        }

        await AssignContractsAsync(p.Id, Input.ClientId, Input.ContractIds);

        return RedirectToPage("/Projects/Details", new { id = p.Id });
    }

    public async Task<IActionResult> OnGetContractsAsync(Guid clientId)
    {
        var rows = await _db.ClientServiceContracts
            .AsNoTracking()
            .Where(c => c.ClientId == clientId)
            .OrderBy(c => c.ServiceType)
            .ThenBy(c => c.Label)
            .Select(c => new
            {
                c.Id,
                text = $"{c.Label} [{c.ServiceType}] · Cuenta {c.AccountNumber}"
            })
            .ToListAsync();

        return new JsonResult(rows);
    }

    private async Task LoadListsAsync(Guid? selectedClientId = null)
    {
        var clients = await _db.Clients.OrderBy(c => c.Name).ToListAsync();
        ClientItems = new SelectList(
            clients,
            "Id",
            "Name",
            selectedClientId ?? (Input.ClientId == Guid.Empty ? null : Input.ClientId));

        var emps = await _db.EmployeeProfiles.OrderBy(e => e.FullName).ToListAsync();
        EmployeeItems = new SelectList(
            emps,
            "UserId",
            "FullName",
            string.IsNullOrWhiteSpace(Input.ResponsibleUserId) ? null : Input.ResponsibleUserId);
    }

    private async Task LoadContractItemsAsync(Guid clientId, List<Guid>? selectedIds = null)
    {
        var rows = clientId == Guid.Empty
            ? new List<ContractOption>()
            : await _db.ClientServiceContracts
                .AsNoTracking()
                .Where(c => c.ClientId == clientId)
                .OrderBy(c => c.ServiceType)
                .ThenBy(c => c.Label)
                .Select(c => new ContractOption(
                    c.Id,
                    $"{c.Label} [{c.ServiceType}] · Cuenta {c.AccountNumber}"))
                .ToListAsync();

        ContractItems = new SelectList(rows, "Id", "Name", selectedIds ?? new List<Guid>());
    }

    private async Task AssignContractsAsync(Guid projectId, Guid clientId, List<Guid>? selectedIds)
    {
        selectedIds ??= new List<Guid>();
        var selected = selectedIds.Distinct().ToHashSet();

        var clientContracts = await _db.ClientServiceContracts
            .Where(c => c.ClientId == clientId)
            .ToListAsync();

        foreach (var c in clientContracts)
        {
            if (selected.Contains(c.Id))
                c.ProjectId = projectId;
            else if (c.ProjectId == projectId)
                c.ProjectId = null;
        }

        await _db.SaveChangesAsync();
    }

    private async Task<bool> HasProjectActivityColumnAsync(string column)
    {
        await using var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'ProjectActivities'
                  AND column_name = @column
            );
            """;
        var p = cmd.CreateParameter();
        p.ParameterName = "@column";
        p.Value = column;
        cmd.Parameters.Add(p);

        var result = await cmd.ExecuteScalarAsync();
        return result is bool b && b;
    }
}


