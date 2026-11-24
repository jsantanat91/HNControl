# Integración con el levantamiento técnico (Web 2.5.33 – 2.6.0)

El formulario público de levantamiento (cPanel) usa dos endpoints de HN Control. `buscar_cliente.php` precarga los datos de un cliente existente. `enviar.php` (desde 2.6.0) entrega cada levantamiento enviado al módulo **Ventas › Factibilidad**. Control es la base de clientes y de factibilidades; el formulario solo guarda una copia de respaldo (CSV/JSON y PDF).

## Activación

1. Generar una clave aleatoria de 32+ caracteres (por ejemplo `openssl rand -hex 32`). No reutilizar la clave de Support: aquella puede crear órdenes.
2. En el servidor de Control, definir `HN_LEVANTAMIENTO_INTEGRATION_KEY` en el entorno que usa `docker-compose.yml` (se mapea a `LevantamientoIntegration__ApiKey`) y recrear el contenedor.
3. En cPanel, poner la misma clave en `datos/control.php` del levantamiento.

Sin clave configurada, ambos endpoints responden `503`. El formulario sigue funcionando: la búsqueda indica que no está disponible y los envíos quedan en la cola `datos/pendientes_control.json`, que se reintenta con los envíos siguientes.

## Consulta de cliente

`POST /api/integrations/levantamiento/clients/lookup` con encabezado `X-HN-Levantamiento-Key: <clave>` y este cuerpo JSON:

```json
{ "rfc": "ABC010101XY1", "email": "compras@cliente.mx" }
```

`rfc` acepta RFC o número de cliente; se ignoran mayúsculas, espacios, guiones y puntos. `email` es opcional desde 2.5.34.

| Envío | Respuesta |
|---|---|
| RFC válido, sin correo o con un correo que no es del cliente | `200 {found:true, contactVerified:false, client:{...}}`: datos de la empresa más el **representante legal** (y su cargo), el **teléfono principal** y el **primer correo principal** (2.6.0) |
| RFC o número de cliente **y** correo de un contacto del mismo cliente | `200 {found:true, contactVerified:true}`: nombre, cargo y teléfono de ese contacto |
| RFC o número de cliente **y** correo principal, de facturación o legal del cliente | `200 {found:true, contactVerified:true}`: representante legal y teléfono principal; `email` = el correo escrito |
| Número de cliente sin correo, RFC mal formado o correo inválido | `400` |
| RFC inexistente, cliente inactivo o prospecto; número de cliente con correo ajeno | `404 {found:false}` |
| Clave incorrecta / integración sin configurar | `401` / `503` |
| Más de 60 consultas por minuto | `429` |

`client` = `{clientCode, name, rfc, contactName, contactRole, phone, email, city, address, businessLine}`.

Exponer el representante legal, el teléfono y el correo principal solo con el RFC fue una decisión de negocio (2.6.0): son datos de contacto de la empresa, para que el cliente no los vuelva a capturar. El número de cliente es consecutivo (HN-0001, HN-0002…), por eso solo se acepta con un correo registrado; sin él se podría recorrer la lista de clientes.

**No se exporta:** datos fiscales (domicilio, CP, régimen, uso de CFDI), correos de facturación y legal, correos principales secundarios, ids internos, servicios, contratos ni saldos.

## Recepción de levantamientos (2.6.0)

`POST /api/integrations/levantamiento/surveys`, con la misma clave, acepta cuerpos de hasta 8 MB:

```json
{
  "externalId": "20261124-153012-a1b2c3",
  "receivedAt": "2026-11-24T21:30:12Z",
  "company": "Empresa SA de CV", "rfc": "ABC010101XY1", "clientCode": "HN-0012",
  "contactName": "…", "contactRole": "…", "email": "…", "phone": "…",
  "city": "Monterrey, Nuevo León", "address": "…", "businessLine": "…",
  "services": ["Internet dedicado"], "urgency": "…", "budget": "…", "executive": "…", "visit": "…",
  "sections": [{ "titulo": "Internet dedicado", "items": [{ "h": "Sitio" }, { "k": "Ancho de banda", "v": "100 Mbps" }] }],
  "pdfFileName": "levantamiento.pdf", "pdfBase64": "JVBERi0…",
  "sitesFileName": "sitios.xlsx", "sitesBase64": "UEsDB…"
}
```

| Caso | Respuesta |
|---|---|
| Nuevo | `201 {id, reused:false, clientLinked}` |
| Mismo `externalId` ya recibido (reintento) | `200 {id, reused:true, clientLinked}`, no duplica |
| `externalId` inválido (`[A-Za-z0-9._-]`, 120 máx.), falta empresa o correo, PDF que no empieza con `%PDF-` o pesa más de 5 MB, archivo de sitios que no es .xlsx/.xls/.csv o pesa más de 5 MB | `400 {message}` |

**Ligado de cliente:** primero por `clientCode` y después por RFC, solo clientes activos. Si no hay coincidencia, la factibilidad aparece en "Sin cliente".

**Archivo de sitios:** se lee la primera hoja (o el CSV con `;` o `,`). Las columnas se detectan por encabezado:
- Dirección: "Dirección", "Domicilio", "Ubicación" o "Sitio".
- Ancho de banda: "MB", "Capacidad", "Velocidad" o "Ancho".
- Sin encabezados reconocibles se toman las columnas A y B.

Se leen hasta 300 sitios y el resto de las columnas va a notas.

## Flujo en Control

1. Llega el levantamiento y aparece en Ventas › Factibilidad con estado *Nueva*.
2. Si no tiene cliente: **Crear cliente con estos datos** (requiere permiso de edición de clientes) o **ligar** a uno existente.
   - Al crear se usan razón social, RFC, correo, teléfono, ciudad, dirección y giro, y el contacto principal. Se genera el código HN-xxxx.
   - Si el RFC ya existe, se liga al cliente existente.
   - Las demás factibilidades sin cliente con el mismo RFC quedan ligadas también.
3. **Crear orden de levantamiento**:
   - Solo con cliente.
   - Es de tipo Levantamiento técnico e incluye el resumen y la liga a la factibilidad.
   - La factibilidad pasa a *En revisión*.
   - Se notifica igual que cualquier orden nueva.
4. Estado y notas internas se guardan desde el mismo detalle.

## Límites de abuso

Control limita a 60 llamadas por minuto por integración. El formulario agrega sus propios límites por IP: 8 búsquedas cada 15 minutos y 6 envíos por hora. Registra cada búsqueda y cada envío en `datos/registro.csv`.

## Base de datos

`LevantamientoSurveys` se crea al iniciar la aplicación (`CREATE TABLE IF NOT EXISTS`). Si el usuario de la base no tiene permiso CREATE, la aplicación registra una advertencia y hay que ejecutar `Sql/20261124_levantamiento_surveys.sql`. La tabla anterior `ServiceFeasibilities` no se modifica.
