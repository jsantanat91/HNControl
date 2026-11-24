# Integración con el levantamiento técnico (Web 2.5.33)

El formulario público de levantamiento (cPanel, `buscar_cliente.php`) consulta a HN Control para precargar los datos de un cliente existente. Control es la única base de clientes; el formulario no guarda copia.

## Activación

1. Generar una clave aleatoria de 32+ caracteres (por ejemplo `openssl rand -hex 32`). No reutilizar la clave de Support: aquella puede crear órdenes.
2. En el servidor de Control, definir `HN_LEVANTAMIENTO_INTEGRATION_KEY` en el entorno que usa `docker-compose.yml` (se mapea a `LevantamientoIntegration__ApiKey`) y recrear el contenedor.
3. En cPanel, poner la misma clave en `datos/control.php` del levantamiento.

Sin clave configurada el endpoint responde `503` y el formulario indica que la búsqueda no está disponible; el cliente puede llenar sus datos a mano.

## Contrato

`POST /api/integrations/levantamiento/clients/lookup`

Encabezado `X-HN-Levantamiento-Key: <clave>`, cuerpo JSON:

```json
{ "rfc": "ABC010101XY1", "email": "compras@cliente.mx" }
```

`rfc` acepta RFC o número de cliente; se ignoran mayúsculas, espacios, guiones y puntos. Desde 2.5.34 `email` es opcional.

| Envío | Respuesta |
|---|---|
| RFC válido, sin correo (o con un correo que no es del cliente) | `200 {found:true, contactVerified:false, client:{...}}` con razón social, RFC, giro, ciudad y dirección; `contactName`, `contactRole` y `phone` vacíos |
| RFC o número de cliente **y** correo registrado del mismo cliente | `200 {found:true, contactVerified:true, client:{...}}` con contacto, cargo y teléfono |
| Número de cliente sin correo, RFC mal formado o correo inválido | `400` |
| RFC inexistente, cliente inactivo o prospecto; número de cliente con correo ajeno | `404 {found:false}` |
| Clave incorrecta / integración sin configurar | `401` / `503` |
| Más de 60 consultas por minuto | `429` |

`client` = `{clientCode, name, rfc, contactName, contactRole, phone, city, address, businessLine}`. Si el correo es de un contacto del cliente, se devuelven el nombre, cargo y teléfono de ese contacto; si no, el contacto y teléfono generales del cliente.

Por qué el contacto exige correo: nombre y teléfono de una persona son datos personales (LFPDPPP) y el RFC de una empresa aparece en cada factura. El número de cliente es consecutivo (HN-0001, HN-0002…), por eso solo se acepta con correo: sin él se podría recorrer la lista de clientes.

## Lo que no se exporta

Datos fiscales (domicilio, CP, régimen, uso de CFDI), representante legal, correos registrados (solo se usan para comparar), ids internos, servicios, contratos y saldos. Clientes inactivos y prospectos temporales no se encuentran.

## Límites de abuso

Control limita a 60 consultas por minuto por integración; el formulario agrega 8 intentos por IP cada 15 minutos y registra cada búsqueda en `datos/registro.csv`.
