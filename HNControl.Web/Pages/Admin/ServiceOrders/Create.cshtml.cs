using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Admin.ServiceOrders;

[Authorize(Roles = AppRoles.Admin)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    public SelectList ClientItems { get; set; } = default!;
    public SelectList EmployeeItems { get; set; } = default!;
    public SelectList TypeItems => new(Enum.GetValues<ServiceOrderType>().Select(x => new { Id = x, Name = ((Enum)x).GetDisplayName() }), "Id", "Name");

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public Guid ClientId { get; set; }
        [Required] public string AssignedUserId { get; set; } = "";
        [Required] public ServiceOrderType Type { get; set; } = ServiceOrderType.NuevaInstalacion;
        [DataType(DataType.Date)] public DateTime? EstimatedEndDate { get; set; } = TimeUtil.UtcDate(DateTime.UtcNow.AddDays(7));
        [Required, MaxLength(200)] public string Title { get; set; } = "";
        [MaxLength(2000)] public string Description { get; set; } = "";
    }

    public async Task OnGetAsync() => await LoadListsAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();
        if (!ModelState.IsValid) return Page();

        var order = new ServiceOrder
        {
            ClientId = Input.ClientId,
            AssignedUserId = Input.AssignedUserId,
            Type = Input.Type,
            Title = Input.Title.Trim(),
            Description = (Input.Description ?? "").Trim(),
            EstimatedEndDate = TimeUtil.UtcDate(Input.EstimatedEndDate),
            Status = ServiceOrderStatus.Created,
            CreatedAt = DateTime.UtcNow,
            PublicToken = Guid.NewGuid().ToString("N")
        };

        _db.ServiceOrders.Add(order);

        // Checklist default por tipo
        var tpl = await _db.ServiceOrderChecklistTemplates
    .Include(t => t.Items)
    .Where(t => t.Type == Input.Type && t.IsActive)
    .OrderByDescending(t => t.UpdatedAt)
    .FirstOrDefaultAsync();

        var items = tpl?.Items.OrderBy(x => x.SortOrder).ToList() ?? new List<ServiceOrderChecklistTemplateItem>();

        var sort = 1;
        foreach (var t in items)
        {
            order.Checklist.Add(new ServiceOrderChecklistItem
            {
                OrderId = order.Id,
                SortOrder = sort++,
                Category = t.Category,
                Title = t.Title,
                IsRequired = t.IsRequired,
                IsDone = false,
                Notes = ""
            });
        }


        await _db.SaveChangesAsync();
        return RedirectToPage("/Admin/ServiceOrders/Details", new { id = order.Id });
    }

    private async Task LoadListsAsync()
    {
        var clients = await _db.Clients.OrderBy(c => c.Name).ToListAsync();
        ClientItems = new SelectList(clients, "Id", "Name");

        var emps = await _db.EmployeeProfiles.OrderBy(e => e.FullName).ToListAsync();
        EmployeeItems = new SelectList(emps, "UserId", "FullName");
    }

    private static List<string> DefaultChecklist(ServiceOrderType type)
    {
        return type switch
        {
            ServiceOrderType.NuevaInstalacion => new List<string>
            {
                "Levantamiento técnico",
                "Cableado (rutas, etiquetas, terminaciones)",
                "Registros / Canalizaciones",
                "Tubería / Charola / Canaleta",
                "Cámaras (montaje y enfoque)",
                "DVR/NVR (configuración y almacenamiento)",
                "Accesorios (fuentes, conectores, protecciones)",
                "Red/WiFi (SSID, VLAN, pruebas)",
                "Pruebas (grabación, playback, acceso remoto)",
                "Entrega y capacitación al cliente"
            },
            ServiceOrderType.Preventivo => new List<string>
            {
                "Levantamiento / diagnóstico preventivo",
                "Limpieza de equipo / racks",
                "Revisión cableado / conectores",
                "Revisión energía / tierras / protecciones",
                "Actualización firmware (si aplica)",
                "Pruebas de operación y rendimiento",
                "Recomendaciones"
            },
            _ => new List<string>
            {
                "Diagnóstico",
                "Acción correctiva / reparación",
                "Pruebas de verificación",
                "Validación con cliente",
                "Cierre"
            }
        };
    }
}
