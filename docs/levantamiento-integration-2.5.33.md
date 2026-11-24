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

`rfc` acepta RFC o número de cliente; se ignoran mayúsculas, espacios, guiones y puntos.

| Respuesta | Significado |
|---|---|
| `200 {found:true, client:{clientCode, name, rfc, contactName, contactRole, phone, city, address, businessLine}}` | RFC/número **y** correo pertenecen al mismo cliente activo |
| `404 {found:false}` | Cualquier otra combinación (no distingue RFC inexistente de correo incorrecto) |
| `400` | RFC de menos de 3 o más de 20 caracteres, o correo inválido |
| `401` / `503` | Clave incorrecta / integración sin configurar |
| `429` | Límite de 60 consultas por minuto |

Si el correo es de un contacto del cliente, se devuelven el nombre, cargo y teléfono de ese contacto; si no, el contacto y teléfono generales del cliente.

## Lo que no se exporta

Datos fiscales (domicilio, CP, régimen, uso de CFDI), representante legal, correos registrados (solo se usan para comparar), ids internos, servicios, contratos y saldos. Clientes inactivos y prospectos temporales no se encuentran.

## Límites de abuso

Control limita a 60 consultas por minuto por integración; el formulario agrega 8 intentos por IP cada 15 minutos y registra cada búsqueda en `datos/registro.csv`.
