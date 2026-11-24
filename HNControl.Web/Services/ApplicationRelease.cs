namespace HNControl.Web.Services;

/// <summary>Versioned release notes shipped with the application; no database changes.</summary>
public static class ApplicationRelease
{
    public const string Version = "2.5.33";
    public static IReadOnlyList<ReleaseNote> History { get; } = Array.AsReadOnly(new[]
    {
        new ReleaseNote("2.5.33", new DateOnly(2026, 11, 24), "Consulta de clientes para el levantamiento técnico", new[]
        {
            new ReleaseChange("Integración", "API interna para el formulario de levantamiento: con RFC o número de cliente y un correo registrado (principal, facturación o de un contacto) precarga empresa, contacto, teléfono, ciudad, dirección y giro."),
            new ReleaseChange("Seguridad", "Clave propia, distinta de Support, y límite de 60 consultas por minuto. Solo clientes activos; no exporta datos fiscales, representante legal, otros correos ni ids internos. Un RFC o correo que no coincide no revela si el cliente existe."),
            new ReleaseChange("Despliegue", "Configurar HN_LEVANTAMIENTO_INTEGRATION_KEY (32+ caracteres) y desplegar la web. Sin SQL ni APK nueva; sin clave la consulta queda deshabilitada.")
        }),
        new ReleaseNote("2.5.32", new DateOnly(2026, 11, 20), "Contacto de firma y copias por documento", new[]
        {
            new ReleaseChange("Clientes", "Selecciona un contacto principal y contactos en copia al crear servicios contratados, contratos/NDA y órdenes. Las órdenes y documentos pueden heredar la selección del servicio contratado."),
            new ReleaseChange("Correo", "Avisos de órdenes, PDF, bienvenida, invitaciones y confirmaciones respetan la selección guardada y conservan las copias internas configuradas, sin correos duplicados."),
            new ReleaseChange("Firmas", "La invitación muestra el nombre y correo del principal. Los documentos con firmas conservan sus contactos; cambiar destinatarios de una invitación pendiente requiere un nuevo enlace."),
            new ReleaseChange("Despliegue", "Ejecutar Sql/20261120_document_contacts.sql antes de desplegar la web. Tres columnas opcionales; no modifica datos históricos. No requiere APK nueva.")
        }),
        new ReleaseNote("2.5.31", new DateOnly(2026, 11, 19), "Inventario y órdenes conectados con HN Support", new[]
        {
            new ReleaseChange("Integración", "API interna autenticada para consultar servicios, cuentas, cliente, carrier, línea y serie desde Support. No exporta contraseñas, notas privadas ni costos."),
            new ReleaseChange("Órdenes", "Creación manual desde tickets de Support, con validación de cliente/servicio y técnico. Idempotencia por origen y ticket; conserva checklist, flujo técnico y notificaciones persistentes de Control."),
            new ReleaseChange("Operación", "La orden muestra referencia al ticket de Support. Requiere clave compartida y Support 2.3.0. Tabla aditiva SupportOrderLinks al activar; SQL alternativo incluido. Sin nueva APK ni cambios a versiones históricas.")
        }),
        new ReleaseNote("2.5.30", new DateOnly(2026, 11, 19), "Contratos de servicios web mensuales", new[]
        {
            new ReleaseChange("Contratos", "Nueva variante para plataformas y software mensual: nombre, URL, plan, alcance, licencias y horario de soporte. Check-in y GPS tienen sus direcciones precargadas; conserva firmas autógrafas y registro digital."),
            new ReleaseChange("Condiciones", "Mensual sin plazo forzoso, corte el día 1 y pago el 15. Conservación contractual restringida durante un año tras la baja, con exportación y respeto a los derechos de protección de datos."),
            new ReleaseChange("Comunicación", "Bienvenida específica para servicios web, con alcance e importe contratado. El formato no incorpora las condiciones de SLA de Internet."),
            new ReleaseChange("Compatibilidad", "Documentos existentes conservan su versión. Sin SQL ni APK; desplegar la web. Las obligaciones de conservación, exportación y avisos requieren operación de cada plataforma.")
        }),
        new ReleaseNote("2.5.29", new DateOnly(2026, 11, 19), "Contrato simplificado y portal de soporte", new[]
        {
            new ReleaseChange("Contratos", "Ficha con servicio, modalidad, velocidad, ubicación, vigencia y cargos de HN Control. Retira red/reventa, operador, IP y campos técnicos previos a la instalación. Datos opcionales solo aparecen cuando están registrados."),
            new ReleaseChange("Soporte", "Monitoreo e informes mensuales mediante el acceso del cliente en support.hubnet-solutions.net. Conserva WhatsApp, correo, escalamiento y solicitud gratuita de copias o extractos."),
            new ReleaseChange("Condiciones", "Segunda revisión de coherencia: HN responde por el servicio, inicio de cobros desde la prestación efectiva y cambios con consentimiento. Mantiene 99.85% y cuatro horas máximas de primera respuesta humana para Dedicado."),
            new ReleaseChange("Compatibilidad", "Nueva revisión v5 para contratos nuevos; documentos emitidos y firmas conservan su versión. Sin SQL ni APK; desplegar la web.")
        }),
        new ReleaseNote("2.5.28", new DateOnly(2026, 11, 19), "Anexo de Servicios y condiciones de atención", new[]
        {
            new ReleaseChange("Contratos", "Anexo A integrado con capacidad de bajada/subida, modalidad, red propia o de terceros, punto de entrega, medición y referencias regulatorias verificadas. Captura desde el servicio contratado; valida los datos técnicos antes de emitir."),
            new ReleaseChange("Atención", "Dedicado: disponibilidad mensual 99.85% y máximo de primera respuesta humana de cuatro horas, 24/7. La recuperación conserva un objetivo separado. Matriz de escalamiento, acceso a evidencia e informes mensuales previstos en el contrato."),
            new ReleaseChange("Condiciones", "Responsabilidad de HN ante el cliente en reventa y red propia, evidencia controvertible, compensaciones explícitas y ajustes de cancelación y propiedad de equipos. No declara como concedidos títulos pendientes."),
            new ReleaseChange("Compatibilidad", "Conserva plantillas históricas y ambas firmas. Sin SQL ni APK; desplegar la web. Los compromisos de informes requieren operación del NOC; no activa monitoreo ni envíos automáticos.")
        }),
        new ReleaseNote("2.5.27", new DateOnly(2026, 11, 19), "SLA y soporte en contratos recurrentes", new[]
        {
            new ReleaseChange("Contratos", "Nueva revisión del contrato recurrente: disponibilidad mensual de Internet Dedicado del 99.85%, respuesta humana en 15 minutos, diagnóstico en una hora, actualizaciones por hora y objetivo de recuperación en cuatro horas. Best Effort conserva condiciones diferenciadas."),
            new ReleaseChange("Soporte", "WhatsApp NOC 5526014566 como canal preferente, soporte@hubnet-solutions.com.mx y support.hubnet-solutions.net para abrir tickets. Medición, exclusiones justificadas y compensaciones sin espera mínima de 72 horas."),
            new ReleaseChange("Compatibilidad", "Bienvenida alineada con la revisión de cada contrato. Documentos anteriores y firmas conservan su plantilla original. Solo desplegar la web; sin SQL ni nueva APK.")
        }),
        new ReleaseNote("2.5.26", new DateOnly(2026, 11, 18), "Portal de firma de contratos", new[]
        {
            new ReleaseChange("Diseño", "Página pública de contratos y NDA renovada: identidad HN en azul, revisión y descarga del documento, estado de cada firma y formulario adaptable a celular."),
            new ReleaseChange("Firma", "Área de firma con guía visual, indicador de captura y acción para limpiar. Conserva el trazo al redimensionar o girar la pantalla y permite dibujar con mouse, dedo o lápiz digital."),
            new ReleaseChange("Confirmación", "Distingue firma del cliente registrada, firma HN pendiente y documento completado. Mantiene los enlaces, validaciones, documentos y notificaciones existentes; sin SQL ni nueva APK.")
        }),
        new ReleaseNote("2.5.25", new DateOnly(2026, 11, 15), "Corrección de nómina y correo de pago", new[]
        {
            new ReleaseChange("Nómina", "Normaliza las fechas de consulta de bonos y deducciones para PostgreSQL, conservando los días de la quincena. Corrige los ajustes mostrados en cero en el panel y el error al construir el recibo."),
            new ReleaseChange("Confirmación de pago", "Corrige las fechas al registrar deducciones y bonos aplicados. Conserva los importes del recibo después del pago y evita duplicar aplicaciones al repetir la confirmación."),
            new ReleaseChange("Validación", "Pruebas con PostgreSQL para panel, Excel, recibo, correo simulado y reintentos. No requiere SQL ni nueva APK; desplegar la web.")
        }),
        new ReleaseNote("2.5.24", new DateOnly(2026, 11, 9), "Panel, órdenes y viáticos", new[]
        {
            new ReleaseChange("Panel", "Tarjetas de indicadores y nómina alineadas con Empleados, acciones más claras y gráficas en tonos azules con estados vacíos. Exportación de nómina del periodo seleccionado."),
            new ReleaseChange("Empleados activos", "Las bajas quedan fuera del panel, KPI, comparativas y Excel de nómina. No permiten nuevas evaluaciones ni asignaciones de órdenes; se conservan perfiles, evaluaciones y asignaciones históricas."),
            new ReleaseChange("Órdenes de servicio", "Tarjetas con estado y responsables, ficha en modal y pestañas de resumen, entrega y firmas, y PDF y correo. Conserva formularios, validación, evidencia y destinatarios."),
            new ReleaseChange("Viáticos", "Resumen por empleado, tarjetas semanales y revisión en modal. Formularios y comprobantes con diseño uniforme en administración y portal del empleado."),
            new ReleaseChange("Compatibilidad", "Conserva reglas de cálculo e históricos. Solo requiere desplegar la web; sin SQL adicional ni nueva APK.")
        }),
        new ReleaseNote("2.5.23", new DateOnly(2026, 11, 9), "Clientes y KPI 80/20", new[]
        {
            new ReleaseChange("Clientes", "Tarjetas alineadas con Empleados, acciones secundarias agrupadas y búsqueda por nombre, número, RFC o correo. Ficha en modal con contratos, datos fiscales, contactos, actividad y accesos en pestañas."),
            new ReleaseChange("KPI 80/20", "Panel de avance, filtros de evaluación y estado, formulario con escala visual y estimación inmediata. Cambiar empleado o quincena carga su propia evaluación; aviso al descartar cambios."),
            new ReleaseChange("Correcciones", "Vista previa alineada con la fórmula existente, notas opcionales y enlace correcto al Excel de nómina. La estimación del panel distingue el 80/20 de los bonos y deducciones."),
            new ReleaseChange("Compatibilidad", "Mantiene permisos, contratos y reglas de cálculo. Sin SQL adicional ni actualización de APK.")
        }),
        new ReleaseNote("2.5.22", new DateOnly(2026, 11, 9), "Ficha de empleado en modal", new[]
        {
            new ReleaseChange("Empleados", "Ver ficha abre un modal desde tarjetas y tabla. Cabecera de identidad, resumen de nómina, datos personales y laborales, desempeño y viáticos organizados en pestañas."),
            new ReleaseChange("Navegación", "Acciones de editar, calificar y consultar ajustes dentro del modal. Cerrar conserva el listado; si se guardaron cambios, las tarjetas se actualizan. Gráficas con estados vacíos y variable expresada como porcentaje."),
            new ReleaseChange("Compatibilidad", "Conserva cálculos y permisos. Sin migración SQL ni nueva APK.")
        }),
        new ReleaseNote("2.5.21", new DateOnly(2026, 11, 9), "Tarjetas de empleados", new[]
        {
            new ReleaseChange("Diseño", "Tarjetas alineadas al panel: identidad y estado, sueldo mensual y comisión separados, acceso a ficha y acciones ordenadas. Adaptación a móvil y nombres largos."),
            new ReleaseChange("Compatibilidad", "Cambio visual; conserva permisos y acciones. Sin SQL adicional ni actualización de APK.")
        }),
        new ReleaseNote("2.5.20", new DateOnly(2026, 11, 9), "Almacén, cotizador público y comisiones", new[]
        {
            new ReleaseChange("Inventario físico", "Almacén, Admin stock y Aprobaciones con navegación unificada. Retirado el módulo antiguo de servicios; se conservan sus datos. Entrada/salida con artículo preseleccionado."),
            new ReleaseChange("Ventas", "Cotizador público sin sesión en tres pasos, búsqueda y resumen. Precios del catálogo calculados en servidor; descuentos y conceptos libres reservados al personal autorizado."),
            new ReleaseChange("Empleados", "Ficha organizada por secciones y tarjetas con comisión por orden. Activación e importe individual; bono único en la quincena de México al finalizar la orden, para el técnico que la tomó o el asignado."),
            new ReleaseChange("Dashboard", "Retirados contadores y gráfica del antiguo sistema de tickets. Acceso al portal externo de soporte."),
            new ReleaseChange("Publicación", "Aplicar 20261109_order_commissions.sql antes del despliegue. Sin cambio de APK; las comisiones se calculan en el servidor. No se generan comisiones retroactivas.")
        }),
        new ReleaseNote("2.5.19", new DateOnly(2026, 11, 9), "Corrección de despliegue Docker", new[]
        {
            new ReleaseChange("Publicación", "El modelo compartido de entregas queda incluido dentro del contexto de compilación de la web. Corrige el error de publicación en Docker."),
            new ReleaseChange("Compatibilidad", "Conserva la APK 2.3.0 y el SQL de entrega unificada ya aplicado; no requiere otra migración.")
        }),
        new ReleaseNote("2.5.18", new DateOnly(2026, 11, 9), "Entrega unificada en órdenes", new[]
        {
            new ReleaseChange("Órdenes", "Servicio, hardware o ambos en una sola orden. Marca y serie de router, equipos, cantidades, accesorios, estado y modalidad en el PDF con firmas y huellas."),
            new ReleaseChange("Captura", "Entrega y equipos disponible en web y APK 2.3.0. Valida datos, series y versión de entrega antes de firmar; la captura queda bloqueada al firmar o enviar a revisión."),
            new ReleaseChange("Histórico", "Nuevas entregas dentro de Órdenes. Los formatos anteriores se conservan para consulta desde Entregas anteriores."),
            new ReleaseChange("Cuentas móviles", "Empleados y administradores consultan cuentas de proveedores y copian contraseña mediante una acción independiente; los listados no incluyen secretos."),
            new ReleaseChange("Publicación", "Aplicar 20261109_order_delivery.sql antes de desplegar la web y después instalar APK 2.3.0.")
        }),
        new ReleaseNote("2.5.17", new DateOnly(2026, 11, 9), "NOC y envío de órdenes de servicio", new[]
        {
            new ReleaseChange("Órdenes", "PDF con canales del NOC: WhatsApp preferente y llamadas al 55 2601 4566, correo de soporte y portal de tickets; conserva firmas y huellas digitales."),
            new ReleaseChange("Correo", "El botón Enviar PDF por correo envía al cliente y copia a los destinatarios configurados de Ingeniería. Muestra los destinatarios y adjunta el PDF actualizado."),
            new ReleaseChange("Publicación", "Solo requiere desplegar la web. Sin migración SQL ni actualización de APK.")
        }),
        new ReleaseNote("2.5.16", new DateOnly(2026, 11, 9), "Ficha completa de servicios en la APK", new[]
        {
            new ReleaseChange("Servicios", "Cuenta vinculada, correo, número de cuenta y línea, costo, plazo y notas disponibles en la consulta móvil."),
            new ReleaseChange("Móvil 2.2.0", "Detalle con tarjetas y badges, copia de referencias y acceso al marcador o soporte online."),
            new ReleaseChange("Acceso", "La contraseña de la cuenta se copia únicamente con permisos de administrador, mediante una acción independiente."),
            new ReleaseChange("Publicación", "No requiere migraciones nuevas. Actualizar la API y la APK para ver los campos adicionales.")
        }),
        new ReleaseNote("2.5.15", new DateOnly(2026, 11, 9), "Servicios móviles y firmas autógrafas", new[]
        {
            new ReleaseChange("Móvil", "API de consulta de Inventario Servicios para empleados y administradores, con filtros por cliente y proveedor. APK 2.1.0 con nuevo acceso visual."),
            new ReleaseChange("Contratos", "Firma dibujada de cliente y HN dentro del contrato, además del registro digital y hash. HN puede completar su trazo usando la cuenta que firmó originalmente."),
            new ReleaseChange("Publicación", "Requiere la migración AddContractProviderAutograph antes de usar la nueva versión.")
        }),
        new ReleaseNote("2.5.14", new DateOnly(2026, 11, 8), "Planes y cuentas del inventario", new[]
        {
            new ReleaseChange("Planes", "Nombre del plan seleccionable según el proveedor, con las opciones de Starlink e Internet 50 a 1000. Tipo Empresarial disponible también para los demás proveedores."),
            new ReleaseChange("Equipos", "Número de kit reservado a Starlink; los demás proveedores muestran el número de serie."),
            new ReleaseChange("Cuentas", "Acceso directo para registrar una cuenta y actualizar el selector sin perder los datos del servicio."),
            new ReleaseChange("Publicación", "No requiere migración SQL adicional.")
        }),
        new ReleaseNote("2.5.13", new DateOnly(2026, 11, 8), "Inventario organizado por proveedor", new[]
        {
            new ReleaseChange("Inventario Servicios", "Selección visual con logotipos y tipos de plan específicos para Starlink y los demás proveedores."),
            new ReleaseChange("Conexión", "Tecnología separada del plan: fibra óptica, satelital para Starlink, microonda u otra conexión. Datos de red desplegables mediante una casilla."),
            new ReleaseChange("Captura", "Soporte de Starlink fijo en Online y vencimiento calculado automáticamente desde el inicio y los meses contratados."),
            new ReleaseChange("Navegación", "El acceso al módulo Carriers anterior se retira del menú; sus registros se conservan."),
            new ReleaseChange("Publicación", "No requiere una migración adicional a la de Inventario Servicios de la versión 2.5.12.")
        }),
        new ReleaseNote("2.5.12", new DateOnly(2026, 11, 8), "Inventario de servicios y formatos corporativos", new[]
        {
            new ReleaseChange("Inventario Servicios", "Módulo independiente para cuentas de carriers, antenas, servicios, costos, datos de red y asignación a clientes."),
            new ReleaseChange("Accesos", "Contraseñas cifradas, capturadas una sola vez y disponibles para copiar por administradores."),
            new ReleaseChange("Documentos", "Contrato eventual y acta de entrega con secciones, texto justificado y espacios de firma; se conserva la plantilla de documentos ya preparados."),
            new ReleaseChange("Soporte", "El acta destaca WhatsApp 55 2601 4566 como canal preferente del NOC, además del correo y portal de soporte."),
            new ReleaseChange("Clientes", "Los documentos sin firmas se pueden eliminar desde Contrato / NDA; su enlace público queda desactivado y el servicio contratado se conserva."),
            new ReleaseChange("Publicación", "Requiere la migración de las nuevas tablas de Inventario Servicios. El módulo Carriers conserva sus datos.")
        }),
        new ReleaseNote("2.5.11", new DateOnly(2026, 11, 6), "Carátula y firmas del contrato de servicios", new[]
        {
            new ReleaseChange("Contratos", "Carátula organizada por cliente, servicio, tarifas y documentación requerida, con una casilla por documento."),
            new ReleaseChange("Firmas", "El clausulado justificado termina con dos espacios para firma manuscrita y sus respectivos registros de firma electrónica.")
        }),
        new ReleaseNote("2.5.10", new DateOnly(2026, 11, 6), "Vencimientos y renovación de contratos", new[]
        {
            new ReleaseChange("Clientes", "Aviso de vencimiento con tres meses de anticipación y registro manual de la renovación acordada."),
            new ReleaseChange("Portal", "El cliente puede consultar el vencimiento y solicitar la renovación de su servicio."),
            new ReleaseChange("Documentos", "Nueva presentación del contrato marco de servicios, con carátula, firmas y 26 cláusulas en formato de lectura continua."),
            new ReleaseChange("Facturación", "El contrato y la bienvenida recurrente indican corte el día 1, primera factura contra entrega y pagos posteriores el día 15 del mismo mes.")
        }),
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
