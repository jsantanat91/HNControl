namespace HNControl.Web.Services;

/// <summary>Versioned release notes shipped with the application; no database changes.</summary>
public static class ApplicationRelease
{
    public const string Version = "2.5.1";
    public static IReadOnlyList<ReleaseNote> History { get; } = Array.AsReadOnly(new[]
    {
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
