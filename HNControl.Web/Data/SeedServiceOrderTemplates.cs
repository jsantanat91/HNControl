using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Data;

public static class SeedServiceOrderTemplates
{
    public static async Task EnsureAsync(ApplicationDbContext db)
    {
        // Si ya hay algo, no molestamos
        if (await db.ServiceOrderChecklistTemplates.AnyAsync())
            return;

        var templates = new List<ServiceOrderChecklistTemplate>
        {
            Build(ServiceOrderType.NuevaInstalacion, "Instalación CCTV (base)", new []
            {
                ("Levantamiento", true, new []{ "Levantamiento técnico", "Materiales / Accesorios" }),
                ("Cableado", true, new []{ "Ruta de cableado", "Etiquetado y terminaciones", "Canalización / tubería" }),
                ("Equipo", true, new []{ "Cámaras (montaje y enfoque)", "DVR/NVR (configuración)", "Almacenamiento (prueba)" }),
                ("Red", false, new []{ "WiFi / VLAN / SSID (si aplica)", "Acceso remoto (si aplica)" }),
                ("Pruebas", true, new []{ "Grabación / playback", "Validación con cliente", "Entrega y capacitación" }),
            }),
            Build(ServiceOrderType.Preventivo, "Preventivo (base)", new []
            {
                ("Diagnóstico", true, new []{ "Levantamiento / revisión general" }),
                ("Mantenimiento", true, new []{ "Limpieza de equipo / racks", "Revisión de cableado y conectores", "Revisión energía / tierras" }),
                ("Pruebas", true, new []{ "Pruebas de operación", "Recomendaciones" }),
            }),
            Build(ServiceOrderType.Correctivo, "Correctivo (base)", new []
            {
                ("Diagnóstico", true, new []{ "Diagnóstico", "Causa raíz (si aplica)" }),
                ("Ejecución", true, new []{ "Acción correctiva / reparación" }),
                ("Cierre", true, new []{ "Pruebas de verificación", "Validación con cliente" }),
            }),
        };

        db.ServiceOrderChecklistTemplates.AddRange(templates);
        await db.SaveChangesAsync();
    }

    private static ServiceOrderChecklistTemplate Build(
        ServiceOrderType type,
        string name,
        (string category, bool required, string[] items)[] groups)
    {
        var t = new ServiceOrderChecklistTemplate
        {
            Type = type,
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = new List<ServiceOrderChecklistTemplateItem>()
        };

        var order = 1;
        foreach (var g in groups)
        {
            foreach (var title in g.items)
            {
                t.Items.Add(new ServiceOrderChecklistTemplateItem
                {
                    SortOrder = order++,
                    Category = g.category,
                    Title = title,
                    IsRequired = g.required
                });
            }
        }

        return t;
    }
}
