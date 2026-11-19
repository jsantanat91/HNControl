# Contrato recurrente v5: ficha y portal de soporte

Revisión del 19 de noviembre de 2026. Web 2.5.29. Sustituye la plantilla para nuevas emisiones, conservando las revisiones anteriores y sus documentos firmados.

## Procedencia de los datos

La captura anterior era un documento **DEMO**, generado por las pruebas, sin datos de un cliente real. No se insertan esos valores de prueba en producción.

| Información del contrato | Origen en HN Control |
| --- | --- |
| Cliente, número, RFC, contactos y domicilio fiscal | Ficha del cliente |
| Número, nombre del servicio, sucursal y dirección de instalación | Servicio contratado |
| Velocidad de bajada | Capacidad de Internet y capacidad alternativa ya capturadas en el servicio |
| Modalidad Dedicado/Best Effort | Selección explícita en Editar servicio; determina las condiciones aplicables, no se adivina por el nombre |
| Inicio, fin y plazo | Fechas y plazo del servicio contratado |
| Mensualidad e instalación | Importe mensual y costo de instalación existentes |
| Razón social, RFC, domicilio fiscal y código postal de HN | Configuración; se congelan al emitir, sin copiar credenciales SMTP |
| Subida/simetría y fecha pactada de instalación | Opcionales del servicio; solo se imprimen cuando se capturan |
| Título/titular y registro PROFECO | Referencias opcionales capturadas y verificadas por el administrador; capturarlas no verifica ni tramita el registro |
| Monitoreo e informes del cliente | Portal externo confirmado por el usuario: https://support.hubnet-solutions.net |

La ficha deja de mostrar red/reventa, operador, IP, medio/punto de entrega, latencia/jitter/pérdida, perímetro/método y responsable individual de escalamiento. No se transforma un dato de reventa en una declaración de propiedad de la red. HN presta bajo su marca y responde frente al cliente; el contrato admite infraestructura propia o de terceros conforme a los títulos aplicables.

Las IP, equipos y datos técnicos de instalación se documentan al entregar el servicio. El portal, usuario y contraseña guardados en el servicio pueden ser credenciales del proveedor: **no se usan como acceso del cliente al soporte ni se imprimen**.

## Informes, medición y soporte

El cliente consulta monitoreo e informes mediante su acceso ya asignado a la plataforma de soporte. El informe mensual estará disponible dentro de los primeros cinco días hábiles del mes siguiente. Puede solicitar gratuitamente copia o extractos y recibirlos en cinco días hábiles. Si el portal no está disponible conserva WhatsApp **5526014566** y **soporte@hubnet-solutions.com.mx**, sin perder el derecho a reportar, aclarar o reclamar.

El informe conserva métricas efectivamente medidas, periodos sin datos, interrupciones, causas, exclusiones justificadas, tickets y tiempos de atención/restablecimiento. La evidencia puede contrastarse con la del cliente. Los periodos sin medición se investigan con las demás evidencias y no se asumen disponibles; si siguen sin aclararse no se presenta un porcentaje definitivo, sin suspender la atención ni los derechos del usuario. Quitar campos manuales del formulario no elimina la obligación de medir la disponibilidad ni de explicar cómo se obtuvo el informe.

Se mantiene **99.85% mensual** para Dedicado y **cuatro horas máximas de primera respuesta humana**, 24/7. Cuatro horas de restablecimiento sigue siendo un objetivo operativo distinto. No es una garantía de reparación máxima; las compensaciones no dependen de esperar 72 horas. Best Effort conserva sus condiciones diferenciadas. Corte el día 1, primera factura contra entrega y posteriores pagos el día 15.

Esta entrega ajusta contrato, captura y bienvenida. La publicación de informes y el monitoreo corresponden al portal externo existente y a la operación del NOC; no se incorpora un nuevo conector, colector ni mecanismo de envío automático.

## Segunda revisión legal

Fuentes oficiales revisadas:

- [Ley en Materia de Telecomunicaciones y Radiodifusión](https://www.diputados.gob.mx/LeyesBiblio/pdf/LMTR.pdf): títulos según actividad, responsabilidad frente al usuario y derechos/contratación, en particular artículos 164-II y 185–190.
- [NOM-184-SCFI-2018](https://sidof.segob.gob.mx/notas/docFuente/5552286), [vigente según el catálogo oficial](https://platiica.economia.gob.mx/normalizacion/nom-184-scfi-2018/): identificación del proveedor, calidad contratada, responsabilidad por terceros, instalación e inicio de cobros, compensaciones y atención, según su ámbito de aplicación.

Se corrigieron referencias a datos corporativos fijos no vinculados al sistema, representación incongruente con la firma y cobro por una fecha meramente programada. El prorrateo inicia con la prestación efectiva, no con la firma. La terminación por fuerza mayor no permite cobrar servicios no prestados; la rescisión respeta el procedimiento pactado y las protecciones de importes controvertidos. Los cambios de servicio y sus costos requieren acuerdo expreso. La ficha simplificada no sustituye derechos mínimos, los reportes no son prueba indubitable y un fallo del proveedor de acceso no libera automáticamente a HN.

El formato no acredita una autorización, concesión ni registro PROFECO. La revisión documental no certifica cumplimiento integral: la persona jurídica, títulos reales, modelo registrado y condiciones específicas deben validarse con el responsable jurídico antes de presentarlo como modelo registrado. Se conservan los límites y asuntos de registro descritos en la [revisión anterior](contratos-anexo-regulatorio-2.5.28.md).

## Compatibilidad y aplicación

No requiere SQL ni APK. Desplegar web 2.5.29. Los datos ya están en los campos y metadatos actuales; las filas opcionales vacías no se imprimen. Se pueden guardar avances, pero no emitir Internet sin modalidad, bajada, dirección y vigencia coherentes. Subida/simetría se validan juntas solo si se especifican.

Las plantillas v1–v4 no se modifican. La validación y sustitución de datos de v4 siguen sus reglas originales al firmar o reemitir su snapshot; cambiar Configuración o el servicio no reescribe documentos emitidos. Una regeneración explícita de un borrador sin firmas usa la nueva revisión; cambiar un contrato aceptado exige un nuevo acuerdo. Se conservan ambas firmas autógrafas y sus hashes.
