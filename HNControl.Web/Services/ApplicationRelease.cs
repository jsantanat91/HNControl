namespace HNControl.Web.Services;

/// <summary>Versioned release notes shipped with the application; no database changes.</summary>
public static class ApplicationRelease
{
    public const string Version = "2.5.2";
    public static IReadOnlyList<ReleaseNote> History { get; } = Array.AsReadOnly(new[]
    {
        new ReleaseNote("2.5.2", new DateOnly(2026, 10, 22), "Una operación más ordenada", new[]
        {
            new ReleaseChange("Navegación", "Documentos, Monitoreo, Llamadas y Exámenes suspendidos y ocultos. Se conservan su código y datos; el monitoreo automático queda detenido."),
            new ReleaseChange("Empleados", "Vacaciones e incidencias se administran desde Empleados. Barra de acciones alineada y tarjetas con acentos de color."),
            new ReleaseChange("Operación", "Factibilidad disponible en un panel dentro de Órdenes de servicio, conservando los permisos existentes."),
            new ReleaseChange("Carriers", "Eliminación de servicios y de carriers sin servicios ligados. Los contratos se conservan y los monitores del servicio eliminado se detienen."),
            new ReleaseChange("Diseño", "Pestañas de contenido con una presentación uniforme y adaptable a pantallas pequeñas."),
            new ReleaseChange("Publicación", "Esta versión no requiere migraciones de base de datos. Los contratos, NDA, actas de entrega y firmas permanecen disponibles.")
        }),
        new ReleaseNote("2.5.1", new DateOnly(2026, 10, 22), "Una nueva vista de tu operación", new[]
        {
            new ReleaseChange("Diseño", "Nueva presentación en tonos azules: navegación clara, tarjetas, tablas y formularios con un estilo uniforme."),
            new ReleaseChange("Diseño", "Pantalla de acceso renovada y mejor distribución en computadoras, tabletas y teléfonos."),
            new ReleaseChange("Consulta", "Salud del sistema para administradores: conexión a la base de datos, recursos del proceso y lectura del almacenamiento."),
            new ReleaseChange("Versiones", "Versión instalada e historial de notas de actualización disponibles desde Sistema."),
            new ReleaseChange("Continuidad", "Se conservan las funciones, permisos, formularios y procesos existentes. Esta versión no requiere migraciones de base de datos.")
        })
    });
    public record ReleaseChange(string Type, string Description);
    public record ReleaseNote(string Version, DateOnly Date, string Title, IReadOnlyList<ReleaseChange> Changes);
}
