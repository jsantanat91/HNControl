# HN Control — notas de versión

## 2.5.12 · 8 de noviembre de 2026

- Nuevo módulo administrativo **Inventario Servicios**, independiente del módulo Carriers y de sus registros actuales. Cuentas compartidas entre servicios, asignación a clientes, equipos, ubicaciones, costos y datos de red.
- Contraseñas de cuentas cifradas, capturadas una vez y accesibles mediante un botón de copia autorizado. Catálogo de carriers con logotipos y modelos de antenas Starlink.
- Contrato eventual y acta de entrega alineados al contrato de servicios aprobado: secciones delimitadas, texto justificado y bloques finales para ambas firmas con sus datos y huellas digitales reales.
- El acta incluye WhatsApp y llamada al 55 2601 4566, correo soporte@hubnet-solutions.com.mx y portal https://support.hubnet-solutions.net. WhatsApp se identifica como canal preferente del NOC.
- Los documentos ya preparados para firma conservan la revisión de su plantilla. El diseño actualizado se aplica a nuevos documentos y a la regeneración de documentos aún sin firmas.
- En **Clientes → Contrato / NDA** se pueden eliminar documentos sin firmas y desactivar sus enlaces públicos. Se protege cualquier documento firmado y se conserva el servicio contratado. La eliminación existente de servicios ahora desvincula sus registros dentro de una transacción.

Requiere la migración `20261108000100_AddServiceInventory`. Se incluye `HNControl.Web/Sql/20261108_service_inventory.sql` para aplicación manual desde pgAdmin.

## 2.5.11 · 6 de noviembre de 2026

- Contrato de servicios con texto justificado, carátula por secciones y documentación requerida en casillas individuales.
- Bloques de cliente y proveedor al final del clausulado con espacio para firma manuscrita, representante y registro electrónico. Los documentos ya preparados para firma conservan su plantilla.

## 2.5.10 · 6 de noviembre de 2026

- Aviso de contratos por vencer en tres meses en Clientes, en la ficha individual y en el portal del cliente.
- Registro de renovaciones acordadas desde Clientes y solicitud desde el portal. Se excluyen los contratos eventuales, de cargo único e indefinidos.
- Contrato marco de servicios con carátula corporativa, ambas firmas y sus 26 cláusulas. Los documentos preparados para firma con la plantilla original conservan esa versión.
- La carátula, la cláusula sexta del nuevo contrato y la bienvenida recurrente indican corte el día 1, primera factura contra entrega y vencimiento de los pagos posteriores el día 15 del mismo mes.

## 2.5.9 · 6 de noviembre de 2026

- PDF de orden de servicio en formato carta con folio, cliente, datos de atención y término estimado en lugar de SLA. El servicio y su capacidad se obtienen del contrato vinculado. Los contratos eventuales muestran el periodo del evento.
- Lista de verificación con encabezados por página, evidencias y firmas del técnico y del cliente. Cada captura disponible lleva una huella SHA-256 que incorpora la orden, el rol, el nombre, la fecha y los bytes de la firma. El pie incluye una huella separada de los datos de la orden.
- Para aplicar el diseño a un PDF anterior, usa **Generar PDF** en el detalle administrativo de la orden.

## 2.5.8 · 6 de noviembre de 2026

- Contrato eventual rediseñado con logotipo incrustado, tipografía uniforme y tablas para cliente, servicio, vigencia e importes.
- Condiciones en una sección independiente, encabezado y paginación en todas las hojas. Ambas firmas permanecen en una fila indivisible, sin alturas fijas.
- Se conservan las ocho cláusulas, los campos de sustitución y los datos congelados para firma. Los contratos recurrentes, NDA y actas conservan sus plantillas.

No requiere migración SQL. Vista previa de dos páginas revisada con exportación de Word; LibreOffice no está instalado en el equipo local. Validada la generación con las pruebas existentes del proyecto.

## 2.5.7 · 4 de noviembre de 2026

- Corregido el guardado de órdenes que mostraba «The Title field is required» aun teniendo título: el formulario de actividades ya no genera errores sin prefijo que bloqueen el formulario principal.
- Validación independiente para editar la orden y agregar actividades, conservando los errores de conversión de fechas y tipos. Los títulos siguen siendo obligatorios, con mensajes en español junto a su campo.
- Pruebas de regresión con el enlace de modelos y la validación reales de ASP.NET Core, incluyendo edición de órdenes globales y conservación de actividades.

No requiere migración SQL.

## 2.5.6 · 4 de noviembre de 2026

- Formularios de alta y edición de clientes, contratos, órdenes y proyectos organizados por secciones. Observaciones, archivos y detalles opcionales se despliegan cuando se necesitan; los datos existentes y errores permanecen visibles.
- Ayudas para reutilizar correo de contacto, nombre de contacto como representante y domicilio del cliente; los campos capturados no se sobrescriben. Los paquetes pueden completar nombre, importe y tipo de servicio.
- Ficha del cliente con contratos más compactos, referencias técnicas desplegables y menos datos vacíos. Acceso para crear una orden desde el contrato o desde el proyecto con sus relaciones precargadas.
- Órdenes: proyecto y contrato se renderizan desde el servidor y se conservan al corregir errores. Validación de pertenencia al cliente y de fechas, sin descartar errores de captura al editar ni desplazar los días programados por la zona horaria.
- Proyectos: selección de contratos con casillas, búsqueda y contador. Al cambiar cliente se limpian las opciones anteriores; la carga muestra errores y permite reintentar. Se comprueba que los contratos pertenezcan al cliente y se desvinculan los del cliente anterior si cambia el cliente del proyecto.
- Selectores dependientes protegidos contra respuestas atrasadas. Se espera a completar la carga antes de guardar.

No requiere migración SQL. Verificado con pruebas automáticas y datos locales de demostración; no se enviaron correos reales ni se modificaron datos de producción.

## 2.5.5 · 4 de noviembre de 2026

- Modalidad **Eventual** al crear y editar servicios del cliente. Fechas obligatorias, inclusivas, de un día hasta un mes calendario; importe único mayor a cero.
- Plantilla independiente `Contrato_Eventual.docx` para Internet/WiFi temporal: sede, periodo, alcance, importe, instalación, cierre y ambas firmas. No hereda permanencia ni penalización del contrato mensual.
- Bienvenida de eventos con el contrato seleccionado y su información congelada para firma, coordinación, pago único y soporte; conserva destinatarios y copias configurados.
- Ficha del cliente y PDF interno muestran periodo e importe único. Los planes de facturación ligados a eventuales se fuerzan a cobro único al crear o editar.

No requiere migración SQL: utiliza la recurrencia y fechas existentes del contrato. Los contratos anteriores conservan su modalidad. Se recomienda revisión jurídica de la nueva plantilla antes de usarla con clientes; anticipos, horarios y acuerdos específicos deben documentarse en las notas y cotización aceptada. La comprobación automática cubre sustitución de datos y firmas; la revisión visual PDF queda pendiente del conversor LibreOffice.

## 2.5.4 · 26 de octubre de 2026

- Acta de entrega: contacto de soporte actualizado en el encabezado y en la sección de soporte a **Llamada y WhatsApp 5526014566** y **soporte@hubnet-solutions.com.mx**.
- Órdenes de servicio: etiqueta **Finalizada** en verde en los listados de administración y empleados.

Cambios de presentación y plantilla. No requiere migraciones ni SQL adicional.

## 2.5.3 · 23 de octubre de 2026

- Nueva sección **Configuración → General → Correos de Ingeniería para órdenes de servicio**, con los destinatarios iniciales `jsantana@hubnet-solutions.net` y `dtaboada@hubnet-solutions.net`. Permite agregar, editar o eliminar direcciones; una lista vacía desactiva solo estos avisos internos.
- Al guardar una orden se envían mensajes separados a Ingeniería y al cliente. Conservan el diseño del correo de bienvenida; el cliente recibe su referencia, servicio y fecha programada, sin notas internas ni vínculos administrativos.
- Aplica al alta directa, a la conversión de factibilidad y a órdenes generadas desde tickets. Las fechas automáticas de factibilidad y la fecha límite de un ticket no se anuncian como una visita confirmada.
- Corregida la creación de órdenes desde tickets: cada orden recibe su identificador público único y su fecha límite se normaliza para PostgreSQL.
- La auditoría conserva las fechas iniciales del aviso, aunque el flujo operativo modifique posteriormente el inicio de la orden. Un error SMTP conserva la orden y permite reintentar desde su detalle administrativo solo los canales pendientes. No se envían avisos retroactivos a órdenes históricas.
- Los correos de contratos, NDA, actas, bienvenida y PDF de órdenes mantienen su flujo existente.

**Antes de desplegar:** requiere la migración `20261023000100_AddEngineeringNotificationEmails`. Dado el antecedente de permisos del usuario de aplicación, ejecutar completo `HNControl.Web/Sql/20261023_engineering_notification_emails.sql` en pgAdmin, en la base de HN Control y con el propietario de las tablas o `postgres`; después desplegar y reiniciar. El script agrega únicamente la columna de destinatarios y registra esta migración dentro de la misma transacción. Es idempotente y no cambia propietarios ni permisos. Si la aplicación tiene permisos de migración, puede aplicar la migración EF al iniciar.

Los envíos utilizan la configuración SMTP existente. La confirmación de envío significa aceptación por SMTP; una interrupción del proceso entre esa aceptación y el registro en base de datos puede requerir conciliación manual antes de reintentar.

## 2.5.2 · 22 de octubre de 2026

- Documentos (biblioteca), Monitoreo de carriers, Llamadas de ventas y Exámenes quedan suspendidos: se ocultan sus accesos, se bloquean rutas y API y no se inicia el monitoreo automático. Se conserva el código y la información.
- Carriers y el resto de Ventas continúan disponibles. Contratos de clientes, NDA, actas de entrega, firmas y correos conservan su funcionamiento.
- Vacaciones e incidencias se abren en un panel desde Empleados; Factibilidad desde Órdenes de servicio. Sus accesos independientes se retiran del menú principal; se mantienen permisos y formularios.
- Barra de acciones de Empleados alineada, tarjetas con acentos de color y pestañas de contenido uniformes.
- Botones para eliminar servicios de carriers y carriers sin servicios ligados, con confirmación. Al eliminar un servicio se elimina su bitácora, se conservan cliente y contrato, y sus monitores quedan inactivos y desvinculados conservando su historial. No se permite eliminar un carrier con servicios ligados.

No requiere migraciones ni ejecutar SQL. Para reactivar módulos en un despliegue futuro, establecer en configuración `Features:DocumentsEnabled`, `Features:MonitoringEnabled`, `Features:ExamsEnabled` o `Features:SalesCallsEnabled` en `true` y reiniciar la aplicación. Los cuatro valores son `false` por defecto; para variables de entorno usar `Features__DocumentsEnabled`, etc. La reactivación del monitoreo requiere reinicio para registrar su proceso automático.

## 2.5.1 · 22 de octubre de 2026

- Presentación renovada en tonos azules, inspirada en DVR Viewer: acceso dividido, navegación clara, tarjetas, tablas y formularios uniformes.
- Panel ejecutivo con una nueva cabecera visual; conserva sus indicadores, filtros, acciones y procesos.
- Adaptación de la presentación a computadoras, tabletas y teléfonos.
- Nueva consulta administrativa en **Sistema → Salud y versiones**: conexión a la base de datos, tiempo y memoria del proceso, espacio disponible y presencia de plantillas.
- Versión instalada y notas de actualización dentro de la aplicación.

Esta entrega no agrega migraciones ni cambia reglas de negocio, permisos existentes, firmas, correos o nómina. La consulta de salud no escribe archivos ni registros y no prueba integraciones externas.

### Publicación de futuras versiones

Actualizar `Version` en `HNControl.Web.csproj` y `ApplicationRelease.Version`; agregar la nueva entrada al principio de `ApplicationRelease.History`, conservando las versiones anteriores con su número literal. Registrar aquí los cambios correspondientes. Las notas se publican con el mismo despliegue de la aplicación; no existe un actualizador automático.
