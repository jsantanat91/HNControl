namespace HNControl.Web.Services;

/// <summary>Versioned release notes shipped with the application; no database changes.</summary>
public static class ApplicationRelease
{
    public const string Version = "2.5.4";
    public static IReadOnlyList<ReleaseNote> History { get; } = Array.AsReadOnly(new[]
    {
        new ReleaseNote("2.5.4", new DateOnly(2026, 10, 26), "Contacto de soporte y estado de órdenes", new[]
        {
            new ReleaseChange("Acta de entrega", "Contacto de soporte actualizado a Llamada y WhatsApp 5526014566 y soporte@hubnet-solutions.com.mx."),
            new ReleaseChange("Órdenes", "La etiqueta Finalizada se distingue en verde en los listados administrativos y de empleados.")
        }),
        new ReleaseNote("2.5.3", new DateOnly(2026, 10, 23), "Avisos de órdenes de servicio", new[]
        {
            new ReleaseChange("Configuración", "Lista editable de correos de Ingeniería, independiente de los destinatarios de documentos de clientes."),
            new ReleaseChange("Órdenes", "Al crear una orden se notifica a Ingeniería y al cliente con el diseño del correo de bienvenida y la fecha de atención registrada."),
            new ReleaseChange("Seguimiento", "Se conservan las fechas originales del aviso y se pueden reintentar los envíos pendientes desde el detalle administrativo de la orden."),
            new ReleaseChange("Continuidad", "Un fallo de correo no impide guardar la orden. Los avisos de creación también aplican a órdenes generadas desde factibilidad y tickets."),
            new ReleaseChange("Tickets", "Corregida la creación de órdenes consecutivas desde tickets y la compatibilidad de su fecha límite con PostgreSQL."),
            new ReleaseChange("Publicación", "Requiere la migración de destinatarios de Ingeniería. Incluye un script SQL para ejecutarla como propietario de la base antes del despliegue.")
        }),
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
