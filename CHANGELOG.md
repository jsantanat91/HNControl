# HN Control — notas de versión

## Web 2.5.32 · 20 de noviembre de 2026

- Contacto principal (quien firma) y contactos del cliente en copia para servicios contratados, contratos/NDA y órdenes. Los documentos y órdenes heredan la selección del servicio y permiten personalizarla antes de firmar.
- Destinatarios guardados por documento: los cambios posteriores en la agenda no redirigen silenciosamente sus correos. Validación de pertenencia al cliente y eliminación de correos repetidos.
- Avisos al cliente, PDF de órdenes, bienvenida, invitación y confirmación de firma usan esos destinatarios y conservan las copias internas. Ingeniería mantiene su aviso operativo separado.
- Selección bloqueada cuando ya existen firmas. Cambiar destinatarios de una invitación pendiente revoca el enlace anterior; es necesario reenviar la invitación.
- **Despliegue:** ejecutar primero `HNControl.Web/Sql/20261120_document_contacts.sql` y después actualizar la web. Agrega tres columnas opcionales sin sustituir datos históricos. Sin nueva APK.

## Web 2.5.31 · 19 de noviembre de 2026

- API interna para HN Support 2.3.0: inventario de servicios y cuentas con filtro, búsqueda y paginación. DTO explícito sin contraseñas, notas ni costos.
- Órdenes desde tickets de Support: confirma cliente, servicio, técnico y fechas; guarda orden y referencia juntas. Restricción única por origen/ticket evita duplicados y no repite avisos ante reintentos.
- Conserva flujo técnico, checklist y sistema de notificaciones existente. Referencia al ticket en el detalle administrativo de la orden. Los clientes de Support solo abren tickets.
- Clave de servidor compartida, HTTPS y límite de solicitudes. Integración deshabilitada si no se configura. Tabla aditiva SupportOrderLinks, creada al iniciar con clave; SQL alternativo incluido para permisos restringidos.
- Mantiene Web 2.5.30 y notas anteriores sin alteración; sin nueva APK. Activación y límites en [guía de integración](docs/support-integration-2.5.31.md).

## Web 2.5.30 · 19 de noviembre de 2026

- Nueva variante **Servicios web mensuales**, seleccionable al crear o editar el servicio del cliente. Datos públicos propios: plataforma, URL HTTPS, plan, alcance, licencias y horario de soporte. Precarga nombre y enlace de Check-in/GPS sin inferir funciones incluidas; admite otras plataformas.
- Contrato SaaS independiente con carátula, ficha de alcance, condiciones de licencia, operación, privacidad, pagos y cancelación. Firmas autógrafas de ambas partes y registro digital; conserva los contratos de Internet, eventos y documentos ya emitidos.
- Mensual sin plazo forzoso, corte día 1 y pago día 15. Importes antes de IVA y totales con IVA 16%. Custodia contractual restringida durante un año tras la baja, exportación durante ese periodo y eliminación o conservación conforme a los derechos y obligaciones legales aplicables.
- Bienvenida específica con datos congelados del documento y canales NOC. No envía credenciales ni notas internas. La emisión valida datos web e identidad fiscal de HN; plan y licencias opcionales se omiten cuando están vacíos.
- Sin SQL ni nueva APK. Desplegar la web. Esta versión no modifica las plataformas externas ni automatiza retención, eliminación o avisos de renovación. Detalles y fuentes: `docs/contratos-servicios-web-2.5.30.md`.

## Web 2.5.29 · 19 de noviembre de 2026

- Contrato recurrente v5 con ficha simplificada: servicio, modalidad Dedicado/Best Effort, velocidad, sede/dirección, vigencia y cargos registrados. Retira red/reventa, operador de acceso, IP, medio/punto de entrega y parámetros técnicos manuales de la ficha.
- Subida/simetría, instalación y referencias regulatorias verificadas quedan opcionales y sus filas se omiten cuando no hay datos. La emisión exige solo datos esenciales coherentes; no inventa velocidades, fechas ni títulos. La edición conserva los metadatos históricos ocultos.
- Monitoreo e informes mensuales se consultan con el acceso del cliente en `https://support.hubnet-solutions.net`. Informe disponible en los primeros cinco días hábiles; copias/extractos gratuitos en cinco días hábiles. Mantiene WhatsApp/correo, evidencia objetable y atención si el portal no está disponible.
- Segunda revisión de coherencia legal: prestación bajo la marca HN con responsabilidad ante el usuario; cobro desde prestación efectiva, cambios consentidos y representación conforme a las firmas. No afirma propiedad de toda infraestructura ni presenta trámites pendientes como concedidos.
- Conserva disponibilidad mensual 99.85%, máximo de primera respuesta humana de cuatro horas y objetivo separado de restablecimiento. Firmas autógrafas/hash, snapshots y lógica de emisión histórica v4 preservados. Ver `docs/contratos-anexo-simplificado-2.5.29.md`.
- Bienvenida alineada a v5. Solo desplegar la web; sin SQL ni APK. El portal de soporte ya existe: esta versión cambia las referencias del contrato, no su plataforma externa.

## Web 2.5.28 · 19 de noviembre de 2026

- Contrato recurrente general v4 con Anexo A integrado: modalidad, capacidad en Mbps, red propia/reventa/mixta, operador de acceso, punto de entrega, vigencia, parámetros y método de medición, escalamiento y referencias regulatorias verificadas.
- Captura desde alta/edición del servicio contratado. Guarda avances pero bloquea emitir un contrato de Internet con anexo técnico incompleto o velocidades incompatibles con la simetría. Reutiliza datos y metadatos existentes; congela proveedor y ficha al preparar la firma.
- Por indicación del usuario, cuatro horas es el máximo de **primera respuesta humana** para incidentes de Internet Dedicado, 24/7. Se conserva el objetivo temprano de 15 minutos y el objetivo separado de recuperación en cuatro horas. No se promete reparación máxima en cuatro horas. Disponibilidad mensual 99.85% y compensaciones independientes.
- Matriz NOC/Ingeniería/Operaciones; informes mensuales en los primeros cinco días hábiles y extractos en cinco días hábiles por correo o ticket. Evidencia consultable y controvertible. Son obligaciones operativas del formato; esta versión no automatiza informes ni reactiva monitoreo.
- Ajustes por LMTR/NOM-184: responsabilidad ante el cliente aun con terceros, bonificación mínima explícita, cancelación sin pena automática sobre rentas, propiedad de equipos según entrega, protección ante reclamaciones y referencias a CRT/PROFECO. El formato no acredita concesión, autorización ni registro de modelo; ver `docs/contratos-anexo-regulatorio-2.5.28.md`.
- Bienvenida alineada a la revisión del documento. Firmas autógrafas/hash y revisiones históricas conservadas. Sin cambios SQL ni APK; requiere desplegar la web. Contratos existentes se modifican solo mediante acuerdo expreso, no al regenerar silenciosamente sus condiciones.

## Web 2.5.27 · 19 de noviembre de 2026

- Nuevos contratos recurrentes con plantilla `Contrato_Servicios_v3.docx`: Internet Dedicado con disponibilidad mínima mensual de 99.85%, respuesta humana en 15 minutos 24/7, diagnóstico inicial en una hora, actualización cada hora y objetivo de recuperación de cuatro horas. Best Effort con respuesta de una hora de soporte, diagnóstico de cuatro, actualización cada cuatro y recuperación objetivo de 24 a 48 horas transcurridas.
- Apartado de medición en el punto de entrega, periodos mensuales, evidencia y exclusiones documentadas. Los objetivos de recuperación no sustituyen la garantía mensual. Se elimina la espera mínima de 72 horas para compensaciones; el incumplimiento del SLA dedicado conserva el beneficio contractual mínimo del 20% mensual, sin reducir derechos legales superiores.
- Carátula y apartado de soporte: WhatsApp NOC **5526014566** como canal preferente, **soporte@hubnet-solutions.com.mx** y **https://support.hubnet-solutions.net** para abrir un ticket.
- Bienvenida alineada con el nuevo contrato; si incluye documentos históricos remite a las condiciones de cada uno. Plantillas anteriores, contratos ya emitidos, contratos eventuales y ambas firmas se conservan.
- Desplegar únicamente la web. Sin SQL ni nueva APK. Las nuevas condiciones requieren emitir un documento nuevo; no se sustituyen contratos existentes.

## Web 2.5.26 · 18 de noviembre de 2026

- Rediseño de la página pública de firma de contratos y NDA: encabezado azul HN, descarga y revisión del documento, estado de las firmas y formulario con secciones claras.
- Captura de firma con guía visual, indicador de firma lista, limpieza y conservación del trazo al redimensionar o girar el celular. Compatible con mouse, dedo y lápiz digital.
- Confirmaciones separadas para firma del cliente registrada y documento completado. Conserva las reglas existentes de contrato y NDA, aceptación, tokens, generación de documentos y correos.
- Verificación en navegador de estados, descarga, validación de firma vacía, captura táctil, cambio de tamaño y envío local con correo y conversión PDF simulados.
- Desplegar únicamente la web. Sin SQL adicional ni nueva APK.

## Web 2.5.25 · 15 de noviembre de 2026

- Corrige `DateTime Kind=Unspecified` al preparar el correo de pago: las fechas de nómina se normalizan a UTC antes de consultar PostgreSQL, manteniendo el día de calendario seleccionado.
- Corrige la consulta de bonos y deducciones que el panel mostraba en cero al ocultar ese error. Panel, Excel y recibo usan los ajustes correspondientes para sumar bonos y restar deducciones.
- Normaliza también las fechas del registro de ajustes aplicados al confirmar el pago; mantiene los importes históricos y la idempotencia por quincena.
- Regresión reproducida y validada con PostgreSQL real en una base local de prueba y correo simulado. Ejecutar con `HNCONTROL_TEST_POSTGRES` y `dotnet run --project HNControl.Web.Tests -- --payroll-postgres`; crea y elimina únicamente un esquema temporal propio.
- Sin SQL adicional ni nueva APK. Desplegar la web y volver a intentar la confirmación que quedó pendiente.

## Web 2.5.24 · 9 de noviembre de 2026

- Panel: tarjetas de indicadores y nómina con el mismo estilo de Empleados, botones claros, acceso a ficha en modal y gráficas adaptables en tonos azules. Estados vacíos, porcentajes legibles y exportación del periodo seleccionado.
- Empleados dados de baja excluidos de KPI, resumen de nómina, conteo y gráfica de desempeño del panel, y exportación Excel. Se bloquean evaluaciones y pago desde el panel para bajas, conservando los registros históricos.
- Nuevas asignaciones de órdenes solo a empleados activos; editar otros datos permite conservar una asignación histórica de baja.
- Órdenes: tarjetas con referencia, estado y responsables; ficha en modal organizada en Resumen, Entrega y firmas, y PDF y correo. Conserva acciones y destinatarios existentes, con pestaña recordada al generar PDF.
- Viáticos: totales del rango y empleado seleccionados, agrupación por empleado, tarjetas semanales y revisión en modal. Formularios y comprobantes uniformes para administración y empleados.
- Verificado en escritorio y móvil; sin cambios en reglas de cálculo ni eliminación de históricos. Sin SQL adicional ni nueva APK: desplegar únicamente la web.

## Web 2.5.23 · 9 de noviembre de 2026

- Clientes: tarjetas con identidad, estado y contratos, acciones secundarias agrupadas y búsqueda por nombre, número, RFC y correo; conserva el alcance de permisos del usuario.
- Ficha del cliente en modal, organizada en Contratos, Datos y facturación, Contactos, Actividad y Enlaces y acceso. Conserva la pestaña al guardar contactos, permite copiar enlaces y protege visualmente la contraseña del portal. Formularios con el mismo estilo de Empleados.
- KPI 80/20: avance de evaluaciones, búsqueda y filtros por estado, indicadores del periodo y formulario con escala de 1 a 5 y estimación en vivo.
- Cambiar empleado o quincena requiere cargar su evaluación; evita trasladar las notas anteriores. Confirma antes de descartar cambios y permite guardar notas vacías.
- Corrige la vista previa para usar la fórmula existente (suma de siete criterios / 35), sin cambiar reglas de nómina. Corrige el enlace de exportación Excel y distingue la estimación 80/20 de bonos y deducciones.
- Sin SQL adicional ni actualización de APK. Desplegar únicamente la web.

## Web 2.5.22 · 9 de noviembre de 2026

- Ficha de empleado en modal JavaScript desde tarjetas y tabla; acciones internas conservan el listado y refrescan los datos al cerrar si hubo cambios.
- Cabecera de identidad y estado; pestañas de resumen quincenal, datos del empleado, desempeño y viáticos. Sueldo y comisión configurada separados de los bonos registrados.
- Gráficas adaptables al abrir pestañas, porcentaje variable en escala 0–100 y estados vacíos. Navegación por teclado y diseño móvil.
- Conserva cálculos y permisos; no requiere SQL adicional ni nueva APK.

## Web 2.5.21 · 9 de noviembre de 2026

- Tarjetas de empleados homologadas al panel, con identidad y estado en cabecera, bloques de sueldo mensual y comisión, y acciones alineadas.
- Indicadores superiores con el estilo del dashboard. Diseño adaptable a móvil y nombres largos, conservando formularios, permisos y acciones.
- Solo presentación: no requiere SQL adicional ni reinstalar la APK.

## Web 2.5.20 · 9 de noviembre de 2026

- Inventario físico: navegación unificada, menos acciones repetidas, artículo preseleccionado al solicitar entradas y salidas. Servicios del inventario anterior retirado de menús y suspendido, sin borrar catálogo ni históricos.
- Cotizador público sin sesión en `/cotizar?public=true`, con tres pasos, búsqueda, contacto y resumen. Servidor calcula precios y rechaza acceso anónimo a edición/preview; ignora descuentos y conceptos manuales públicos. No publica listas de clientes ni contactos.
- Cotizador interno reutiliza datos de cliente/contacto; errores conservan la selección. Ventas incorpora acceso y copia del enlace público.
- Empleados: fichas por secciones, tarjetas renovadas y comisión configurable por orden. Se registra un bono en la quincena del cierre (México), preferentemente al técnico que tomó la orden; el asignado es respaldo. Registro único por orden, importe congelado al generarlo, sin retroactividad ni duplicados al reaprobar.
- Si la quincena ya está pagada, la comisión se registra en la siguiente pendiente. Recibos, ficha, resumen y exportación de nómina usan el mismo cálculo y conservan los bonos ya aplicados.
- Dashboard sin métricas ni gráfica de tickets locales; enlace al portal externo de soporte.
- Aplicar `HNControl.Web/Sql/20261109_order_commissions.sql` antes del despliegue. APK 2.3.0 compatible, sin reinstalación.

## Web 2.5.19 · 9 de noviembre de 2026

- Corrige la publicación Docker: el modelo compartido de entrega reside dentro de HNControl.Web, incluido en su contexto de compilación; la APK y sus pruebas enlazan ese mismo archivo.
- Mantiene APK 2.3.0. No requiere SQL adicional al de entrega unificada ya aplicado.

## Web 2.5.18 / APK 2.3.0 · 9 de noviembre de 2026

- Servicio, entrega de hardware o ambos en la misma orden; captura de marca y serie del router, otros equipos, cantidades, accesorios, modalidad y estado.
- Una sola orden PDF con soporte NOC, equipos, firmas técnico/cliente y huella que incluye la entrega. Se bloquea modificar equipos firmados y se detectan cambios entre captura y firma.
- Captura desde web y APK; formatos anteriores accesibles desde Órdenes → Entregas anteriores. Retirada la creación independiente y la sección de formatos en Clientes.
- Cuentas de proveedores en la APK: búsqueda y copia de cuenta, correo y contraseña para empleados y administradores. Contraseñas fuera de listados y solo obtenidas al copiar.
- Aplicar `HNControl.Web/Sql/20261109_order_delivery.sql` antes de desplegar la web; instalar APK 2.3.0 después.


## Web 2.5.17 · 9 de noviembre de 2026

- Orden PDF con bloque NOC, WhatsApp preferente y llamadas al 55 2601 4566, soporte@hubnet-solutions.com.mx y support.hubnet-solutions.net; mantiene firmas y huellas SHA-256.
- Enviar PDF por correo: cliente como destinatario y correos de Ingeniería configurados como CC, sin duplicados. Destinatarios visibles antes y después del envío.
- PDF regenerado al enviar para incluir los datos y firmas actuales; correo con formato corporativo y canales de soporte.
- Sin migración SQL ni APK nueva. El formato de entrega de hardware se conserva.

## Web 2.5.16 / APK 2.2.0 · 9 de noviembre de 2026

- Ficha móvil completa con cuenta vinculada, correo de acceso, referencias diferenciadas de cuenta y línea, costo, fechas y notas.
- Tarjetas por sección y badges de proveedor, estado, plan y conexión; copiar datos y abrir marcador de soporte. Starlink conserva soporte online.
- Copia de contraseña separada y restringida a administradores. Sin migración SQL nueva.


## APK 2.1.1 · 9 de noviembre de 2026

- Mensajes específicos para indisponibilidad del servidor (502/503/504), errores internos (500) y exceso de solicitudes (429). Incluyen el estado HTTP para diagnóstico.
- No modifica autenticación ni permisos; un servidor fuera de servicio debe recuperarse independientemente de esta actualización móvil.


## 2.5.15 · 9 de noviembre de 2026

- APK 2.1.0: Inventario Servicios, filtros por cliente/proveedor, detalle técnico de solo lectura y login azul renovado.
- Contratos: ambas firmas autógrafas visibles junto al registro digital. Captura HN obligatoria; completar firmas antiguas requiere la cuenta original.
- Migración: AddContractProviderAutograph. SQL alternativo en Sql/20261109_contract_provider_autograph.sql; desplegar API antes de distribuir la nueva APK.


## 2.5.14 · 8 de noviembre de 2026

- Nombre del plan en lista desplegable: Itinerante 100 GB, Itinerante Ilimitado, Residencial, Residencial Lite y Empresarial Global para Starlink; Internet 50, 100, 200, 300, 400, 500 y 1000 para los demás proveedores. Se conserva la opción de planes anteriores al editar.
- Tipo de plan Empresarial agregado junto a Dedicado y Best Effort para proveedores distintos de Starlink.
- Número de kit visible y editable solo para Starlink; los demás equipos muestran serie.
- Nueva cuenta abre el registro del proveedor en otra pestaña. Actualizar cuentas vuelve a cargar el selector conservando el formulario del servicio.

No requiere migración SQL adicional.

## 2.5.13 · 8 de noviembre de 2026

- Inventario Servicios con logotipos en la selección del proveedor y tipo de plan junto a esa selección: Residencial, Itinerante o Empresarial para Starlink; Dedicado o Best Effort para los demás.
- La tecnología de conexión se registra por separado: fibra óptica, satelital para Starlink, microonda u otra conexión. Starlink muestra soporte Online, completado y protegido también en el servidor.
- La fecha final del contrato se calcula automáticamente a partir del inicio y el plazo en meses. Se conservan los vencimientos anteriores que aún no tienen esos datos completos.
- Los parámetros IP, máscara, gateway, DNS, LAN y DDNS se despliegan con una casilla; cerrar el bloque conserva los datos guardados.
- Acceso al módulo Carriers anterior retirado del menú, con sus datos conservados.

No requiere migración SQL adicional a la versión 2.5.12.

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
