"""Build the v4 recurring contract from immutable v3, preserving package assets.

Run with Python + lxml. Review the rendered DOCX after any wording/layout change.
"""
from pathlib import Path
from copy import deepcopy
from zipfile import ZipFile, ZIP_DEFLATED
from lxml import etree as E

base=Path(__file__).resolve().parents[2]/'HNControl.Web/assets/legal-templates'
W='http://schemas.openxmlformats.org/wordprocessingml/2006/main'
ns={'w':W}
def q(s):return '{'+W+'}'+s
def text(p):return ''.join(p.xpath('.//w:t/text()',namespaces=ns))
with ZipFile(base/'Contrato_Servicios_v3.docx') as z: parts={n:z.read(n) for n in z.namelist()}
doc=E.fromstring(parts['word/document.xml']);body=doc.find(q('body'))
def find(s):return next(p for p in body.findall(q('p')) if text(p).startswith(s))
def replace(p,value):
    runs=p.findall(q('r'));rp=deepcopy(runs[-1].find(q('rPr')))
    headrp=deepcopy(runs[0].find(q('rPr')))
    for child in list(p):
        if child.tag!=q('pPr'):p.remove(child)
    if ' - ' in value and value.split('.')[0].isupper():
        h,value=value.split(' - ',1)
        r=E.SubElement(p,q('r'));r.append(headrp)
        t=E.SubElement(r,q('t'));t.set('{http://www.w3.org/XML/1998/namespace}space','preserve');t.text=h+' - '
    r=E.SubElement(p,q('r'));r.append(rp);E.SubElement(r,q('t')).text=value
def change(prefix,value):replace(find(prefix),value)

change('Que es un agente comercial','Que la modalidad de prestación, el operador de la red de acceso y los títulos habilitantes aplicables se identifican en el Anexo A de Servicios. La infraestructura podrá ser propia, de terceros o mixta conforme a las autorizaciones que legalmente correspondan. Este contrato no acredita por sí mismo una concesión, autorización ni registro y no presenta como otorgado un trámite pendiente.')
intro=find('CONTRATO DE PRESTACIÓN')
replace(intro,text(intro).replace('HUBNET INFRAESTRUCTURE TECHNOLOGY SOLUTIONS','HNNOMBRELEGAL'))
change('Que su domicilio legal','Que se identifica con la razón social HNNOMBRELEGAL, RFC HNRFC y domicilio fiscal en HNDOMICILIO, código postal HNCP. Para atención y soporte utiliza los canales del Anexo A.')
change('Anexo de Servicios.','Anexo de Servicios. - Es el Anexo A incorporado a este mismo documento, identificado con el número del contrato. Comprende la ficha técnica individual del servicio, los niveles de servicio, la medición, el soporte y el escalamiento. Forma parte de las condiciones que ambas partes aceptan con las firmas al final del contrato; no remite a un documento externo ausente.')
change('Los Servicios que prestará HN SOLUTIONS','Los Servicios se regirán por este contrato y su Anexo A. HN SOLUTIONS es responsable frente a “EL USUARIO” del servicio contratado, aun cuando utilice capacidad, infraestructura o soporte de otros operadores. La contratación de terceros no traslada al usuario la obligación de gestionar la falla ni libera a HN SOLUTIONS de sus compromisos.')
change('En caso de existir alguna discrepancia','El Anexo A individualiza las características y condiciones particulares expresamente identificadas y aceptadas. Su contenido no podrá reducir derechos mínimos legales ni contradecir las condiciones aplicables del modelo registrado. Las diferencias deberán aclararse por escrito antes de la aceptación; no existe una prevalencia general que permita modificar unilateralmente el contrato.')
change('HN SOLUTIONS se reserva el derecho exclusivo','HN SOLUTIONS podrá rechazar una solicitud nueva por causas objetivas de factibilidad técnica, cobertura o impedimento legal, informando el motivo al solicitante. Esta facultad no afecta los servicios ya contratados ni libera a HN de devolver cantidades o cumplir los remedios que procedan por su incumplimiento.')
change('TERCERA.','TERCERA. SOLICITUD Y ANEXO DEL SERVICIO. - Antes de la aceptación, HN SOLUTIONS entregará a “EL USUARIO” el contrato y su Anexo A completo. La fecha de instalación, el punto de entrega, las capacidades, la modalidad, la vigencia y los cargos serán los expresamente acordados. Toda modificación de las condiciones contratadas requerirá información previa y consentimiento expreso en los términos legales aplicables; cuando proceda, se notificará con al menos treinta días de anticipación. Ningún campo pendiente constituye una prestación o cargo aceptado.')
change('NOVENA.','NOVENA. ENTREGA Y ACEPTACIÓN DEL SERVICIO. - La entrega se documentará mediante una orden de servicio que incluya las pruebas de aceptación del enlace y la conformidad de “EL USUARIO” o de su representante autorizado. La sola presencia de una persona adulta o el uso del servicio no implican renuncia a reclamar deficiencias ni autorización automática para aceptar condiciones en nombre del usuario.')
change('“LAS PARTES” convienen que los reportes','Los registros del centro de gestión de red son evidencia técnica consultable y susceptible de contraste con los reportes y pruebas del usuario; no son prueba indubitable. HN SOLUTIONS entregará por correo o ticket un informe mensual del servicio dentro de los primeros cinco días hábiles del mes siguiente y atenderá solicitudes de extractos en un máximo de cinco días hábiles. El informe incluirá disponibilidad y fórmula aplicada, interrupciones, causas conocidas, exclusiones y su evidencia, tickets, tiempos de respuesta y restablecimiento, así como las mediciones de calidad efectivamente realizadas, su fuente y punto de medición. Los intervalos sin datos se identificarán expresamente y no se presentarán como servicio disponible. El usuario podrá objetar el informe por cualquiera de los canales de soporte; no requiere acceso al sistema interno del NOC.')
change('DÉCIMA SÉXTA.','DÉCIMA SÉXTA. VIGENCIA Y TERMINACIÓN. - La vigencia será la indicada en la carátula y en el Anexo A. El usuario podrá solicitar la terminación por los canales de atención, sin obstáculos injustificados. No se establece una pena automática sobre rentas por la terminación anticipada. En su caso, únicamente procederán los importes por servicios efectivamente prestados y el remanente del equipo financiado que esté identificado y desglosado en el acuerdo correspondiente, respetando los derechos legales aplicables. Al concluir el plazo pactado no habrá penalización por cancelar; se conservan los derechos de terminación y compensación por incumplimiento del proveedor.')
rescission=find('DÉCIMA SEPTIMA.')
replace(rescission,text(rescission).split('independientemente del derecho')[0]+'sin generar penas automáticas sobre rentas. Cualquier saldo exigible se sujetará a la cláusula décima sexta y a la legislación aplicable; esta disposición no autoriza cobros por servicios no prestados ni limita los medios de defensa del usuario.')
replace(rescission,text(rescission).replace('DÉCIMA SEPTIMA. RESCISIÓN DE CONTRATO.-','DÉCIMA SÉPTIMA. RESCISIÓN DEL CONTRATO. -'))
clause_run=rescission.find(q('r'))
clause_run.remove(clause_run.find(q('rPr')))
clause_run.insert(0,deepcopy(find('DÉCIMA SÉXTA.').find(q('r')).find(q('rPr'))))
change('Dar por terminada la prestación','Dar por terminada la prestación de los servicios adicionales, especiales o conexos mediante solicitud expresa por los canales de atención, sin condicionar la cancelación a la liquidación de adeudos ni a un plazo mínimo. HN SOLUTIONS atenderá la solicitud en un máximo de cinco días naturales, sin afectar el servicio básico. Los saldos por servicios efectivamente prestados se resolverán por separado conforme a la legislación aplicable.')
change('VIGÉSIMA SEGUNDA.','VIGÉSIMA SEGUNDA. EQUIPOS E INFRAESTRUCTURA. - La propiedad, identificación y modalidad de entrega de los equipos constarán en la orden de servicio. Los bienes entregados en comodato o arrendamiento conservarán la propiedad de su titular y deberán utilizarse y restituirse conforme a las condiciones expresamente pactadas. No se presume que todo equipo instalado pertenezca a HN SOLUTIONS. Los equipos vendidos o financiados se regirán por su acuerdo específico y no estarán sujetos a devolución como si fueran bienes en comodato; se respetarán los derechos del usuario y el desglose de saldos previsto en la cláusula décima sexta.')
change('DÉCIMA CUARTA.','DÉCIMA CUARTA. SUSPENSIÓN DE LOS SERVICIOS. - HN SOLUTIONS podrá suspender el servicio por falta de pago de cantidades exigibles no controvertidas, por uso contrario a la ley o al objeto contratado, o por orden de autoridad competente, observando los avisos, requisitos y derechos aplicables. No se suspenderá el servicio por importes de facturación sujetos a una reclamación pendiente de resolución cuando proceda la protección legal; el usuario cubrirá las cantidades no reclamadas. Tampoco se interrumpirá unilateralmente el servicio durante un procedimiento conciliatorio ante PROFECO en los supuestos previstos por la normativa aplicable. La suspensión no autoriza cobros o penalidades no pactados ni restringe los medios de defensa del usuario.')
change('El Servicio permanecerá suspendido','Para el restablecimiento de un servicio suspendido por falta de pago únicamente podrán exigirse las cantidades vencidas no controvertidas y los cargos de reconexión expresamente pactados y legalmente procedentes. Los cargos reclamados se resolverán por la vía correspondiente sin desconocer las protecciones del párrafo anterior. No se generarán cobros de activación o intereses ajenos a las condiciones legalmente aplicables.')
change('VIGÉSIMA QUINTA.','VIGÉSIMA QUINTA. NOTIFICACIONES. - Los avisos contractuales se comunicarán por escrito a los domicilios o correos que las partes hayan registrado, conservando constancia de recepción. Los reportes de fallas, aclaraciones, solicitudes de soporte y cancelaciones podrán presentarse por los canales del Anexo A y surtirán efectos conforme al momento de recepción y a sus reglas específicas; no se diferirá el inicio del ticket al siguiente día hábil. Cada parte comunicará oportunamente los cambios de sus datos. Los avisos de modificaciones contractuales respetarán los plazos y el consentimiento previstos por la legislación aplicable.')
compensation=find('DECIMA TERCERA.')
replace(compensation,text(compensation).replace('se compensará la parte proporcional del servicio no prestado y se otorgará la bonificación que corresponda conforme a la legislación aplicable','se reintegrará la parte proporcional del servicio no prestado y se otorgará adicionalmente una bonificación de al menos el 20% del importe correspondiente al periodo afectado, o la cantidad superior que corresponda conforme a la legislación aplicable'))
change('VIGÉSIMA SEXTA.','VIGÉSIMA SEXTA. LEGISLACIÓN Y AUTORIDADES COMPETENTES. - Este contrato se rige por la legislación mexicana aplicable, incluida la Ley en Materia de Telecomunicaciones y Radiodifusión, la Ley Federal de Protección al Consumidor y las normas aplicables según la naturaleza de la contratación. Se reconocen las competencias de PROFECO en protección al consumidor y de la Comisión Reguladora de Telecomunicaciones en su ámbito regulatorio, así como las de los tribunales legalmente competentes. Ninguna estipulación implica renuncia general del usuario a sus derechos ni impide acudir a la autoridad correspondiente.')
change('Entendido el alcance','Entendido el alcance de este contrato y de su Anexo A integrado (ficha técnica, niveles de servicio, medición, soporte y escalamiento), ambas partes manifiestan su aceptación mediante las firmas que constan a continuación. Cualquier modificación posterior se formalizará por acuerdo expreso; el documento firmado se conservará sin sustituir sus condiciones.')

anchor=find('Niveles de servicio y soporte')
title_sample=deepcopy(find('Datos del cliente'))
paragraph_sample=deepcopy(find('Este apartado aplica'))
def para(value,heading=False,page=False):
    p=deepcopy(title_sample if heading else paragraph_sample);replace(p,value)
    pp=p.find(q('pPr'))
    for old in pp.findall(q('pageBreakBefore')):pp.remove(old)
    if page:E.SubElement(pp,q('pageBreakBefore'))
    anchor.addprevious(p)
    return p

para('Anexo A de Servicios',True,True)
para('Contrato: ANEXONUMERO · Cliente: RSCLIENTE')
para('ANEXOESTADO')
para('Ficha técnica y modalidad de prestación',True)
sample=body.findall(q('tbl'))[2]
t=deepcopy(sample)
for tr in list(t.findall(q('tr'))):t.remove(tr)
rows=[
 ['Servicio y sede','CONTRATOC · ANEXOSEDE'],
 ['Dirección del servicio','ANEXODIRECCION'],
 ['Modalidad y red','ANEXOMODALIDAD · ANEXORED'],
 ['Operador de acceso','ANEXOPROVEEDOR'],
 ['Título y titular declarados','ANEXOTITULO · ANEXOTITULAR'],
 ['Registro del modelo PROFECO','ANEXOPROFECO'],
 ['Capacidad y simetría','Bajada: ANEXOBAJADA · Subida: ANEXOSUBIDA · ANEXOSIMETRIA'],
 ['Medio y punto de entrega','ANEXOMEDIO · ANEXOPUNTO'],
 ['IP pública y alcance','ANEXOIP'],
 ['Parámetros de calidad','Latencia: ANEXOLATENCIA · Jitter: ANEXOJITTER · Pérdida: ANEXOPERDIDA'],
 ['Perímetro y método','ANEXOMEDICION'],
 ['Vigencia y plazo','ANEXOVIGENCIA · PERIODOC'],
 ['Instalación / activación pactada','ANEXOINSTALACION'],
 ['Cargos antes de IVA','Mensualidad: $ COSTOCLIENTE MXN · Instalación: $ COSTOINST MXN'],
 ['Responsable de escalamiento','ANEXOESCALAMIENTO'],
]
for row in rows:
    tr=deepcopy(sample.findall(q('tr'))[0])
    for c,value in zip(tr.findall(q('tc')),row):
        ps=c.findall(q('p'));replace(ps[0],value)
        for extra in ps[1:]:c.remove(extra)
    t.append(tr)
anchor.addprevious(t)
para('Los campos no especificados no acreditan títulos ni registros y no constituyen garantías adicionales. La modalidad de red no modifica la responsabilidad de HN frente al usuario. No se presume redundancia, ruta alterna, equipo financiado ni IP fija si no están pactados expresamente.')
para('Las velocidades numéricas se expresan en megabits por segundo (Mbps), no en megabytes. Los parámetros de calidad deben indicar su alcance de medición y no se inferirán de la velocidad comercial. Los equipos entregados y su propiedad se documentan en la orden de servicio; cualquier financiamiento requiere desglose aceptado.')
para('El corte es el día 1 y el pago posterior a la primera factura vence el día 15, conforme a la carátula. Los niveles, la medición y los canales que siguen forman parte de este Anexo A y se aceptan con las firmas finales de ambas partes.')

installation=deepcopy(paragraph_sample)
replace(installation,'Cuando la contratación esté comprendida en la NOM-184-SCFI-2018, la entrega o instalación del equipo terminal requerido se realizará dentro de los diez días hábiles siguientes a la firma. Si el servicio no puede iniciarse por causa atribuible a HN, se devolverá el anticipo dentro de los treinta días hábiles posteriores a la fecha límite de instalación y el contrato terminará sin responsabilidad del usuario, con la penalidad que legalmente corresponda a HN. Si no hubo anticipo, se cancelará el cargo de instalación y no se gestionará su cobro. Los cargos de servicio comenzarán desde su prestación efectiva; la fecha planeada no acredita por sí sola la activación.')
find('TERCERA.').addnext(installation)

replace(anchor,'Anexo A · Niveles de servicio y soporte')
change('Medición y seguimiento del SLA','Anexo A · Medición y evidencia')
change('Atención de una interrupción total','Atención de incidentes y restablecimiento')
# The complete contract retains 24/7 service channels regardless of the access modality.
change('La primera respuesta será','La atención de reportes es gratuita y está disponible 24 horas al día, todos los días, para ambas modalidades. En Dedicado, la primera respuesta humana tiene un máximo contractual de cuatro horas transcurridas, contado desde la detección o reporte, lo que ocurra antes; un acuse automático no la sustituye. Se procurará responder en quince minutos y efectuar el diagnóstico inicial dentro de la primera hora: son objetivos operativos tempranos. Una vez iniciada la atención se actualizará el ticket cada hora. El objetivo de recuperación no es un plazo máximo garantizado ni amplía la indisponibilidad permitida. Responder y restablecer el enlace son compromisos distintos.')
sla_table=next(t for t in body.findall(q('tbl')) if 'Hasta 15 minutos' in text(t))
for tr in sla_table.findall(q('tr')):
    cells=tr.findall(q('tc'))
    if len(cells)<3:continue
    label=text(cells[0])
    mapping={'Primera respuesta':'Objetivo: 15 minutos. Máximo contractual: 4 horas transcurridas, 24/7',
             'Diagnóstico':'Objetivo: hasta 1 hora desde la detección o reporte',
             'Actualización':'Cada hora desde la primera respuesta'}
    for prefix,value in mapping.items():
        if label.startswith(prefix):replace(cells[1].find(q('p')),value)
for node in doc.xpath('//w:t',namespaces=ns):
    if node.text:node.text=node.text.replace('horas de soporte','horas').replace('hora de soporte','hora')
channel=find('Canales para abrir un ticket')
replace(channel,'Soporte NOC y matriz de escalamiento')
following=channel.getnext().getnext()
replace(following,'NOC: recibe el ticket y asigna folio; en Dedicado la primera respuesta humana tiene un máximo de cuatro horas. Ingeniería: objetivo de escalamiento al persistir la interrupción una hora, o de inmediato al identificar necesidad de intervención especializada. Operaciones: objetivo de intervención ante riesgo de incumplimiento del SLA o de los tiempos de atención, sin esperar a agotar cuatro horas. El NOC coordina y comunica los tres niveles por los canales anteriores; no es necesario duplicar el reporte ni contactar directamente al mayorista.')
change('Evidencia y comunicación','Informes y acceso a evidencia')
change('El NOC conservará','El NOC conservará las horas de detección, reporte, respuesta, diagnóstico, escalamiento y restablecimiento, junto con la causa y las evidencias disponibles. HN remitirá el informe mensual dentro de los primeros cinco días hábiles del mes siguiente por correo o ticket. El usuario podrá pedir extractos de su enlace gratuitamente y recibirlos dentro de cinco días hábiles, sin acceder al sistema interno del proveedor. Se admitirán los reportes verificables del usuario y se identificarán los periodos sin medición.')
change('Si se prevé rebasar','El informe indicará minutos evaluables, interrupciones computables, porcentaje de disponibilidad, exclusiones justificadas y evidencia, tickets, tiempos de atención y restablecimiento. Para las mediciones de capacidad, latencia, jitter y pérdida efectivamente realizadas se indicarán punto, método, fecha y cobertura. Una métrica sin datos se marcará como no medida. El usuario podrá controvertir el informe por los canales de soporte y aportar su propia evidencia; HN dará respuesta a la aclaración sin limitar los plazos y vías legales de reclamación.')
evidence=find('El informe indicará')
incident=deepcopy(paragraph_sample)
replace(incident,'La respuesta máxima de cuatro horas para Dedicado aplica también a degradaciones e incidentes parciales. El objetivo de restablecimiento de cuatro horas se refiere a interrupciones totales; las demás afectaciones recibirán diagnóstico y un plan según su impacto. Si se prevé rebasar un objetivo, el NOC comunicará la causa, las acciones en curso y una nueva estimación, manteniendo las actualizaciones. Una nueva estimación no reinicia el tiempo de interrupción ni elimina las compensaciones aplicables.')
evidence.addnext(incident)
compensation_summary=find('La compensación y bonificación se determinan')
replace(compensation_summary,text(compensation_summary).replace(' La disponibilidad garantizada, el objetivo de recuperación y las compensaciones son conceptos independientes.',''))
E.SubElement(find('VIGÉSIMA PRIMERA.').find(q('pPr')),q('keepNext'))
for node in doc.xpath('//w:t',namespaces=ns):
    if node.text=='Hubnet Infraestructure Technology Solutions':node.text='HNNOMBRELEGAL'
    if node.text=='Jorge Alberto Santana Torres':node.text='HNREPRESENTANTE'
parts['word/document.xml']=E.tostring(doc,xml_declaration=True,encoding='UTF-8',standalone=True)
with ZipFile(base/'Contrato_Servicios_v4.docx','w',ZIP_DEFLATED) as z:
    for n,b in parts.items():z.writestr(n,b)
print('Created v4; v3 unchanged')
