# Recuperación del despliegue de firmas y notificaciones

El error PostgreSQL `42501: must be owner of table ClientLegalDocuments`
interrumpe el arranque cuando Entity Framework intenta aplicar las migraciones
de firmas. El proceso web no llega a escuchar y el proxy puede mostrar un 502.
Volver a desplegar con el mismo usuario de base de datos no corrige los permisos.

## Procedimiento

1. Conectarse a **la base de datos que usa HN Control**, como `postgres` o con
   un usuario propietario de las tablas afectadas. El usuario habitual de la
   aplicación no puede ejecutar esta reparación si no es propietario.
2. Abrir `20261022_repair_document_signatures.sql` y ejecutar **todo el archivo**.
   En pgAdmin se puede abrir desde Query Tool. Con `psql`, usar:

   ```sh
   psql -h HOST -U USUARIO_PROPIETARIO -d BASE_HNCONTROL -v ON_ERROR_STOP=1 -f 20261022_repair_document_signatures.sql
   ```

   Sustituir los tres valores por los del servidor. No incluir contraseñas en el
   comando ni compartirlas en registros o capturas.
3. Confirmar que la ejecución terminó sin errores y que la consulta final muestra:
   - `20261022000100_AddClientDocumentCountersignature`
   - `20261022000200_AddDeliverySignaturesAndNotificationRecipients`
4. Reiniciar el servicio o contenedor de HN Control. La versión desplegada que
   contiene estas migraciones puede arrancar después de reparar la base; no
   necesita volver a ejecutar las modificaciones con el usuario de la aplicación.
5. Revisar que el registro alcance `Application started` / `Now listening on`,
   que no repita el error 42501 y que sea posible abrir el inicio de sesión y
   Configuración → General. Verificar después el flujo de firmas con datos de prueba.

Si una sentencia falla, no ejecutar instrucciones aisladas ni registrar las
migraciones manualmente. En una consola que mantenga la conexión, ejecutar
`ROLLBACK;` antes de corregir la causa y volver a ejecutar el archivo completo.
Un error de permisos requiere reconectar como propietario; un error de esquema
o de migraciones previas requiere revisar la base seleccionada.

## Alcance

El script agrega las 13 columnas de las dos migraciones dentro de una transacción,
valida sus tipos y nulabilidad y solo entonces registra las migraciones. No borra
registros, no modifica propietarios y no otorga privilegios al usuario web.
Los destinatarios internos predeterminados se asignan solo al crear la columna;
si ya existe, se conservan sus valores, incluido un valor vacío.
Se puede repetir después de una ejecución exitosa.

El archivo SQL también se incluye en `Sql/` al publicar la aplicación.

## Validación local

Se reprodujo el error 42501 usando un rol sin propiedad en PostgreSQL embebido
(PGlite). Se verificaron la reparación como propietario, las 13 columnas, la
conservación de registros y destinatarios, la repetición sin duplicados y la
lectura posterior con el rol de aplicación sin ampliar sus privilegios. También
se comprobó la reversión completa ante un tipo de columna incompatible o una
migración previa ausente. Las 77 comprobaciones de `HNControl.Web.Tests` pasaron.
Estas pruebas no sustituyen la ejecución y verificación en el servidor afectado.
