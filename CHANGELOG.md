# HN Control — notas de versión

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
