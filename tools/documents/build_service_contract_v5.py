"""Build the simplified recurring contract v5 from immutable v4.

The prior DOCX and builder remain unchanged. Runtime removes optional annex
rows when their existing service fields are empty. Render filled examples for
visual review after changing this template.
"""
from copy import deepcopy
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

from lxml import etree as E


BASE = Path(__file__).resolve().parents[2] / "HNControl.Web/assets/legal-templates"
W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
NS = {"w": W}


def q(name):
    return "{" + W + "}" + name


def text(element):
    return "".join(element.xpath(".//w:t/text()", namespaces=NS))


with ZipFile(BASE / "Contrato_Servicios_v4.docx") as package:
    parts = {name: package.read(name) for name in package.namelist()}
document = E.fromstring(parts["word/document.xml"])
body = document.find(q("body"))


def find(prefix):
    return next(p for p in body.findall(q("p")) if text(p).startswith(prefix))


def replace(paragraph, value):
    """Preserve paragraph geometry and clause heading/body typography."""
    runs = paragraph.findall(q("r"))
    body_style = deepcopy(runs[-1].find(q("rPr"))) if runs else None
    heading_style = deepcopy(runs[0].find(q("rPr"))) if runs else None
    for child in list(paragraph):
        if child.tag != q("pPr"):
            paragraph.remove(child)

    def append_run(content, style):
        run = E.SubElement(paragraph, q("r"))
        if style is not None:
            run.append(style)
        node = E.SubElement(run, q("t"))
        node.set("{http://www.w3.org/XML/1998/namespace}space", "preserve")
        node.text = content

    if " - " in value and value.split(".")[0].isupper():
        heading, value = value.split(" - ", 1)
        append_run(heading + " - ", heading_style)
    append_run(value, body_style)


def change(prefix, value):
    replace(find(prefix), value)


# Identify each signatory without asserting an unverified notarial instrument.
change("CONTRATO DE PRESTACIÓN", "CONTRATO DE PRESTACIÓN DE SERVICIOS DE TELECOMUNICACIONES Y REDES QUE CELEBRAN POR UNA PARTE HNNOMBRELEGAL, EN ADELANTE HN SOLUTIONS, REPRESENTADA POR LA PERSONA QUE SUSCRIBE EL APARTADO FINAL DEL PROVEEDOR, Y POR LA OTRA PARTE EL USUARIO IDENTIFICADO EN LA CARÁTULA, CONFORME A LAS DECLARACIONES Y CLÁUSULAS DE ESTE DOCUMENTO.")
body.remove(find("Que con fecha 06 de mayo de 2020"))
change("Que la modalidad de prestación", "Que presta los servicios contratados bajo la denominación comercial HN SOLUTIONS y podrá utilizar infraestructura propia o de terceros, conforme a las autorizaciones que legalmente correspondan, conservando su responsabilidad frente al usuario. Este documento no acredita por sí mismo una concesión, autorización ni registro, ni presenta como otorgado un trámite pendiente.")
change("Que cuenta con las facultades", "Que cuenta con capacidad para obligarse o con facultades vigentes para representar al usuario, según corresponda. Cuando actúe en representación de otra persona, acreditará esa calidad mediante la documentación aplicable a su tipo de persona y al acto que celebra; no se presume la existencia de datos notariales o registrales en la carátula.")

# A conditional checklist applies to both individuals and corporate customers.
change("Documentación requerida", "Documentación según el tipo de persona")
checklist = next(table for table in body.findall(q("tbl")) if "☐" in text(table))
items = [
    "☐  Identificación oficial de quien firma",
    "☐  Comprobante de domicilio",
    "☐  Constancia de situación fiscal",
    "☐  Acta constitutiva (persona moral)",
    "☐  Documento que acredita facultades (cuando exista representación)",
]
for paragraph, value in zip(checklist.xpath(".//w:tc/w:p", namespaces=NS), items):
    replace(paragraph, value)
change("El Usuario manifiesta bajo protesta", "El Usuario declara que los datos de su identificación y contratación son correctos. Proporcionará los documentos de esta lista que correspondan a su tipo de persona y representación; no se exigirán documentos corporativos a quien contrate por derecho propio como persona física. Ambas partes aceptan la carátula, el Anexo A y las cláusulas mediante las firmas al final del contrato.")

# The annex is an integrated summary of existing service data, not a separate
# invented specification or a second set of operational configuration fields.
body.remove(find("ANEXOESTADO"))
change("Ficha técnica y modalidad", "Ficha del servicio contratado")
annex = next(table for table in body.findall(q("tbl")) if "ANEXORED" in text(table))
row_sample = deepcopy(annex.find(q("tr")))
for row in list(annex.findall(q("tr"))):
    annex.remove(row)
rows = [
    ("Servicio y sede", "ANEXOSERVICIO"),
    ("Dirección del servicio", "ANEXODIRECCION"),
    ("Modalidad contratada", "ANEXOMODALIDAD"),
    ("Velocidad contratada de bajada", "ANEXOBAJADA"),
    ("Subida y simetría acordadas", "ANEXOSUBIDA · ANEXOSIMETRIA"),
    ("Vigencia y plazo", "ANEXOVIGENCIA · PERIODOC"),
    ("Instalación o activación pactada", "ANEXOINSTALACION"),
    ("Cargos antes de IVA", "Mensualidad: $ COSTOCLIENTE MXN · Instalación: $ COSTOINST MXN"),
    ("Título y titular declarados", "ANEXOTITULO · ANEXOTITULAR"),
    ("Registro del modelo PROFECO", "ANEXOPROFECO"),
]
for label, value in rows:
    row = deepcopy(row_sample)
    for cell, content in zip(row.findall(q("tc")), (label, value)):
        paragraphs = cell.findall(q("p"))
        replace(paragraphs[0], content)
        for extra in paragraphs[1:]:
            cell.remove(extra)
    annex.append(row)
change("Los campos no especificados", "Esta ficha reúne los datos particulares del servicio identificado en la carátula. Los niveles de servicio, el cálculo de disponibilidad y los canales de atención de las páginas siguientes forman parte del mismo Anexo A y se aceptan con las firmas finales de ambas partes. Las referencias regulatorias que se incluyan deben corresponder a documentos vigentes verificables.")
change("Las velocidades numéricas", "Las velocidades se expresan en megabits por segundo (Mbps). La velocidad de subida y la simetría solo se comprometen cuando se indiquen expresamente. Los equipos entregados, su identificación y su propiedad se documentan en la orden de servicio; cualquier financiamiento requiere un desglose aceptado.")
change("El corte es el día 1", "El corte es el día 1 y el pago posterior a la primera factura vence el día 15, conforme a la carátula. La primera factura se paga contra la entrega efectiva del servicio.")
change("Anexo de Servicios.", "Anexo de Servicios. - Es el Anexo A incorporado a este mismo contrato e identificado con su número. Reúne la ficha del servicio, los niveles comprometidos, el cálculo de disponibilidad, la consulta de monitoreo e informes y los canales de soporte y escalamiento. Ambas partes lo aceptan con las firmas finales; no depende de un documento externo ausente.")
change("Especificaciones Técnicas.", "Especificaciones Técnicas. - Son las características del servicio, su capacidad y los niveles de calidad expresamente acordados en la carátula y el Anexo A, sin perjuicio de los parámetros mínimos que resulten exigibles conforme a la normativa aplicable.")
change("TERCERA.", "TERCERA. SOLICITUD Y ANEXO DEL SERVICIO. - Antes de la aceptación, HN SOLUTIONS entregará al usuario el contrato con su Anexo A integrado. El servicio, la sede, la capacidad, la modalidad, la vigencia y los cargos serán los expresamente acordados. La instalación se coordinará con el usuario conforme al párrafo siguiente. Toda modificación de las condiciones contratadas requerirá información previa y consentimiento expreso en los términos legales aplicables; cuando proceda, se notificará con al menos treinta días de anticipación. Ningún campo pendiente constituye una prestación o cargo aceptado.")
change("CUARTA.", "CUARTA. CONDICIONES PARA LA PRESTACIÓN DEL SERVICIO. - HN SOLUTIONS coordinará la instalación en la sede contratada y realizará las pruebas que correspondan al servicio. El inicio efectivo de su prestación se documentará en la orden de servicio junto con los resultados disponibles y la entrega al usuario o a su representante autorizado. Los cargos se generarán desde la prestación efectiva y documentada; la fecha programada o la falta de firma no acreditan por sí mismas la activación ni implican aceptación definitiva. Los cambios de características o de ubicación solicitados por el usuario estarán sujetos a factibilidad, cotización previa y consentimiento expreso sobre su alcance y costo antes de ejecutarse.")
payment = find("HN SOLUTIONS facturará a")
replace(payment, text(payment).split('Si “EL USUARIO” firmara')[0] + "Si la activación ocurre después de iniciado el mes, la primera factura incluirá únicamente la parte proporcional desde la prestación efectiva hasta el cierre de ese mes y será pagadera contra entrega del servicio. Los periodos posteriores seguirán el corte del día 1 y vencimiento del día 15.")
change("DÉCIMA.", "DÉCIMA. RESPONSABILIDAD DEL USUARIO. - El usuario se abstendrá de conectar a los equipos de HN SOLUTIONS dispositivos que puedan dañarlos y utilizará los equipos conforme al servicio contratado. Responderá por los daños que legalmente le sean imputables, sin que ello autorice la terminación inmediata o la imposición automática de cargos. Cualquier rescisión por incumplimiento se sujetará a la notificación y procedimiento de la cláusula décima séptima.")
change("Para efectos de esta cláusula", "HN SOLUTIONS informará al usuario las conductas, datos y circunstancias que sustenten un posible incumplimiento y le permitirá aportar aclaraciones y, cuando proceda, subsanarlo dentro de los plazos de la cláusula décima séptima. Se conservan las medidas de protección y suspensión legalmente procedentes conforme a la cláusula décima cuarta y los medios de defensa del usuario.")
force_majeure = find("DECIMA SEGUNDA.")
replace(force_majeure, (text(force_majeure).split("excepto la de pagar")[0].rstrip(", ") + ". Subsistirá únicamente el pago de los servicios efectivamente prestados y demás saldos legalmente exigibles conforme a la cláusula décima sexta, aplicando los ajustes y derechos que correspondan.").replace(" -Ninguna", " - Ninguna"))
force_heading = force_majeure.find(q("r"))
if force_heading.find(q("rPr")) is not None:
    force_heading.remove(force_heading.find(q("rPr")))
force_heading.insert(0, deepcopy(find("DÉCIMA SÉXTA.").find(q("r")).find(q("rPr"))))
rescission = find("DÉCIMA SÉPTIMA.")
replace(rescission, text(rescission).replace("cualquier cantidad vencida y no pagada derivada de la prestación de Servicios", "cantidades vencidas, exigibles y no controvertidas, respetando las protecciones y procedimientos de la cláusula décima cuarta"))

# Keep SLA commitments and calculations, with a service/site scope rather than
# requiring a customer to fill internal network measurement configuration.
change("Internet Dedicado:", "Internet Dedicado: disponibilidad mínima mensual del 99.85% del servicio en la sede contratada. La capacidad y, cuando se indiquen, la velocidad de subida y la simetría serán las registradas en la ficha del servicio. La disponibilidad no se limita al funcionamiento de la red interna de HN SOLUTIONS.")
change("Los minutos evaluables", "Los minutos evaluables son los minutos del periodo activo menos las exclusiones justificadas indicadas abajo. Los minutos de interrupción computable son aquellos en que no se puede utilizar el servicio de acceso a Internet en la sede contratada por una falla dentro del alcance de HN SOLUTIONS, incluidos los servicios e infraestructura de terceros que utilice. El cómputo termina al verificar el restablecimiento; cerrar administrativamente el ticket no modifica la duración real.")
change("NOC: recibe el ticket", "NOC: recibe el ticket y asigna folio; en Dedicado la primera respuesta humana tiene un máximo de cuatro horas. Ingeniería: objetivo de escalamiento al persistir la interrupción una hora, o de inmediato al identificar necesidad de intervención especializada. Operaciones: objetivo de intervención ante riesgo de incumplimiento del SLA o de los tiempos de atención, sin esperar a agotar cuatro horas. El NOC coordina y comunica estos niveles por los canales indicados; el usuario no necesita duplicar el reporte ni gestionar la falla con terceros.")

# The existing support portal is the customer-facing access confirmed by HN.
change("Informes y acceso a evidencia", "Monitoreo, informes y evidencia")
change("El NOC conservará", "El usuario puede consultar el monitoreo de su servicio, los tickets y los informes en https://support.hubnet-solutions.net con su acceso de cliente. HN SOLUTIONS pondrá a su disposición el informe mensual dentro de los primeros cinco días hábiles del mes siguiente. Podrá solicitar gratuitamente copias o extractos de su servicio por cualquiera de los canales de soporte y recibirlos dentro de cinco días hábiles, mediante el portal, correo o ticket.")
change("El informe indicará", "El informe identificará el servicio y el periodo, los minutos evaluables, las interrupciones computables, el porcentaje de disponibilidad, las exclusiones justificadas y la evidencia disponible, así como los tickets y tiempos de atención y restablecimiento. Las mediciones de calidad efectivamente realizadas indicarán fuente, ubicación, método y periodo de medición. Los periodos sin datos se identificarán expresamente y no se presentarán como servicio disponible. Los registros del NOC podrán contrastarse con reportes y pruebas del usuario; el informe puede objetarse por cualquier canal de soporte sin limitar las vías ni los plazos legales de reclamación.")
portal_fallback = deepcopy(find("El informe identificará"))
replace(portal_fallback, "Si el portal no está disponible, el usuario podrá abrir y seguir sus reportes por WhatsApp o correo y solicitar por esos medios los informes o extractos. La indisponibilidad del portal no reinicia los tiempos de atención, no impide presentar evidencia y no elimina los compromisos del servicio.")
find("El informe identificará").addnext(portal_fallback)
missing_measurement = deepcopy(portal_fallback)
replace(missing_measurement, "Los intervalos sin medición se contrastarán con las demás evidencias disponibles, incluidos los reportes del usuario, sin presumir continuidad del servicio. Si no es posible determinar su estado, el informe identificará esa limitación y no presentará un porcentaje definitivo de disponibilidad hasta aclararla; ello no suspende la atención ni los derechos de compensación aplicables.")
find("Los minutos evaluables").addnext(missing_measurement)
change("Los registros del centro de gestión", "Los registros del centro de gestión de red son evidencia técnica consultable y susceptible de contraste; no son prueba indubitable. El acceso al monitoreo, la disponibilidad del informe mensual, las copias o extractos gratuitos y el derecho a objetar la evidencia se rigen por el apartado «Monitoreo, informes y evidencia» del Anexo A. No se exige al usuario acceso a sistemas internos de otros operadores ni se le impide presentar sus propias pruebas.")
change("Entendido el alcance", "Entendido el alcance de este contrato y de su Anexo A integrado, ambas partes manifiestan su aceptación mediante las firmas que constan a continuación. Cualquier modificación posterior se formalizará por acuerdo expreso; el documento firmado se conservará sin sustituir sus condiciones.")

# Use the space gained by the shorter service sheet for the client-facing
# reports section. Keep the SLA calculation/exclusions together on their page.
report_anchor = find("El corte es el día 1")
for paragraph in [find("Monitoreo, informes y evidencia"),
                  find("El usuario puede consultar el monitoreo"),
                  find("El informe identificará"), portal_fallback]:
    body.remove(paragraph)
    report_anchor.addnext(paragraph)
    report_anchor = paragraph

# The older template forced a page before clause 24; expanded preceding clauses
# otherwise leave a nearly empty page. Keep the final clauses and signatures in
# normal flow, preserving the signature table's existing keep-together rules.
for page_break in find("VIGÉSIMA CUARTA.").xpath("./w:pPr/w:pageBreakBefore", namespaces=NS):
    page_break.getparent().remove(page_break)

parts["word/document.xml"] = E.tostring(
    document, xml_declaration=True, encoding="UTF-8", standalone=True
)
with ZipFile(BASE / "Contrato_Servicios_v5.docx", "w", ZIP_DEFLATED) as package:
    for name, content in parts.items():
        package.writestr(name, content)
print("Created Contrato_Servicios_v5.docx; v4 unchanged")
