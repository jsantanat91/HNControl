# Contratos de servicios web mensuales · Web 2.5.30

## Uso en HN Control

1. En Clientes, abra la ficha y agregue o edite un servicio contratado.
2. En **Formato del contrato**, seleccione **Servicios web mensuales**.
3. Elija Check-in, HN Tracking GPS u otra plataforma. Capture el alcance que realmente incluye la suscripción, su mensualidad antes de IVA, fecha de inicio y horario de soporte acordado. Plan y cantidad/unidad de licencias son opcionales.
4. En **Contrato / NDA**, seleccione ese servicio. El formulario indica el formato que va a generar. La emisión exige los datos esenciales y la identidad fiscal de HN configurada.
5. Revise el PDF, solicite ambas firmas y use el flujo existente de bienvenida. El correo específico usa la información congelada del documento, sin contraseñas ni notas internas.

Los tipos Internet y Eventos conservan sus formatos. El selector determina la variante del servicio; no convierte documentos ya emitidos. Un contrato histórico mantiene su plantilla y sus datos congelados aunque posteriormente cambie el servicio. Una nueva emisión o regeneración explícita de un documento sin firmas toma los datos actuales conforme al flujo existente.

## Origen de los datos

| Documento | Fuente |
| --- | --- |
| Cliente, representante, RFC y domicilio | Ficha del cliente |
| Razón social, RFC, domicilio y CP de HN | Configuración y snapshot al emitir |
| Servicio, contrato, inicio, mensualidad y alta/configuración | Servicio contratado y costo de instalación existente |
| Plataforma, URL, plan, alcance, licencias, soporte | Campos propios de la variante web |
| Nombres, trazos, fecha y registro digital de firmas | Flujo de firma existente |

Se guarda `VarianteContrato=WebMensual` junto con los metadatos existentes; no se agrega una tabla ni una columna. Para esta variante, la recurrencia es mensual, sin plazo forzoso ni vencimiento fijo. Los datos técnicos ocultos de un servicio previamente existente se conservan al editarlo, pero no se imprimen en el contrato web.

La URL exige HTTPS sin credenciales, parámetros ni fragmentos. Las direcciones precargadas son públicas. Las funciones descritas en las páginas comerciales no se incorporan automáticamente: el alcance lo captura HN y lo acepta el cliente.

## Condiciones de la nueva plantilla

- Licencia de uso de la plataforma durante la suscripción, sin cesión del código o de la propiedad intelectual de HN. Los datos del cliente siguen siendo suyos.
- Mensual sin plazo forzoso; corte día 1 y pago día 15. Primera factura desde activación efectiva y prorrateo cuando corresponda. Desglose antes de IVA y total con tasa general de 16%.
- Renovación mensual, aviso previo de al menos cinco días naturales y cancelación inmediata por medios electrónicos. Contratar no autoriza por sí mismo cargos automáticos a una tarjeta o cuenta.
- Soporte por WhatsApp **5526014566**, **soporte@hubnet-solutions.com.mx** y **https://support.hubnet-solutions.net**, dentro del horario capturado. No traslada al software el SLA de Internet de 99.85% ni el máximo de respuesta de cuatro horas.
- Check-in: el cliente debe definir usos laborales lícitos, avisos y permisos aplicables. GPS: alcance sujeto a equipos, conectividad e integraciones contratadas; no se promete recuperación de vehículos ni se incluye hardware por defecto.
- Para datos operativos, se distinguen las instrucciones del cliente como responsable y HN como encargado. HN conserva sus obligaciones propias sobre facturación y administración. Se contemplan confidencialidad, seguridad, incidentes, subencargados y exportación.
- **Un año de conservación después de la baja**, solicitado por el usuario, se formula como plazo contractual de custodia restringida y exportación. No afirma que exista una obligación legal general de conservar todos los datos un año. Respeta supresión cuando proceda, minimización y plazos específicos de conservación legal.

## Alcance operativo y revisión jurídica

La plantilla crea obligaciones contractuales. HN Control no administra los datos de Check-in/GPS y este cambio no implementa en esas plataformas procesos de borrado, respaldos, exportación ni avisos automáticos de renovación. HN debe aplicar esos procesos en la operación de cada servicio. No se enviaron correos ni se modificaron clientes reales al preparar los ejemplos.

El aviso público de GPS presenta a HN como responsable en términos generales; debe armonizarse en su propio proyecto con la distinción de roles del contrato. Los avisos vigentes de Check-in y GPS tienen sus propios contactos de privacidad, distintos del NOC; no se sustituyen por el correo de soporte operativo. El formato no acredita certificaciones, registros ni garantiza por sí solo cumplimiento legal integral. Conviene revisión jurídica de la versión definitiva y de su aplicación a cada plataforma.

Fuentes oficiales consultadas el 19/11/2026:

- [LFPC, reforma del artículo 76 Bis sobre cargos recurrentes y renovación](https://sidof.segob.gob.mx/notas/docFuente/5775999): consentimiento, aviso de cinco días y cancelación inmediata.
- [Ley Federal de Protección de Datos Personales en Posesión de los Particulares](https://www.diputados.gob.mx/LeyesBiblio/pdf/LFPDPPP.pdf): finalidad, proporcionalidad, avisos, seguridad, supresión y conservación aplicable; no impone un año universal.
- [Reglamento, relación responsable–encargado y servicios en nube](https://www.ordenjuridico.gob.mx/Documentos/Federal/pdf/wo88475.pdf), artículos 50–54, en lo compatible con la legislación vigente.
- [Ley Federal de Protección al Consumidor](https://www.diputados.gob.mx/LeyesBiblio/pdf/LFPC.pdf): se preservan derechos y compensaciones aplicables por deficiencias, incluidos artículos 92 Bis y 92 Ter.
- [SAT, artículo 1 de la Ley del IVA](https://wwwmat.sat.gob.mx/articulo/19848/articulo-1): tasa general del 16%.
- Referencias públicas: [Check-in](https://checkin.hubnet-solutions.net/), [su aviso](https://checkin.hubnet-solutions.net/privacidad), [GPS](https://gps.hubnet-solutions.com.mx/) y [su aviso](https://gps.hubnet-solutions.com.mx/privacidad). No se accedió a datos privados de estas plataformas.

## Validación y despliegue

La suite `dotnet run --project HNControl.Web.Tests -- --web-contract` verifica metadatos, handlers de alta/edición, rutas de plantillas, snapshots, importes, campos opcionales, firmas y bloqueo de emisión incompleta. Produce ejemplos con identidades, importes, alcance y firmas de demostración, que no constituyen ofertas ni contratos reales.

Plantilla versionada: `Contrato_Servicios_Web_v1.docx`. Desplegar únicamente HNControl.Web 2.5.30; no requiere SQL ni APK. Las plantillas anteriores permanecen disponibles para regeneración histórica.
