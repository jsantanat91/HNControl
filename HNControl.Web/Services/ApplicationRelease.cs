namespace HNControl.Web.Services;

/// <summary>Versioned release notes shipped with the application; no database changes.</summary>
public static class ApplicationRelease
{
    public const string Version = "2.5.9";
    public static IReadOnlyList<ReleaseNote> History { get; } = Array.AsReadOnly(new[]
    {
        new ReleaseNote("2.5.9", new DateOnly(2026, 11, 6), "Orden de servicio y huellas de firma", new[]
        {
            new ReleaseChange("PDF", "Formato corporativo con folio de la orden, término estimado, tablas de verificación y paginación."),
            new ReleaseChange("Contratos", "La orden muestra el servicio y la capacidad del contrato vinculado, así como la sucursal y el periodo de los eventos cuando están registrados."),
            new ReleaseChange("Firmas", "El PDF muestra las capturas del técnico y del cliente con sus huellas SHA-256 y una huella de los datos de la orden.")
        }),
        new ReleaseNote("2.5.8", new DateOnly(2026, 11, 6), "Contrato de eventos con diseño corporativo", new[]
        {
            new ReleaseChange("Documentos", "Contrato eventual con logotipo de HN Solutions, datos del cliente y servicio organizados en tablas, importes separados y numeración de páginas."),
            new ReleaseChange("Firmas", "Condiciones y firmas en una sección independiente, con ambas firmas juntas y espacio adaptable para sus datos."),
            new ReleaseChange("Publicación", "Cambio de presentación de la plantilla. Esta versión no requiere migraciones de base de datos.")
        }),
        new ReleaseNote("2.5.7", new DateOnly(2026, 11, 4), "Corrección al editar órdenes de servicio", new[]
        {
            new ReleaseChange("Órdenes", "Guardar una orden con título ya no exige completar el formulario independiente de actividades."),
            new ReleaseChange("Validación", "Orden y actividades conservan sus propias validaciones y errores de captura. Los avisos de título obligatorio se muestran en español junto al campo correspondiente."),
            new ReleaseChange("Publicación", "Esta versión no requiere migraciones de base de datos.")
        }),
        new ReleaseNote("2.5.6", new DateOnly(2026, 11, 4), "Formularios conectados y más claros", new[]
        {
            new ReleaseChange("Diseño", "Altas y ediciones de clientes, contratos, órdenes y proyectos organizadas por secciones, con detalles opcionales desplegables y controles adaptables a móvil."),
            new ReleaseChange("Clientes", "Fichas de contratos más compactas y ayudas para reutilizar correo de contacto, representante, domicilio y datos de paquetes sin sobrescribir campos capturados."),
            new ReleaseChange("Órdenes", "Creación desde un contrato o proyecto con datos vinculados; las selecciones se conservan cuando hay errores y se valida que pertenezcan al mismo cliente."),
            new ReleaseChange("Proyectos", "Contratos seleccionables con casillas y búsqueda. Carga de datos con mensajes de error y reintento, sin conservar opciones de otro cliente."),
            new ReleaseChange("Publicación", "Esta versión no requiere migraciones de base de datos.")
        }),
        new ReleaseNote("2.5.5", new DateOnly(2026, 11, 4), "Conectividad para eventos", new[]
        {
            new ReleaseChange("Clientes", "Contratos eventuales de un día hasta un mes con fechas inclusivas e importe único."),
            new ReleaseChange("Documentos", "Plantilla independiente de contrato temporal y bienvenida específica para eventos, conservando las firmas y copias configuradas."),
            new ReleaseChange("Facturación", "Los planes vinculados a contratos eventuales se registran como cobro único.")
        }),
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
