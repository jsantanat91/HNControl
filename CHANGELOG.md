# HN Control — notas de versión

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
