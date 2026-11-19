"""Build the independent SaaS contract using the established HN document design.

Only this builder and Contrato_Servicios_Web_v1.docx belong to this document.
The Internet templates and their frozen wording are never changed. Runtime
substitutes the public service scope, not private Notes, and removes empty
WEBPLAN/WEBLICENCIAS rows. Render populated examples before delivery.
"""
from copy import deepcopy
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

from lxml import etree as E


BASE = Path(__file__).resolve().parents[2] / "HNControl.Web/assets/legal-templates"
W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
NS = {"w": W}
XML_SPACE = "{http://www.w3.org/XML/1998/namespace}space"

# Approved business policy: monthly service without a mandatory minimum term;
# one year of restricted contractual custody after cancellation, not a claim
# that every category of personal data is legally required to be kept one year.
TERM_TEXT = "La suscripción es mensual y no tiene plazo forzoso: comienza con la activación efectiva y continúa hasta que se solicite su cancelación. Su inicio consta en la carátula; los periodos estimados no crean un plazo mínimo ni una penalización. El cliente puede cancelar conforme a la cláusula siguiente."
EXPORT_TEXT = "Después de la baja, HN conservará durante un año los datos operativos disponibles, con acceso restringido y únicamente para su custodia y recuperación por el cliente. Durante ese periodo el cliente podrá solicitar su exportación por soporte en un formato utilizable que se acuerde según la naturaleza de la información. Esta conservación contractual no mantiene activa la suscripción ni autoriza usos operativos, comerciales o incompatibles con esa finalidad. El plazo no impide atender solicitudes de supresión u otros derechos cuando procedan, ni sustituye los plazos específicos de conservación o bloqueo exigidos por la ley."


def q(name):
    return "{" + W + "}" + name


def text(element):
    return "".join(element.xpath(".//w:t/text()", namespaces=NS))


with ZipFile(BASE / "Contrato_Servicios_v5.docx") as package:
    parts = {name: package.read(name) for name in package.namelist()}
document = E.fromstring(parts["word/document.xml"])
body = document.find(q("body"))
templates = body.findall(q("tbl"))
reference_table = deepcopy(templates[0])
client_table = deepcopy(templates[1])
field_table = deepcopy(templates[2])
fees_table = deepcopy(templates[3])
signature_table = deepcopy(templates[-1])
for node in signature_table.xpath(".//w:t", namespaces=NS):
    if node.text == "RLCLIENTE":
        node.text = "SIGNCLIENTNAME"
section = deepcopy(body.find(q("sectPr")))
for child in list(body):
    body.remove(child)


def paragraph(value, *, heading=False, title=False, page=False, bold=False, size=None, after=100):
    p = E.Element(q("p"))
    pp = E.SubElement(p, q("pPr"))
    if title:
        E.SubElement(pp, q("pStyle")).set(q("val"), "Title")
    if page:
        E.SubElement(pp, q("pageBreakBefore"))
    if heading or title:
        E.SubElement(pp, q("keepNext"))
    E.SubElement(pp, q("widowControl"))
    spacing = E.SubElement(pp, q("spacing"))
    spacing.set(q("before"), "130" if heading else "0")
    spacing.set(q("after"), str(after))
    spacing.set(q("line"), "264")
    spacing.set(q("lineRule"), "auto")
    E.SubElement(pp, q("jc")).set(q("val"), "left" if heading or title else "both")
    r = E.SubElement(p, q("r"))
    rp = E.SubElement(r, q("rPr"))
    fonts = E.SubElement(rp, q("rFonts"))
    fonts.set(q("ascii"), "Arial")
    fonts.set(q("hAnsi"), "Arial")
    if heading or title or bold:
        E.SubElement(rp, q("b"))
    E.SubElement(rp, q("color")).set(q("val"), "125F8C" if heading else "203345")
    E.SubElement(rp, q("sz")).set(q("val"), str(size or (34 if title else 25 if heading else 21)))
    t = E.SubElement(r, q("t"))
    t.set(XML_SPACE, "preserve")
    t.text = value
    body.append(p)
    return p


def rewrite_paragraph(p, value):
    # Table geometry and typographic treatment come directly from the template.
    runs = p.findall(q("r"))
    rp = deepcopy(runs[-1].find(q("rPr"))) if runs else None
    for child in list(p):
        if child.tag != q("pPr"):
            p.remove(child)
    r = E.SubElement(p, q("r"))
    if rp is not None:
        r.append(rp)
    t = E.SubElement(r, q("t"))
    t.set(XML_SPACE, "preserve")
    t.text = value


def fields(rows):
    table = deepcopy(field_table)
    sample = deepcopy(table.find(q("tr")))
    for row in list(table.findall(q("tr"))):
        table.remove(row)
    for label, value in rows:
        row = deepcopy(sample)
        for cell, content in zip(row.findall(q("tc")), (label, value)):
            paragraphs = cell.findall(q("p"))
            rewrite_paragraph(paragraphs[0], content)
            for extra in paragraphs[1:]:
                cell.remove(extra)
        table.append(row)
    body.append(table)
    return table


def clause(number, name, *paragraphs):
    paragraph(f"{number}. {name}", heading=True)
    for value in paragraphs:
        paragraph(value)


# 1. Identification and the commercial terms accepted by both signatories.
paragraph("Contrato de servicios web", title=True)
for node in reference_table.xpath(".//w:t", namespaces=NS):
    if node.text:
        node.text = node.text.replace("EVENTONUMERO", "ANEXONUMERO")
body.append(reference_table)
paragraph("HNNOMBRELEGAL, en adelante HN SOLUTIONS, y el cliente identificado en esta carátula acuerdan la suscripción de la plataforma WEBPLATAFORMA. Este documento reúne la ficha del servicio, sus condiciones de uso y el acuerdo sobre el tratamiento de datos. Cada parte queda representada por la persona que firma al final y declara contar con las facultades necesarias.")
paragraph("Datos del cliente", heading=True)
body.append(client_table)
paragraph("Datos del prestador", heading=True)
fields([
    ("Razón social y RFC", "HNNOMBRELEGAL · HNRFC"),
    ("Domicilio fiscal", "HNDOMICILIO · C. P. HNCP"),
])
paragraph("Suscripción y facturación", heading=True)
fields([
    ("Plataforma", "WEBPLATAFORMA"),
    ("Acceso web", "WEBURL"),
    ("Vigencia", "ANEXOVIGENCIA"),
    ("Periodo de contratación", "PERIODOC"),
    ("Corte y vencimiento", "Corte: día 1. Pago: día 15 del mismo mes. Primera factura contra entrega efectiva del acceso contratado."),
])
paragraph("Cuotas del servicio", heading=True)
fee_values = [
    ("Concepto", "Antes de IVA", "Total con IVA (16%)"),
    ("Suscripción mensual", "$ COSTOCLIENTE MXN", "$ WEBMENSUALTOTAL MXN"),
    ("Alta y configuración inicial", "$ COSTOINST MXN", "$ WEBALTATOTAL MXN"),
]
for row, values in zip(fees_table.findall(q("tr")), fee_values):
    for cell, value in zip(row.findall(q("tc")), values):
        ps = cell.findall(q("p"))
        rewrite_paragraph(ps[0], value)
        for extra in ps[1:]:
            cell.remove(extra)
body.append(fees_table)
paragraph("La cuota de alta es un cargo único. La mensualidad corresponde al alcance y capacidad identificados en la ficha del servicio; no autoriza por sí misma cargos automáticos. Los importes adicionales requieren aceptación expresa antes de prestarse o cobrarse.", size=19)

# 2. Public scope only. No free-form internal contract notes are used here.
paragraph("Ficha del servicio web", title=True, page=True)
paragraph("Contrato ANEXONUMERO · Cliente RSCLIENTE", size=19)
fields([
    ("Plataforma y acceso", "WEBPLATAFORMA · WEBURL"),
    ("Plan contratado", "WEBPLAN"),
    ("Usuarios o unidades incluidos", "WEBLICENCIAS"),
    ("Horario de atención", "WEBHORARIO"),
])
paragraph("Funciones y entregables incluidos", heading=True)
paragraph("WEBALCANCE")
clause("PRIMERA", "Objeto y alcance", "HN SOLUTIONS proporcionará acceso a la plataforma en modalidad de software como servicio, con las funciones, usuarios o unidades y entregables expresamente descritos en esta ficha. El alcance se determina por lo contratado; la publicación de una función en otro plan no la incorpora automáticamente a esta suscripción.", "El servicio web no incluye por sí mismo equipos, instalación física, tarjetas SIM, planes de datos, acceso a Internet, licencias de mapas ni otros servicios de terceros. Solo se incluyen cuando la ficha los identifica y precisa su costo o integración. Si HN contrata a terceros para cumplir una prestación incluida, conserva frente al cliente las obligaciones que asumió.")
clause("SEGUNDA", "Licencia y cuentas de acceso", "Durante la vigencia se concede al cliente un derecho no exclusivo de acceso y uso de las funciones contratadas, para sus operaciones y personas autorizadas. La suscripción no transmite la propiedad intelectual del software, su código fuente ni derechos de distribución o reventa. Los datos aportados por el cliente conservan su titularidad y los derechos que legalmente correspondan a sus titulares.", "El cliente designará a sus administradores, mantendrá actualizados los permisos y protegerá sus credenciales. No compartirá accesos fuera de las personas autorizadas ni eludirá los límites contratados. HN aplicará permisos de acceso conforme al servicio y permitirá corregir o revocar autorizaciones por los canales de soporte.")
clause("TERCERA", "Activación y cambios", "La entrega se acredita mediante la habilitación efectiva del acceso y de las funciones contratadas, con comunicación al cliente y constancia del alta. Una fecha programada o una cuenta sin las funciones convenidas no acreditan por sí mismas la entrega completa. Las configuraciones o migraciones incluidas se realizarán conforme al alcance aceptado.", "HN podrá corregir errores, reforzar la seguridad y actualizar la plataforma. Las mejoras no deberán eliminar ni reducir sustancialmente las funciones contratadas. Los cambios de alcance, precio o condiciones requerirán información previa y el consentimiento que legalmente corresponda; una actualización no autoriza cargos nuevos ni sustituye unilateralmente este contrato.")

# 3. Operational care, with no borrowed Internet availability or repair SLA.
paragraph("Operación y soporte", title=True, page=True)
clause("CUARTA", "Canales y horario", "El soporte del servicio se atiende en el horario WEBHORARIO. El cliente puede abrir un ticket por WhatsApp al 5526014566, por correo a soporte@hubnet-solutions.com.mx o en https://support.hubnet-solutions.net. El NOC coordinará el seguimiento y, cuando corresponda, escalará el caso a personal especializado.", "La recepción técnica de un mensaje o un acuse automático no equivalen a atención humana inmediata ni amplían el horario contratado. HN registrará el reporte, informará su seguimiento y comunicará el diagnóstico o la información disponible y las acciones previstas según el impacto. No es necesario duplicar el reporte por distintos medios. Si el portal no está disponible, WhatsApp y correo siguen habilitados para presentar solicitudes.")
clause("QUINTA", "Continuidad y mantenimiento", "HN realizará las actividades razonables de operación, mantenimiento y corrección necesarias para prestar el servicio conforme a lo convenido. Cuando un mantenimiento previsto pueda afectar el uso, informará su alcance y ventana con la anticipación razonablemente posible. Las intervenciones urgentes de seguridad se comunicarán tan pronto resulte viable, sin revelar información que agrave el riesgo.", "Este contrato no establece un porcentaje de disponibilidad ni un tiempo universal de recuperación de la plataforma. Eso no elimina la obligación de atender fallas, cumplir el alcance contratado y aplicar las compensaciones o remedios que procedan. HN no presentará intervalos sin medición como funcionamiento comprobado ni excluirá por el solo hecho de involucrar a terceros una falla de una prestación a su cargo.")
clause("SEXTA", "Uso de Checkin", "Cuando la plataforma contratada sea Checkin, sus registros y reportes son herramientas de administración y consulta. El cliente revisará la configuración de horarios, usuarios, permisos y reglas; la plataforma no sustituye sus obligaciones laborales, fiscales ni la revisión humana de incidencias y decisiones sobre su personal.", "La captura de fotografías, ubicación o datos biométricos solo procederá si la función está contratada y habilitada expresamente para una finalidad lícita, necesaria y proporcional. El cliente determinará y documentará la base que permita ese tratamiento, entregará los avisos y obtendrá los consentimientos exigibles. Esta firma no equivale al consentimiento de sus empleados ni autoriza por sí misma todas esas funciones.")
clause("SÉPTIMA", "Uso de GPS e integraciones", "Cuando la plataforma contratada sea GPS, la posición y los eventos dependen del equipo, energía, señal, cobertura y servicios efectivamente incluidos. Los reportes deben interpretarse conforme a su fecha y disponibilidad de datos. La plataforma no es un seguro, una garantía antirrobo ni una promesa de recuperación física de vehículos o bienes.", "El cliente usará la localización únicamente respecto de personas, vehículos o bienes sobre los que tenga atribuciones legítimas y cumplirá sus obligaciones de información y privacidad. Las integraciones con mapas, comunicaciones u otras plataformas se sujetan al alcance informado; HN identificará las dependencias relevantes y no trasladará al cliente su responsabilidad por prestaciones que haya asumido o subcontratado.")

# 4. Roles and instructions for client-controlled operational personal data.
paragraph("Datos y seguridad", title=True, page=True)
clause("OCTAVA", "Roles y finalidades", "El cliente determina las finalidades de los datos de empleados, conductores, usuarios u otras personas que incorpore a la plataforma y actúa como responsable de ese tratamiento. HN los tratará como encargado, conforme a las instrucciones documentadas del cliente, únicamente para prestar y proteger el servicio contratado y atender las obligaciones aplicables.", "HN actúa como responsable independiente de los datos que necesita para sus propias finalidades de contratación, facturación y administración de soporte. Estos tratamientos se informarán mediante los avisos de privacidad correspondientes. La aceptación del contrato no autoriza la venta ni la reutilización de datos operativos del cliente para finalidades incompatibles.", "El cliente proporcionará datos lícitos, pertinentes y proporcionales al servicio, informará a sus titulares y atenderá las solicitudes que le correspondan. HN colaborará para que pueda cumplirlas. Para tratamientos propios de HN, las personas podrán ejercer sus derechos por los medios del aviso aplicable, sin perjuicio de los medios y plazos legales. Los avisos publicados se consultan, según la plataforma, en https://checkin.hubnet-solutions.net/privacidad o https://gps.hubnet-solutions.com.mx/privacidad. Para otra plataforma, HN proporcionará el aviso correspondiente por los canales de soporte antes de tratar los datos; estos avisos no sustituyen el que deba entregar el cliente a sus empleados o conductores.")
clause("NOVENA", "Seguridad y confidencialidad", "HN aplicará medidas administrativas, técnicas y físicas adecuadas a los datos y riesgos del servicio; limitará el acceso al personal y proveedores que lo necesiten y los sujetará a deberes de confidencialidad. El cliente protegerá sus cuentas y equipos, limitará privilegios y notificará oportunamente accesos no autorizados o uso indebido del que tenga conocimiento.", "La información operativa, técnica y comercial que las partes conozcan al ejecutar este contrato será confidencial. Solo se utilizará para cumplir sus obligaciones y podrá comunicarse a personas autorizadas o cuando exista una obligación legal, con las salvaguardas aplicables. La confidencialidad subsiste después de terminar la relación.", "HN informará sin demora al cliente las vulneraciones de seguridad que afecten sus datos y cooperará para contenerlas, documentarlas y atender los derechos y notificaciones legales que correspondan. La información se actualizará según se conozcan hechos verificables; no se establece una certificación, localización de alojamiento o recuperación garantizada que no se haya acordado expresamente.")
clause("DÉCIMA", "Proveedores y recuperación de información", "Los proveedores que traten información por cuenta de HN quedarán sujetos a instrucciones, confidencialidad y medidas de seguridad acordes con el servicio. HN informará al cliente cuáles intervienen y las finalidades aplicables, y observará las autorizaciones y requisitos que correspondan a cada remisión o transferencia. Su intervención no libera a HN de sus obligaciones.", "El cliente podrá solicitar acceso y recuperación de su información conforme a las funciones contratadas y por los canales de soporte. HN facilitará los datos disponibles en un formato utilizable que se acuerde según su naturaleza, sin condicionar el ejercicio de derechos personales a la continuidad comercial de la suscripción.")

# 5. Commercial provisions with the approved monthly and custody policies.
commercial_start = len(body)
paragraph("Facturación y terminación", title=True, page=True)
clause("DÉCIMA PRIMERA", "Cuotas y pagos", "Las cuotas son las de la carátula. El corte mensual es el día 1 y el pago vence el día 15 del mismo mes; la primera factura se paga contra entrega efectiva del acceso contratado. Cuando la activación ocurra después de iniciado el mes, esa primera factura incluirá la parte proporcional del servicio efectivamente prestado hasta el cierre del mes. La cuota de alta y configuración se cobra una sola vez cuando esté expresamente indicada.", "Los cargos automáticos solo procederán con autorización expresa e informada separada que identifique importe, fecha y periodicidad. La mensualidad no constituye por sí misma esa autorización. Los importes no previstos requieren consentimiento previo. Las aclaraciones de facturación se tramitarán por los canales de soporte y los cargos indebidos se corregirán conforme a la ley.")
clause("DÉCIMA SEGUNDA", "Vigencia y continuidad", TERM_TEXT, "Si existe una renovación automática, HN dará aviso al menos cinco días naturales antes y permitirá cancelarla sin penalización por los medios habilitados. El aviso no sustituye el consentimiento para cobros automáticos ni permite cambiar unilateralmente el precio o el alcance.")
clause("DÉCIMA TERCERA", "Cancelación y salida", "El cliente podrá solicitar la cancelación por los canales electrónicos de soporte. La solicitud tendrá efecto inmediato y se entregará constancia, sin condicionar su recepción al pago de adeudos. Los saldos legítimos por servicios efectivamente prestados se resolverán por separado; no se cobrará por periodos posteriores a la cancelación. Los ajustes o devoluciones que correspondan se calcularán conforme a la prestación efectiva, las cantidades pagadas y la legislación aplicable.", EXPORT_TEXT, "Al concluir el año de custodia se revisarán las obligaciones de conservación aplicables y se tramitará la supresión o disociación de lo que ya no deba conservarse. Cualquier retención adicional quedará limitada a su finalidad legítima y con bloqueo cuando proceda. La terminación comercial no extingue los derechos de acceso, rectificación, cancelación u oposición, ni autoriza a vender o reutilizar los datos. Las obligaciones fiscales o probatorias se cumplirán conservando únicamente lo necesario.")
clause("DÉCIMA CUARTA", "Incumplimiento y remedios", "Cada parte responderá por los incumplimientos que le sean imputables. Antes de suspender por un adeudo vencido no controvertido, HN lo notificará y permitirá su aclaración o regularización; cualquier restricción será proporcional, respetará los derechos aplicables y no impedirá solicitar la cancelación o la recuperación legítima de información. Las medidas urgentes para contener un riesgo de seguridad deberán limitarse a lo necesario y comunicarse al cliente.", "Por incumplimientos imputables a HN procederán la corrección, el reintegro y las compensaciones o bonificaciones exigibles. Cuando aplique la Ley Federal de Protección al Consumidor, se respetará la bonificación mínima de sus artículos 92 Bis y 92 Ter. No hay exención absoluta por fallas de proveedores ni renuncia general a reclamar daños o acudir a la autoridad competente.")

# Keep this dense section on one page without reducing type or line height.
# Three-point paragraph gaps retain visible separation and avoid a two-line
# overflow page before the separately paginated signatures.
for p in list(body)[commercial_start:]:
    spacing = p.find(q("pPr")).find(q("spacing"))
    spacing.set(q("after"), "60")
    if spacing.get(q("before")) == "130":
        spacing.set(q("before"), "100")

# 6. Final terms and the same autograph/digital evidence structure as v5.
paragraph("Aceptación y firmas", title=True, page=True)
clause("DÉCIMA QUINTA", "Comunicaciones y cambios", "Las comunicaciones contractuales se enviarán a los correos registrados, conservando constancia. Las solicitudes de soporte, aclaración o cancelación podrán presentarse por los canales de este contrato; surtirán efectos conforme a su recepción y a las reglas legales aplicables. Cada parte mantendrá actualizados sus contactos y las personas autorizadas.", "La carátula y la ficha del servicio forman parte del mismo acuerdo. Cualquier cambio posterior que requiera consentimiento deberá quedar documentado y aceptado, sin sustituir retroactivamente el documento firmado. No se incorporan condiciones ocultas ni notas internas de operación.")
clause("DÉCIMA SEXTA", "Legislación y controversias", "Este contrato se rige por la legislación mexicana aplicable a los servicios web, las relaciones de consumo cuando correspondan y la protección de datos personales. Se conservan las competencias de PROFECO, de la autoridad competente en materia de datos personales y de los tribunales legalmente competentes. Ninguna cláusula implica renuncia general a los derechos de las partes o de los titulares de datos.")
paragraph("Las partes declaran haber revisado la identificación, plataforma, alcance, capacidad, cuotas, vigencia y condiciones de este contrato. La firma autógrafa capturada y el registro electrónico de cada firmante se incorporan a continuación; ambas partes recibirán el documento final con las firmas registradas.")
paragraph("Contrato: ANEXONUMERO", bold=True)
body.append(signature_table)
paragraph("Soporte: WhatsApp 5526014566 · soporte@hubnet-solutions.com.mx · https://support.hubnet-solutions.net", size=18, after=0)

body.append(section)
parts["word/document.xml"] = E.tostring(document, xml_declaration=True, encoding="UTF-8", standalone=True)
for name, raw in list(parts.items()):
    if not (name.startswith("word/header") or name.startswith("word/footer")) or not name.endswith(".xml"):
        continue
    root = E.fromstring(raw)
    for node in root.xpath("//w:t", namespaces=NS):
        if node.text:
            node.text = node.text.replace("SERVICIOS DE TELECOMUNICACIONES", "SERVICIOS WEB Y PLATAFORMAS")
            node.text = node.text.replace("Contrato de servicios", "Contrato de servicios web")
    parts[name] = E.tostring(root, xml_declaration=True, encoding="UTF-8", standalone=True)

with ZipFile(BASE / "Contrato_Servicios_Web_v1.docx", "w", ZIP_DEFLATED) as package:
    for name, raw in parts.items():
        package.writestr(name, raw)
print("Created Contrato_Servicios_Web_v1.docx")
