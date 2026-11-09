# HN Control móvil 2.1.0

Aplicación .NET MAUI para Android, con el diseño visual de Somos Uno adaptado a HN Control. El servidor sigue siendo https://control.hubnet-solutions.net.

## Alcance

- Órdenes de servicio: búsqueda por cliente, título o responsable; filtros de órdenes propias, disponibles y finalizadas; prioridad a vencidas; PDF; toma de órdenes; cuatro etapas técnicas; notas, evidencias, checklist, firmas y envío a revisión.
- Mi dashboard: nómina quincenal, neto, bonos, deducciones, historial de importes y variable, calificaciones KPI, comentarios de RH, resultados 360 publicados, vacaciones y capacitación.
- Viáticos: semanas, registro y edición de gastos, comprobantes, solicitud de anticipo mediante formulario de viaje y envío a revisión.

- Inventario de Servicios: consulta de equipos y datos técnicos con filtros combinables por cliente y proveedor, paginación y actualización. Acceso de solo lectura para Employee, Admin y SuperAdmin; sin credenciales de proveedores ni costos internos.

La navegación ofrece estos cuatro módulos. Los permisos y las reglas de negocio permanecen en el servidor.

## Correcciones

- Recarga después de tomar órdenes, guardar/eliminar gastos o enviar viáticos.
- Actualización de listas al regresar del detalle.
- Guardado de notas/checklist al retroceder de etapa.
- El checklist respeta permisos y ya no superpone el interruptor al título.
- El regreso desde la captura de firmas conserva el estado; cancelar con Atrás libera la espera.
- Los gastos cerrados no ofrecen edición/eliminación.
- Al editar gastos se conserva el comprobante existente. La API actual solo permite adjuntar archivos al crear gastos: la interfaz ya no ofrece una sustitución de archivo que el servidor no admite.
- Moneda es-MX, validación de acceso vacío, contraseña visible opcional y errores de conexión comprensibles.
- Eliminado el bypass de certificados TLS de las compilaciones de desarrollo.
- Sin resultados inventados ni puntuación cero cuando todavía no hay evaluación.

## Compilación y firma

Ejecutar en PowerShell:

    .\HNControl.Mobile\Build-Apk.ps1

El script utiliza .NET 10, Android SDK 36 y JDK 17 instalados en esta PC. Compila fuera de OneDrive en %LOCALAPPDATA%\HNControlBuild para evitar bloqueos de archivos temporales. Genera artifacts\entrega\HN-Control-2.1.0.apk y SHA256.txt.

La firma privada y su contraseña están en %LOCALAPPDATA%\HNControlSigning, con acceso restringido al usuario actual. Respaldar esa carpeta de forma segura: se necesita para firmar futuras actualizaciones. Nunca incorporarla al repositorio ni distribuirla con la APK.

El nuevo identificador es net.hubnet.hncontrol. La APK antigua encontrada usa com.companyname.hncontrol.mobile y una firma cuya clave no se encontró en esta PC. Esta versión se instala de forma independiente, sin desinstalar la antigua. Es necesario iniciar sesión nuevamente. Los datos se consultan desde el mismo servicio; no se migran credenciales locales.

Arquitecturas incluidas: arm64-v8a y x86_64. Android mínimo 5.0 (API 21), objetivo API 36. Compilación Release con firma RSA propia y depuración deshabilitada.

## Validación

    dotnet run --project HNControl.Mobile.Tests -c Release

30 comprobaciones con los servicios reales y un transporte HTTP simulado: contratos de órdenes y viáticos, multipart, precisión de nómina, visibilidad de evaluaciones, filtros, vencimientos, conflictos, desconexión, timeout y caducidad de sesión.

Prueba visual y funcional en emulador Android 15, con una copia de QA que usa un servidor local y datos ficticios: acceso, navegación, dashboard y KPI, filtros/estado vacío, creación de gasto y recarga inmediata, avance de las cuatro etapas de órdenes, persistencia de notas, captura de dos firmas y envío a revisión.

La copia QA y el servidor ficticio están separados del proyecto de entrega. La APK de entrega usa exclusivamente la configuración real del proyecto. Se verifica instalación, arranque, versión, arquitecturas y firma.

Pendiente de una cuenta autorizada y datos de prueba del servicio real: validación autenticada en producción, autorización por roles, PDF generado por el servidor, carga real de comprobantes/evidencias y procesamiento de nómina. No se realizaron escrituras autenticadas en producción. Las pruebas locales no certifican funcionamiento al 100% en todos los dispositivos o estados del servidor.

La app requiere conexión para consultar y guardar. No hay sincronización fuera de línea; ante un timeout, actualizar y verificar el resultado antes de repetir una operación.

## Entrega 2.1.0 (9 de noviembre de 2026)

Instalar artifacts/entrega/HN-Control-2.1.0.apk. Antes de consultar Servicios, desplegar web 2.5.15. Esta versión del servidor agrega firmas autógrafas de contratos y la migración AddContractProviderAutograph; si el usuario de la app no puede modificar tablas, aplicar HNControl.Web/Sql/20261109_contract_provider_autograph.sql con el propietario antes del deploy.

APK Release firmada con la clave existente, versionCode 3. Datos de QA ficticios y servidor local solo en una copia separada; la entrega conserva https://control.hubnet-solutions.net.
