# Integración con HN Support 2.3.0

Web **2.5.31** continúa la secuencia 2.5.30. La aplicación móvil conserva su versión; no hay nueva APK. No se alteran plantillas históricas, firmas, contratos o movimientos de versiones anteriores.

## Activación

1. Respaldo de base y despliegue de Control 2.5.31.
2. Generar una clave aleatoria de al menos 32 caracteres (recomendado 32 bytes hex). Guardarla fuera de Git.
3. Configurar la misma variable de stack `HN_SUPPORT_INTEGRATION_KEY` en los compose de Control y Support. En Control se mapea a `SupportIntegration__ApiKey`; en Support a `HN_CONTROL_API_KEY`.
4. Mantener `SupportIntegration__SourceId=hn-support` estable. Es el ámbito único de las órdenes; no reutilizarlo con otra base de tickets.
5. En Support: `HN_CONTROL_BASE_URL=https://control.hubnet-solutions.net`. Solo comunicación backend HTTPS, sin nuevos puertos publicados.
6. Redeploy de ambos. Control crea `SupportOrderLinks` al iniciar si hay clave configurada. Si el usuario SQL no tiene permisos DDL, aplicar antes `HNControl.Web/Sql/20261119_support_integration.sql` como propietario y conceder SELECT/INSERT al usuario de la aplicación. Support inicializa su tabla de vínculos automáticamente.
7. En Support, un superadministrador consulta Servicios activos y vincula monitores manualmente. Otorgar SERVICES_VIEW / SERVICES_MANAGE / SERVICE_ORDERS_CREATE según el rol; las órdenes además requieren TICKETS_VIEW.

Sin configurar la clave no se habilita la API. No usar contraseña de administrador, token móvil o AUTH_SECRET como clave. Para rotación, reemplazar el secreto en ambos stacks y reiniciar; habrá un breve intervalo de conexión no disponible. Puede restringirse el endpoint a la IP de salida de Support en el proxy. No registrar el header secreto.

## API y alcance

Prefijo `/api/integrations/support`, header `X-HN-Support-Key`:

- GET services: q, activeOnly (true), page (1), pageSize (20; máximo 100).
- GET services/{UUID}: ficha incluyendo servicios inactivos para conservar contexto histórico.
- GET order-options: clientes activos no prospectos y técnicos activos.
- GET orders/{ticketId}: orden existente o null.
- POST orders: ticketId, clientId, serviceId opcional, title, description, requestedBy, type (1/2/4), assignedUserId opcional, startDate/endDate (YYYY-MM-DD).

La API de inventario proyecta una lista explícita y no comparte contraseñas cifradas, correos de acceso, costos o notas privadas. El secreto es operativo y permite abrir órdenes; nunca se entrega al navegador. Límite compartido de 240 solicitudes/minuto.

Cada monitor tiene un servicio; un servicio puede tener varios monitores. Los vínculos viven en Support; crear un servicio en Control basta para que aparezca en la siguiente consulta del catálogo (60 s mientras esté visible). No se crean vínculos por nombre ni se infiere una empresa.

## Órdenes y notificaciones

El equipo confirma la orden desde Support. Control valida cliente activo, servicio y pertenencia, técnico, tipo y fechas; usa el checklist activo y área inicial Levantamiento. Guarda orden, referencia de origen y solicitud de avisos en una transacción. Después usa ServiceOrderCreationNotifications, igual que el alta manual existente.

La restricción única SourceId/TicketId garantiza una orden por ticket. Reintentos devuelven la orden ya registrada y no repiten correos. Ante timeout, consultar GET orders/{ticketId}. Los avisos fallidos siguen disponibles para reintento en el detalle de Control; no se pierde la orden.

Support consulta el estado actual al abrir la orden y permite refrescarlo. No cierra tickets ni cambia monitores automáticamente. El cliente solo reporta problemas mediante tickets de su empresa; la API y los datos de proveedor son internos.

Desactivar la integración retirando la clave no elimina órdenes ni vínculos. Mantener la tabla para auditoría. Cambiar el SourceId o conectar otra base requiere reconciliación operativa previa para no perder la correspondencia.

## Pruebas

`dotnet run --project HNControl.Web.Tests -- --support-integration`

Base SQLite aislada y correo simulado, sin crear órdenes de producción. Incluye proyección EF real, filtros, separación cuenta/línea, validación cliente/servicio, tipos/fechas, unicidad, avisos persistentes y reintentos sin duplicar órdenes. La suite general incluye estas comprobaciones.
