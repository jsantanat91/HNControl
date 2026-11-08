"""Build versioned HN documents from the approved service-contract design.

Run with the bundled workspace Python runtime (python-docx). Source legal clauses
are extracted verbatim from Contrato_Eventual.docx; no original is overwritten.
"""
from pathlib import Path
from copy import deepcopy
from hashlib import sha256
from zipfile import ZipFile
from io import BytesIO
from PIL import Image
from docx import Document
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Pt, Inches, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_LINE_SPACING
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT

ROOT = Path(__file__).resolve().parents[2]
FOLDER = ROOT / 'HNControl.Web/assets/legal-templates'
REF = FOLDER / 'Contrato_Servicios_v2.docx'
NAVY, BLUE, INK, MUTE, BORDER, WASH = '163650', '125F8C', '203345', '4F6473', 'CFDCE4', 'F1F6F9'


def el(tag, **attrs):
    value = OxmlElement('w:' + tag)
    for key, text in attrs.items():
        value.set(qn('w:' + key), str(text))
    return value


def fmt(p, before=0, after=0, keep=False, line=1.05):
    f = p.paragraph_format
    f.space_before, f.space_after = Pt(before), Pt(after)
    f.line_spacing, f.keep_with_next = line, keep
    f.keep_together, f.widow_control = False, True
    return p


def run(p, text, size=10, bold=False, color=INK):
    r = p.add_run(text)
    r.font.name, r.font.size, r.bold = 'Arial', Pt(size), bold
    r.font.color.rgb = RGBColor.from_string(color)
    return r


class Form:
    def __init__(self, header, footer, title):
        self.d = Document(REF)
        for item in list(self.d.element.body)[:-1]:
            self.d.element.body.remove(item)
        for sec in self.d.sections:
            for hf in [sec.header]:
                for text in hf._element.xpath('.//w:t'):
                    if 'SERVICIOS DE TELECOMUNICACIONES' in (text.text or ''):
                        text.text = text.text.replace('SERVICIOS DE TELECOMUNICACIONES', header)
            for hf in [sec.footer]:
                for text in hf._element.xpath('.//w:t'):
                    if 'Contrato de servicios' in (text.text or ''):
                        text.text = text.text.replace('Contrato de servicios', footer)
        # The reference's 3,689px logo is displayed at one inch; a print-resolution
        # copy avoids intermittent omission by LibreOffice's PDF image exporter.
        with ZipFile(REF) as package:
            logo = Image.open(BytesIO(package.read('word/media/image1.png')))
            logo.thumbnail((620, 620), Image.Resampling.LANCZOS)
            buffer = BytesIO(); logo.save(buffer, format='PNG'); buffer.seek(0)
        paragraph = self.d.sections[0].header.paragraphs[0]
        for existing in list(paragraph.runs):
            if existing._r.xpath('.//w:drawing'):
                drawing = paragraph.add_run()
                drawing.add_picture(buffer, width=Inches(1.03))
                existing._r.addprevious(drawing._r)
                paragraph._p.remove(existing._r)
        settings = self.d.settings.element
        for update in settings.findall(qn('w:updateFields')):
            settings.remove(update)
        settings.append(el('updateFields', val='true'))
        p = self.p(title, 24, True, '000000', after=3)
        p.style = self.d.styles['Title']
        borders = el('pBdr'); borders.append(el('bottom', val='nil'))
        p._p.get_or_add_pPr().append(borders)
        self.d.core_properties.title = title
        self.d.core_properties.author = 'HN Solutions'
        self.d.core_properties.comments = ''

    def p(self, text, size=10.6, bold=False, color=INK, before=0, after=4, justify=False, keep=False):
        p = fmt(self.d.add_paragraph(), before, after, keep, 1.11 if justify else 1.05)
        run(p, text, size, bold, color)
        if justify:
            p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
        return p

    def section(self, title, newpage=False):
        p = self.p(title, 12.5, True, NAVY, before=9, after=4, keep=True)
        p.paragraph_format.page_break_before = newpage
        p._p.get_or_add_pPr().append(el('outlineLvl', val=1))
        return p

    def table(self, widths, count, padding=70):
        t = self.d.add_table(rows=count, cols=len(widths))
        t.autofit = False
        t.alignment = WD_TABLE_ALIGNMENT.CENTER
        for c, w in zip(t.columns, widths):
            c.width = Inches(w)
        pr = t._tbl.tblPr
        borders = el('tblBorders')
        for edge in ['top', 'left', 'bottom', 'right', 'insideH', 'insideV']:
            borders.append(el(edge, val='single', sz=5, color=BORDER))
        pr.append(borders)
        margin = el('tblCellMar')
        for edge, value in [('top', padding), ('bottom', padding), ('left', 100), ('right', 100)]:
            margin.append(el(edge, w=value, type='dxa'))
        pr.append(margin)
        for row in t.rows:
            row._tr.get_or_add_trPr().append(el('cantSplit'))
            for c, w in zip(row.cells, widths):
                c.width = Inches(w)
                c.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
        return t

    @staticmethod
    def cell(c, text, size=10, bold=False, color=INK, new=False, before=0, after=0):
        p = fmt(c.add_paragraph() if new else c.paragraphs[0], before, after)
        run(p, text, size, bold, color)
        return p

    @staticmethod
    def shade(c, color=WASH):
        c._tc.get_or_add_tcPr().append(el('shd', val='clear', fill=color))

    def pairs(self, pairs):
        t = self.table([3.48, 3.48], (len(pairs) + 1) // 2)
        for i, (label, value) in enumerate(pairs):
            c = t.cell(i // 2, i % 2)
            self.cell(c, label, 9, True, BLUE)
            self.cell(c, value, 10, new=True, before=2)
        return t

    def records(self, headers, rows, widths, align_last=False):
        t = self.table(widths, len(rows) + 1, 70)
        t.rows[0]._tr.get_or_add_trPr().append(el('tblHeader'))
        for i, row in enumerate([headers] + rows):
            for j, text in enumerate(row):
                c = t.cell(i, j)
                if i == 0:
                    self.shade(c, NAVY)
                elif i % 2 == 0:
                    self.shade(c)
                p = self.cell(c, text, 9.5, i == 0, 'FFFFFF' if i == 0 else INK)
                if align_last and j == len(widths) - 1:
                    p.alignment = WD_ALIGN_PARAGRAPH.RIGHT
        return t

    def signatures(self, delivery=False):
        self.section('Firmas de conformidad' if delivery else 'Firmas de aceptación')
        self.p('Folio: ACTAFOLIO  ·  Fecha de entrega: FECHAHOY' if delivery else 'Contrato: EVENTONUMERO',
               9.3, True, BLUE, after=6, keep=True)
        t = self.table([3.48, 3.48], 1, 110)
        for index, (label, prefix, party, rep) in enumerate([
            ('Entrega · HN Solutions' if delivery else 'EL PROVEEDOR', 'SIGNHN', 'HN Solutions', 'NOMBRETECNICO' if delivery else 'Jorge Alberto Santana Torres'),
            ('Recibe · Cliente' if delivery else 'EL CLIENTE', 'SIGNCLIENT', 'RSCLIENTE', 'RECEPTORACTA' if delivery else 'RLCLIENTE')
        ]):
            c = t.cell(0, index)
            c.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.TOP
            self.cell(c, label, 10, True, BLUE)
            space = fmt(c.add_paragraph())
            space.paragraph_format.line_spacing = Pt(105)
            space.paragraph_format.line_spacing_rule = WD_LINE_SPACING.AT_LEAST
            line = el('pBdr'); line.append(el('bottom', val='single', sz=7, space=1, color='738EA1'))
            space._p.get_or_add_pPr().append(line)
            self.cell(c, 'Firma', 9, color=MUTE, new=True, before=3)
            self.cell(c, rep, 10, True, new=True, before=3)
            self.cell(c, party, 9.2, color=MUTE, new=True, before=2)
            self.cell(c, 'Registro electrónico', 9, True, BLUE, new=True, before=10, after=2)
            self.cell(c, prefix + 'STATUS', 9.2, new=True)
            self.cell(c, prefix + 'NAME', 9.2, new=True)
            self.cell(c, prefix + 'EMAIL', 9.2, new=True)
            self.cell(c, 'Fecha de firma: ' + prefix + 'DATE', 9.2, new=True, before=2)
            self.cell(c, 'Hash de firma: ' + prefix + 'HASH', 9.2, new=True, before=2)

    def save(self, filename):
        self.d.save(FOLDER / filename)


def event():
    old = Document(FOLDER / 'Contrato_Eventual.docx')
    source = [p.text for p in old.paragraphs]
    intro = next(x for x in source if x.startswith('Celebran el presente contrato'))
    agreement = next(x for x in source if x.startswith('La cotización aceptada'))
    clauses = [x for x in source if len(x) > 2 and x[0].isdigit() and x[1:3] == '. ']
    assert len(clauses) == 8
    f = Form('CONECTIVIDAD PARA EVENTOS', 'Contrato eventual', 'Contrato de conectividad temporal para eventos')
    f.p('Contrato: EVENTONUMERO     Fecha de elaboración: FECHAHOY', 9.4, color=BLUE, after=6)
    f.p(intro, 10, justify=True)
    f.section('Datos del cliente')
    f.pairs([('Nombre o razón social', 'RSCLIENTE'), ('Representante legal', 'RLCLIENTE'),
             ('RFC', 'RFCC'), ('Correo de facturación', 'EMAILC'),
             ('Domicilio', 'DIRECCIONC'), ('Estado y código postal', 'ESTADOC  ·  CPC')])
    f.section('Servicio contratado y vigencia')
    f.records(['Concepto', 'Detalle del servicio'], [
        ['Evento o servicio', 'NOMBREPROYECTO'], ['Conectividad contratada', 'CONTRATOC'],
        ['Sede del evento', 'EVENTOSITIO'], ['Vigencia inclusiva', 'PERIODOC']], [1.90, 5.06])
    f.section('Importes del servicio')
    f.records(['Concepto', 'Importe'], [['Importe único del servicio', 'MXN COSTOCLIENTE antes de IVA'],
                                        ['Instalación adicional', 'MXN COSTOINST antes de IVA']], [3.48, 3.48], True)
    f.section('Alcance y acuerdos particulares')
    f.p('EVENTOALCANCE', 10.6, justify=True)
    f.p(agreement, 9.5, color=MUTE, justify=True)
    f.section('Condiciones del servicio eventual')
    for i, clause in enumerate(clauses):
        ordinal, title, body = clause.split('. ', 2)
        p = f.p('', 10.6, before=5, after=5, justify=True, keep=i == 7)
        p.paragraph_format.keep_together = True
        run(p, ordinal + '. ' + title + '. ', 10.6, True, NAVY)
        run(p, body, 10.6)
    f.signatures()
    f.save('Contrato_Eventual_v2.docx')
    generated = [p.text for p in Document(FOLDER / 'Contrato_Eventual_v2.docx').paragraphs]
    assert [x for x in generated if len(x) > 2 and x[0].isdigit() and x[1:3] == '. '] == clauses
    assert intro in generated and agreement in generated


def delivery():
    f = Form('ENTREGA DE SERVICIOS', 'Acta de entrega', 'Acta de entrega de servicios y equipos')
    f.p('Folio: ACTAFOLIO     Fecha de entrega: FECHAHOY', 9.4, color=BLUE, after=6)
    f.p('Detalle de los servicios y equipos entregados, datos de recepción y registro de conformidad de ambas partes.', 10.6, justify=True)
    f.section('Datos del cliente y de la entrega')
    f.pairs([('Nombre o razón social', 'RSCLIENTE'), ('Representante legal', 'RLCLIENTE'),
             ('Correo de facturación', 'CORREOFACTURA'), ('Domicilio del cliente', 'DIRECCIONC'),
             ('Proyecto o servicio', 'NOMBREPROYECTO'), ('Lugar de entrega', 'LUGARENTREGA'),
             ('Técnico responsable', 'NOMBRETECNICO'), ('Persona que recibe', 'RECEPTORACTA')])
    f.section('Datos de conexión')
    f.pairs([('IP pública', 'IPPUBLICA'), ('Segmento LAN', 'SEGMENTOLAN')])
    f.section('Servicios entregados')
    f.records(['Partida', 'Descripción', 'Modalidad', 'Plazo', 'Entrega a\nsatisfacción'], [
        [str(i), f'SERVICIO{i:02}', f'SMODAL{i:02}', f'SPLAZO{i:02}', f'SATISFACCION{i:02}'] for i in range(1, 6)
    ], [.60, 3.10, 1.08, .80, 1.38])
    f.section('Equipamiento entregado')
    f.records(['Partida', 'Descripción', 'Cantidad'], [
        [str(i), f'EQUIPO{i:02}', f'ECANTIDAD{i:02}'] for i in range(1, 6)
    ], [.60, 5.46, .90], True)
    f.section('Notas adicionales')
    f.p('Los equipos cuentan con garantía de calidad y vicios ocultos por 12, 24 o 36 meses, según el contrato.', 10, justify=True)
    f.section('Atención y soporte del NOC', newpage=True)
    f.p('WhatsApp 55 2601 4566', 17, True, BLUE, after=3)
    f.p('Canal preferente de atención del NOC. También puede comunicarse por llamada al mismo número.', 10.6, justify=True, after=8)
    f.records(['Canal de atención', 'Datos de contacto'], [
        ['Llamada y WhatsApp', '5526014566'],
        ['Correo de soporte', 'soporte@hubnet-solutions.com.mx'],
        ['Portal de tickets', 'https://support.hubnet-solutions.net'],
        ['Horario NOC', 'Lunes a viernes: 9:00 am – 8:00 pm\nSábado: 9:00 am – 5:00 pm']
    ], [1.90, 5.06])
    f.signatures(delivery=True)
    f.save('Acta_Entrega_Material_Servicios_v2.docx')


if __name__ == '__main__':
    before = {p: sha256(p.read_bytes()).hexdigest() for p in [REF, FOLDER / 'Contrato_Eventual.docx', FOLDER / 'Acta_Entrega_Material_Servicios.docx']}
    event()
    delivery()
    assert all(sha256(p.read_bytes()).hexdigest() == digest for p, digest in before.items())
    print('Built event and delivery v2; originals and all eight event clauses preserved.')
