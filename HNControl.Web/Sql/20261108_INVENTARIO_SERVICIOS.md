# Despliegue de Inventario Servicios

La versión 2.5.12 agrega `ServiceInventoryAccounts` y `ServiceInventoryItems`. El módulo Carriers y sus registros permanecen separados. La migración de EF se ejecuta normalmente si el usuario de la aplicación tiene permisos para crear tablas y referencias.

Si la conexión web usa un rol sin permisos de esquema, aplicar **antes del despliegue** el archivo `20261108_service_inventory.sql` desde Query Tool de pgAdmin, conectado a la base de HN Control como propietario o `postgres`.

1. Identificar el nombre del rol que utiliza la conexión PostgreSQL de la aplicación (`Username` de su configuración en el servidor). No es necesariamente el usuario con el que se abre pgAdmin y no se necesita compartir su contraseña.
2. En el encabezado del SQL, descomentar `SET LOCAL hncontrol.application_role = 'ROL_EXISTENTE_DE_HNCONTROL';` y sustituir únicamente el nombre por ese rol existente. **Este paso es obligatorio al ejecutar como `postgres` u otro superusuario:** si se omite, el archivo se detiene antes de crear tablas. Esto concede `SELECT`, `INSERT`, `UPDATE` y `DELETE` exclusivamente sobre las dos tablas nuevas. No concede permisos a `PUBLIC`, no crea roles y no cambia propietarios ni permisos de tablas anteriores.
3. Ejecutar **todo el archivo**. El proceso valida la migración anterior de Ingeniería, crea las tablas y relaciones, verifica las 43 columnas y registra `20261108000100_AddServiceInventory`. Solo al terminar correctamente se confirma la transacción.
4. Desplegar y comprobar que un administrador puede abrir **Inventario Servicios**, crear una cuenta y registrar un servicio. La contraseña se guarda una sola vez y se copia mediante HTTPS; no se muestra al editar.

Si el script falla, ejecutar `ROLLBACK;`, corregir el motivo y ejecutar nuevamente el archivo completo. No registrar la migración a mano. El archivo puede repetirse después de una ejecución exitosa; también permite agregar el permiso del rol web si inicialmente se omitió.

Las contraseñas usan las claves de ASP.NET Core Data Protection del sistema. Conservar el volumen/directorio de esas claves al desplegar o respaldar la aplicación: la base de datos por sí sola no permite recuperar las credenciales cifradas.
