# Contrato general y Anexo A — revisión 4

Revisión del 19 de noviembre de 2026. Implementación: web 2.5.28. Aplica a nuevos contratos recurrentes; no modifica contratos eventuales ni documentos ya emitidos con revisiones anteriores.

## Decisiones confirmadas

- Disponibilidad mínima mensual de Internet Dedicado: **99.85%**. En 30 días equivale a 64 minutos y 48 segundos de indisponibilidad computable.
- **Máximo de primera respuesta humana: cuatro horas transcurridas, 24/7**, también para incidentes parciales. Objetivo temprano de respuesta: 15 minutos; diagnóstico temprano: una hora. No basta el acuse automático.
- Objetivo de restablecimiento ante interrupción total: cuatro horas. **No es un máximo garantizado de resolución**. La petición de un cliente de garantizar reparación en cuatro horas es distinta y no se aprobó como obligación general.
- Best Effort mantiene condiciones diferenciadas, sin porcentaje garantizado por defecto; atención gratuita 24/7 para ambas modalidades.
- Soporte: WhatsApp 5526014566 (preferente), soporte@hubnet-solutions.com.mx y https://support.hubnet-solutions.net. Un ticket basta; el NOC coordina Ingeniería y Operaciones.
- Corte día 1; primera factura contra entrega y pagos posteriores el día 15 del mismo mes.

## Uso en HN Control

En **Clientes → servicio contratado → Editar** se captura el Anexo de Servicios de Internet. La bajada, sede, dirección, proveedor y vigencia reutilizan campos existentes. El resto se guarda en los metadatos de Notes; no necesita SQL. Se puede guardar incompleto para continuar después, pero la emisión exige los datos técnicos mínimos, vigencia, fecha pactada de instalación/activación y método de medición. La instalación no se infiere del inicio de vigencia. Los campos opcionales sin datos se identifican como no especificados; no se inventan títulos, registros o parámetros.

La razón social, RFC, domicilio fiscal y código postal de HN se toman de **Configuración**, deben estar completos antes de emitir y se congelan en el snapshot. Se copian exclusivamente esos cuatro campos; no se incluyen claves SMTP/PAC ni la configuración completa. Cambiar después los datos de Configuración no modifica el documento firmado.

El PDF incluye ficha individual, niveles de servicio, medición, soporte, escalamiento y las cláusulas. Las firmas finales del cliente y HN abarcan el documento y su Anexo A. La revisión y los datos quedan congelados para la segunda firma. Las plantillas v1–v3 se conservan; no se cambia el compromiso de un documento previo al reenviarlo. Para modificar uno ya aceptado corresponde formalizar una adenda bilateral o un nuevo acuerdo.

## Compromisos operativos que exige el formato

El NOC debe entregar el informe mensual en los primeros cinco días hábiles del mes siguiente y los extractos solicitados en cinco días hábiles. Debe documentar medición, interrupciones, causas, exclusiones, respuesta, escalamiento y restablecimiento; identificar periodos sin datos y aceptar evidencia del usuario. El anexo define dónde y cómo se mide. Un enlace crítico puede necesitar respaldo para sostener 99.85%; no se presume redundancia si no está contratada.

Esta versión modifica el contrato y su captura. **No incorpora un colector de métricas, acceso del cliente al NOC ni automatización de informes.** Los reportes deben prepararse y entregarse mediante la operación de soporte existente. Una interrupción computable de cuatro horas incumple 99.85% en un mes de 30 días, aunque se responda dentro del máximo; se aplican las compensaciones correspondientes.

## Hallazgos regulatorios y fuentes oficiales

1. El [portal del IFT](https://www.ift.org.mx/industria/concesion-unica-para-uso-comercial) se identifica como archivo histórico y dirige los trámites vigentes a la Comisión Reguladora de Telecomunicaciones (CRT). Se evita presentar un futuro trámite como autorización obtenida.
2. La [Ley en Materia de Telecomunicaciones y Radiodifusión](https://www.diputados.gob.mx/LeyesBiblio/pdf/LMTR.pdf), publicada el 16 de julio de 2025, distingue concesión única comercial (arts. 45–46) y autorización para comercializadoras sin carácter de concesionario (art. 159-I). Reventa y explotación de red propia deben revisarse según la actividad y títulos reales; «registrarse como carrier» no identifica por sí solo el trámite suficiente. El art. 164-II mantiene responsabilidad de la comercializadora frente al usuario.
3. Los arts. 185–190 de esa ley contienen derechos de usuarios, condiciones de contratación y registro del modelo ante PROFECO. Capturar un folio en HN Control no tramita ni verifica una autorización o registro.
4. La [NOM-184-SCFI-2018](https://sidof.segob.gob.mx/notas/docFuente/5552286) figura [vigente en el catálogo oficial](https://platiica.economia.gob.mx/normalizacion/nom-184-scfi-2018/). Se revisaron identificación del proveedor (5.2.2), calidad contratada (5.2.5), responsabilidad con terceros (5.2.7), instalación/anticipos/inicio de cobros (5.2.14–16), reintegro proporcional y bonificación mínima de 20% del periodo afectado (5.2.18), cancelación de adicionales (5.2.24.1), conciliación (5.2.32.1), registro de modelo (5.2.33), atención gratuita 24/7 (6.1) y facturación reclamada (6.3.3).
5. La NOM tiene exclusiones de ámbito en 1.3; no se presume que toda contratación empresarial esté excluida. El plazo de resolución de quejas de 6.3.1 no es un SLA técnico de reparación. No se encontró una regla general que asigne 72 horas al residencial o cuatro horas al dedicado: los plazos aquí descritos son compromisos comerciales de HN.

## Antes de usar el modelo como contrato registrado

Validar la persona jurídica que contrata, actividad real, títulos necesarios y vigentes, registro del modelo ante PROFECO cuando corresponda y capacidad operativa de cumplir instalación, informes, SLA y compensaciones. Formalizar también la pena aplicable al incumplimiento de instalación según la contratación y el modelo que se registre; la revisión remite a la procedencia legal y no inventa un importe autorizado. El DOCX es una adaptación para revisión y uso contractual; **no es un modelo aprobado por la autoridad ni una certificación de cumplimiento integral**. Las referencias regulatorias deben completarse con documentos comprobados, conservando la distinción entre autorización comercializadora, concesión de red y registro de contrato.
