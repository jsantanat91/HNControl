using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Data;

public static class SeedServiceOrderTemplates
{
    public static async Task EnsureAsync(ApplicationDbContext db)
    {
        var templates = new List<ServiceOrderChecklistTemplate>
        {
            Build(ServiceOrderType.NuevaInstalacion, "Instalacion CCTV (base)", new []
            {
                ("Levantamiento", true, new []{ "Levantamiento tecnico", "Materiales / Accesorios" }),
                ("Cableado", true, new []{ "Ruta de cableado", "Etiquetado y terminaciones", "Canalizacion / tuberia" }),
                ("Equipo", true, new []{ "Camaras (montaje y enfoque)", "DVR/NVR (configuracion)", "Almacenamiento (prueba)" }),
                ("Red", false, new []{ "WiFi / VLAN / SSID (si aplica)", "Acceso remoto (si aplica)" }),
                ("Pruebas", true, new []{ "Grabacion / playback", "Validacion con cliente", "Entrega y capacitacion" }),
            }),
            Build(ServiceOrderType.Preventivo, "Preventivo (base)", new []
            {
                ("Diagnostico", true, new []{ "Levantamiento / revision general" }),
                ("Mantenimiento", true, new []{ "Limpieza de equipo / racks", "Revision de cableado y conectores", "Revision energia / tierras" }),
                ("Pruebas", true, new []{ "Pruebas de operacion", "Recomendaciones" }),
            }),
            Build(ServiceOrderType.LevantamientoTecnico, "Levantamiento tecnico (base)", new []
            {
                ("Levantamiento", true, new []{ "Visita y revision inicial", "Levantamiento fotografico", "Levantamiento de infraestructura" }),
                ("Materiales", true, new []{ "Materiales requeridos", "Herramientas y consumibles", "Estimacion de cantidades" }),
                ("Entrega", true, new []{ "Resumen tecnico", "Riesgos y recomendaciones", "Aprobacion de alcance" }),
            }),
            Build(ServiceOrderType.Correctivo, "Correctivo (base)", new []
            {
                ("Diagnostico", true, new []{ "Diagnostico", "Causa raiz (si aplica)" }),
                ("Ejecucion", true, new []{ "Accion correctiva / reparacion" }),
                ("Cierre", true, new []{ "Pruebas de verificacion", "Validacion con cliente" }),
            }),
        };

        var existing = await db.ServiceOrderChecklistTemplates
            .AsNoTracking()
            .Select(t => new { t.Type, t.Name })
            .ToListAsync();

        foreach (var template in templates)
        {
            var exists = existing.Any(x => x.Type == template.Type && x.Name == template.Name);
            if (!exists)
                db.ServiceOrderChecklistTemplates.Add(template);
        }

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
