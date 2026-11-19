# SLA de contratos recurrentes

Cambios aprobados el 19 de noviembre de 2026 para los documentos nuevos. La revisión 3 del contrato conserva el diseño de la revisión 2, los datos de contratación, el corte del día 1, el pago del día 15 y ambas firmas autógrafas con registro digital.

| Concepto | Internet Dedicado | Best Effort |
| --- | --- | --- |
| Disponibilidad mensual | 99.85% | Sin porcentaje garantizado salvo anexo expreso |
| Primera respuesta humana | Hasta 15 minutos, 24/7 | Hasta 1 hora de soporte |
| Diagnóstico y plan inicial | Hasta 1 hora | Hasta 4 horas de soporte |
| Actualización de avance | Cada hora | Cada 4 horas de soporte |
| Objetivo de recuperación | Hasta 4 horas transcurridas | De 24 a 48 horas transcurridas |

Los objetivos corresponden a interrupciones totales. En Best Effort rige el horario del anexo y, si no se especifica otro, atención 24/7. El objetivo de recuperación no es un máximo garantizado. Cumplirlo no exime de compensar un incumplimiento de disponibilidad.

La disponibilidad se calcula sobre el periodo activo del mes en Ciudad de México, descontando únicamente exclusiones acreditadas. En 30 días completos sin exclusiones, 99.85% equivale a 64 minutos y 48 segundos de interrupción acumulada. Se considera el punto de entrega al cliente, también cuando la falla corresponde al proveedor de acceso. Los reportes verificables del usuario cuentan aunque no exista monitoreo suficiente.

## Cambios jurídicos delimitados

- Se sustituye la cláusula octava por el compromiso de atención, el inicio del cómputo desde detección o reporte —lo que ocurra primero— y la referencia al apartado de niveles de servicio.
- Se sustituye la cláusula décima tercera para eliminar el umbral de 72 horas. Las interrupciones imputables dan lugar a compensación proporcional y a la bonificación legal aplicable. Para el incumplimiento del SLA dedicado mensual, se conserva el piso contractual del 20% de la facturación mensual del servicio; no se presenta ese piso mensual como una obligación legal universal ni reduce beneficios legales superiores.
- Las demás declaraciones y cláusulas se conservan literalmente respecto de la revisión 2. Esto no constituye una revisión jurídica integral ni certifica la validez del resto del contrato.
- Las exclusiones requieren causa, evidencia y duración. La mera dependencia del carrier, la falta de refacciones o la espera de una visita no eximen automáticamente del SLA.

Fuentes primarias consultadas: [Ley en Materia de Telecomunicaciones y Radiodifusión, artículo 185 XIII](https://www.diputados.gob.mx/LeyesBiblio/pdf/LMTR.pdf) y [Ley Federal de Protección al Consumidor, artículos 92 Bis y 92 Ter](https://www.diputados.gob.mx/LeyesBiblio/pdf/LFPC.pdf). La bonificación legal por deficiencias imputables al proveedor y la garantía contractual de disponibilidad son obligaciones distintas.

## Soporte

WhatsApp NOC **5526014566** es el canal preferente. También se puede abrir un ticket en **soporte@hubnet-solutions.com.mx** o **https://support.hubnet-solutions.net**. El usuario confirmó expresamente el correo con «hubnet-solutions» y dominio `.com.mx`.

## Versionado y despliegue

`ClientSigningSnapshot.CurrentServiceTemplateRevision` pasa de 2 a 3. Solo se añade `Contrato_Servicios_v3.docx`; las plantillas anteriores no se editan. Las instantáneas con revisión 0, 1 o 2 siguen generándose con su propia plantilla, incluso al recibir la segunda firma. Un documento ya emitido sin instantánea conserva el fallback histórico.

La bienvenida solo resume los nuevos niveles si todos los contratos incluidos tienen revisión 3 o posterior y son recurrentes. Para documentos anteriores o combinaciones de revisiones remite a las condiciones de cada contrato, sin prometer automáticamente el SLA nuevo. El contrato y la bienvenida eventuales no cambian.

Desplegar web 2.5.27. No se requiere SQL ni APK. Los documentos existentes no se sustituyen; para adoptar los términos nuevos hay que emitir y formalizar un documento nuevo.
