using System.Net;
using HNControl.Web.Models;

namespace HNControl.Web.Services;

public static class ClientCommunicationTemplates
{
    public const string EventWelcomeSubject = "HN Solutions · Bienvenida y coordinación de conectividad para su evento";
    public static string EventWelcome(Client client, ClientServiceContract contract, IEnumerable<(ClientLegalDocument Document, string Url)> documents)
    {
        var links = string.Join("", documents.Select(x => $"<p><a href=\"{E(x.Url)}\">Revisar contrato del evento: {E(x.Document.Title)}</a></p>"));
        return Layout("Conectividad para su evento", $"""
            <p>Estimado(a) <strong>{E(client.Name)}</strong>:</p>
            <p>Gracias por elegir a HN Solutions para la conectividad de su evento. Estos son los datos de su contratación temporal:</p>
            <h2 style="color:#086db4;font-size:19px">01 · Su evento</h2>
            <p><strong>Servicio:</strong> {E(contract.Label)}<br>
            <strong>Contrato:</strong> {E(contract.ContractNumber)}<br>
            <strong>Periodo:</strong> {E(EventContract.Period(contract))}<br>
            <strong>Sede:</strong> {E(contract.Branch)} · {E(contract.BranchAddress)}<br>
            <strong>Importe único del servicio, antes de IVA:</strong> {contract.MonthlyAmount:N2} MXN</p>
            <p>La contratación concluye en la fecha indicada, sin renovación automática ni cobros mensuales recurrentes. La instalación se cobra por separado cuando figure en su contrato.</p>
            <h2 style="color:#086db4;font-size:19px">02 · Contrato y coordinación</h2>
            {links}
            <p>Revise el alcance y complete la firma electrónica. Al firmar ambas partes recibirá el PDF final. Nuestro equipo coordinará los horarios de montaje, pruebas, operación y retiro de equipos, así como el acceso y la energía necesarios en la sede.</p>
            <h2 style="color:#086db4;font-size:19px">03 · Facturación y pago</h2>
            <p>Los anticipos y fechas de pago serán los acordados en la cotización aceptada. La factura se enviará al correo de facturación registrado: {E(client.BillingEmail)}.</p>
            <p>Banco: BBVA<br>Beneficiario: HUBNET INFRAESTRUCTURE TECHNOLOGY SOLUTIONS<br>
            Cuenta: 0123904423<br>CLABE: 012 180 00123904423 6<br>Referencia: <strong>{E(client.ClientCode)}</strong></p>
            <h2 style="color:#086db4;font-size:19px">04 · Atención durante el evento</h2>
            <p>Llamada y WhatsApp: <a href="https://wa.me/525526014566">5526014566</a><br>
            <a href="mailto:soporte@hubnet-solutions.com.mx">soporte@hubnet-solutions.com.mx</a><br>
            <a href="https://support.hubnet-solutions.net">Sistema de tickets</a></p>
            <p>Para cambios de sede, fechas o requerimientos, contáctenos antes del evento para revisar disponibilidad y condiciones.</p>
            """);
    }
    public const string WelcomeSubject = "Bienvenido a HN Solutions: Proceso de Contratación, Facturación y Pago";
    public static string Recipient(Client client) => new[] { client.Email, client.LegalEmail, client.BillingEmail }
        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";
    public static bool IsEmail(string? value) => !string.IsNullOrWhiteSpace(value)
        && new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(value);
    public static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    public static string Layout(string title, string content, string signature = "Administración y Finanzas") => $$"""
        <!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"></head>
        <body style="margin:0;background:#f1f5f9;color:#243247;font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:1.65">
        <table role="presentation" style="width:100%;border-collapse:collapse"><tr><td style="padding:24px 12px">
        <table role="presentation" style="width:100%;max-width:680px;margin:auto;border-collapse:collapse;background:#fff">
        <tr><td style="background:#102c4a;padding:28px 32px;color:#fff"><div style="font-size:12px;letter-spacing:2px;color:#9bd5ff">HN SOLUTIONS</div>
        <h1 style="font-size:25px;line-height:1.3;margin:12px 0 0">{{E(title)}}</h1></td></tr>
        <tr><td style="padding:28px 32px">{{content}}
        <p style="margin-top:30px;border-top:1px solid #e2e8f0;padding-top:22px">Cordialmente,<br><strong>{{E(signature)}}</strong><br>HN Solutions</p>
        </td></tr></table></td></tr></table></body></html>
        """;

    public static string Welcome(Client client, IEnumerable<(ClientLegalDocument Document, string Url)> documents)
    {
        var items = documents.ToList();
        // Only summarize the new SLA when every contract in this welcome incorporates it.
        // Older snapshots remain authoritative when invitations are resent.
        var contracts = items.Where(x => x.Document.DocumentType == ClientLegalDocumentType.Contract).ToList();
        var usesCurrentSla = contracts.Count > 0 && contracts.All(x =>
        {
            var snapshot = string.IsNullOrWhiteSpace(x.Document.SigningSnapshotJson) ? null
                : System.Text.Json.JsonSerializer.Deserialize<ClientSigningSnapshot>(x.Document.SigningSnapshotJson);
            return snapshot?.ServiceTemplateRevision >= 3 && !EventContract.IsEvent(snapshot.Contract)
                && (snapshot.ServiceTemplateRevision < 5 || snapshot.Contract?.ServiceType == ClientServiceType.Internet
                    || ClientServiceContractMetadata.ParseTechnical(snapshot.Contract?.Notes).ServiceTypes
                        .Contains("Internet", StringComparer.OrdinalIgnoreCase));
        });
        var usesIntegratedAnnex = usesCurrentSla && contracts.All(x =>
            System.Text.Json.JsonSerializer.Deserialize<ClientSigningSnapshot>(x.Document.SigningSnapshotJson!)?.ServiceTemplateRevision >= 4);
        var usesSupportPortal = usesIntegratedAnnex && contracts.All(x =>
            System.Text.Json.JsonSerializer.Deserialize<ClientSigningSnapshot>(x.Document.SigningSnapshotJson!)?.ServiceTemplateRevision >= 5);
        var serviceTerms = usesSupportPortal ? """
            <li><strong>Su Anexo A:</strong> está integrado en el contrato con el servicio, la modalidad, velocidad, ubicación, vigencia y cargos pactados. HN Solutions responde ante usted por la prestación del servicio.</li>
            <li><strong>Internet Dedicado:</strong> disponibilidad mínima mensual del <strong>99.85%</strong>. Primera respuesta humana ante incidentes con objetivo de 15 minutos y <strong>máximo contractual de cuatro horas transcurridas, 24/7</strong>. Un acuse automático no sustituye la atención humana.</li>
            <li><strong>Restablecimiento:</strong> el objetivo operativo para una interrupción total en Dedicado es de cuatro horas desde la detección o reporte, lo que ocurra antes. No es un máximo garantizado de reparación. El tiempo de respuesta no sustituye la disponibilidad mensual ni las compensaciones.</li>
            <li><strong>Best Effort:</strong> sin disponibilidad porcentual garantizada salvo pacto expreso. Primera respuesta en hasta una hora, diagnóstico inicial en hasta cuatro y actualizaciones cada cuatro horas. Recuperación objetivo de 24 a 48 horas transcurridas.</li>
            <li><strong>Monitoreo e informes:</strong> consulte su servicio y sus informes mensuales en <a href="https://support.hubnet-solutions.net">la plataforma de soporte</a> con su acceso de cliente. El informe estará disponible dentro de los primeros cinco días hábiles del mes siguiente; puede solicitar gratuitamente copia o extractos, que recibirá dentro de cinco días hábiles.</li>
            <li><strong>Soporte:</strong> atención gratuita 24/7 en ambas modalidades, con escalamiento a Ingeniería y Operaciones. Si no puede acceder al portal, utilice WhatsApp o correo. Los informes pueden aclararse por esos mismos canales y no se exige esperar 72 horas para las compensaciones aplicables.</li>
            """ : usesIntegratedAnnex ? """
            <li><strong>Su Anexo A:</strong> está integrado en el contrato y contiene la modalidad, la capacidad, el punto de entrega, la red utilizada y las condiciones de calidad del servicio. HN responde ante usted también cuando utilice infraestructura de terceros.</li>
            <li><strong>Internet Dedicado:</strong> disponibilidad mínima mensual del <strong>99.85%</strong>. Primera respuesta humana ante incidentes con objetivo de 15 minutos y <strong>máximo contractual de cuatro horas transcurridas, 24/7</strong>. Un acuse automático no sustituye la atención humana.</li>
            <li><strong>Restablecimiento:</strong> el objetivo operativo para una interrupción total en Dedicado es de cuatro horas desde la detección o reporte, lo que ocurra antes. No es un máximo garantizado de reparación. El tiempo de respuesta no sustituye la disponibilidad mensual ni las compensaciones.</li>
            <li><strong>Best Effort:</strong> sin disponibilidad porcentual garantizada salvo pacto expreso. Primera respuesta en hasta una hora, diagnóstico inicial en hasta cuatro y actualizaciones cada cuatro horas. Recuperación objetivo de 24 a 48 horas transcurridas.</li>
            <li><strong>Soporte y evidencia:</strong> atención gratuita 24/7 en ambas modalidades. El contrato define escalamiento a Ingeniería y Operaciones, informe mensual dentro de los primeros cinco días hábiles y acceso a extractos dentro de cinco días hábiles. No se exige esperar 72 horas para las compensaciones aplicables.</li>
            """ : usesCurrentSla ? """
            <li><strong>Internet Dedicado:</strong> disponibilidad mínima mensual del <strong>99.85%</strong>, medida en el punto de entrega conforme al contrato. Ante una interrupción total: primera respuesta humana en hasta 15 minutos, diagnóstico inicial en hasta una hora y actualizaciones cada hora, con atención 24/7.</li>
            <li><strong>Objetivo de recuperación para Dedicado:</strong> hasta cuatro horas transcurridas. Es un objetivo operativo; no sustituye la disponibilidad mensual ni las compensaciones por incumplimiento.</li>
            <li><strong>Best Effort:</strong> ancho de banda compartido, sin porcentaje de disponibilidad garantizado salvo acuerdo expreso en su anexo. Primera respuesta en hasta una hora de soporte, diagnóstico en hasta cuatro y actualizaciones cada cuatro horas de soporte. Objetivo de recuperación de 24 a 48 horas transcurridas, sujeto al diagnóstico y al proveedor de acceso.</li>
            <li><strong>Horario y compensaciones:</strong> consulte el apartado de niveles de servicio de su contrato. En Best Effort, si el anexo no especifica otro horario, la atención es 24/7. No se exige esperar 72 horas de interrupción para las compensaciones que correspondan.</li>
            """ : """
            <li><strong>Garantías y atención:</strong> la disponibilidad, los tiempos de soporte y las compensaciones son los establecidos en cada contrato y su Anexo de Servicios. Consulte el documento enlazado para conocer las condiciones de su servicio.</li>
            """;
        var links = string.Join("", items.Select(item => $"""
            <div style="border:1px solid #dbe5ef;border-radius:8px;padding:18px;margin:14px 0">
            <strong>{E(item.Document.Title)}</strong><br>
            Plazo: {E(ContractPeriod(item.Document))}<br>
            <a href="{E(item.Url)}" style="display:inline-block;background:#086db4;color:#fff;text-decoration:none;padding:10px 18px;border-radius:5px;margin-top:12px">{(item.Document.SignedAt.HasValue ? "Consultar documento" : "Revisar y firmar contrato")}</a>
            </div>
            """));
        return Layout("Bienvenido a HN Solutions", $$"""
            <p>Estimado(a) <strong>{{E(client.Name)}}</strong>:</p>
            <p>Agradecemos su confianza en HN Solutions. Somos una empresa con más de 15 años de experiencia en México, dedicada a brindar soluciones integrales de conectividad, infraestructura y seguridad para empresas.</p>
            <p>A continuación, encontrará los pasos para formalizar su contratación y la información de facturación y pago de sus servicios.</p>
            <h2 style="font-size:19px;color:#086db4;margin-top:28px">01 · Proceso de contratación</h2>
            <p><strong>Contrato Marco de Servicio.</strong> Le solicitamos revisar los datos y condiciones de sus documentos y completar su firma electrónica mediante los siguientes enlaces:</p>
            {{links}}
            <p>Cuando el cliente y HN Solutions hayan firmado el contrato, recibirá una confirmación por correo con el PDF final firmado. No es necesario imprimirlo ni reenviarlo.</p>
            <ul style="padding-left:20px">
            {{serviceTerms}}
            <li><strong>Alcance contratado:</strong> {{(usesSupportPortal ? "las condiciones particulares constan en el contrato y su anexo; los equipos y datos técnicos de instalación se documentan en la orden de servicio." : "instalación, equipos e IP pública únicamente en las condiciones expresamente indicadas en su contrato y anexo.")}}</li>
            </ul>
            <p style="background:#edf6fc;padding:16px;border-left:3px solid #086db4">
            <strong>Abra un ticket de soporte por cualquiera de estos canales:</strong><br>
            WhatsApp del NOC (canal preferente): <a href="https://wa.me/525526014566">5526014566</a><br>
            Sistema de tickets: <a href="https://support.hubnet-solutions.net">support.hubnet-solutions.net</a><br>
            Correo: <a href="mailto:soporte@hubnet-solutions.com.mx">soporte@hubnet-solutions.com.mx</a></p>
            <p>Indique su número de cliente o contrato, la sede y una descripción de la falla. Nuestro equipo le comunicará el folio para seguimiento; no es necesario duplicar el reporte en los tres canales.</p>
            <h2 style="font-size:19px;color:#086db4;margin-top:28px">02 · Facturación y fechas de pago</h2>
            <p>La fecha de corte es el <strong>día 1 de cada mes</strong>. Dentro de los cinco días siguientes al corte recibirá su factura en PDF y XML en el correo de facturación registrado{{(string.IsNullOrWhiteSpace(client.BillingEmail) ? "." : $": <strong>{E(client.BillingEmail)}</strong>.")}}</p>
            <p>La <strong>primera factura se paga contra entrega del servicio</strong>. A partir de la segunda factura, la fecha límite de pago es el <strong>día 15 del mismo mes del corte</strong>.</p>
            <h2 style="font-size:19px;color:#086db4;margin-top:28px">03 · Datos bancarios</h2>
            <p>Para realizar sus pagos mensuales, utilice los siguientes datos:</p>
            <table role="presentation" style="width:100%;background:#f8fafc;border-collapse:collapse">
            <tr><td style="padding:10px 14px">Banco</td><td style="padding:10px 14px"><strong>BBVA</strong></td></tr>
            <tr><td style="padding:10px 14px">Beneficiario</td><td style="padding:10px 14px">HUBNET INFRAESTRUCTURE TECHNOLOGY SOLUTIONS</td></tr>
            <tr><td style="padding:10px 14px">Cuenta</td><td style="padding:10px 14px">0123904423</td></tr>
            <tr><td style="padding:10px 14px">CLABE</td><td style="padding:10px 14px">012 180 00123904423 6</td></tr>
            <tr><td style="padding:10px 14px">Referencia</td><td style="padding:10px 14px"><strong>{{E(client.ClientCode)}}</strong></td></tr>
            </table>
            <p>Incluya su número de cliente como referencia para facilitar la identificación de su pago.</p>
            <h2 style="font-size:19px;color:#086db4;margin-top:28px">04 · Confirmación de entrega del servicio</h2>
            <p>{{(usesIntegratedAnnex ? "La entrega se documentará mediante una orden de servicio con pruebas de aceptación y la conformidad de usted o de su representante autorizado. Recibirá los datos técnicos para validar o configurar su conexión." : "El servicio se considerará entregado cuando reciba el correo de confirmación de que está activo y funciona conforme a lo solicitado. Ese mensaje incluirá la información técnica y de servicio necesaria para validar o configurar su conexión.")}}</p>
            <p>Quedamos a sus órdenes para cualquier duda o comentario.</p>
            """);
    }

    public static string Completed(ClientLegalDocument doc) => Layout("Su documento firmado está listo", $"""
        <p>Estimado(a) <strong>{E(doc.Client?.Name)}</strong>:</p>
        <p>{(doc.DocumentType == ClientLegalDocumentType.Contract ? "Confirmamos que el cliente y HN Solutions han completado la firma electrónica del contrato." : "Confirmamos el registro de su firma electrónica.")}</p>
        <p><strong>{E(doc.Title)}</strong></p>
        <p>Firma del cliente: {E(doc.SignedByName)} · {doc.SignedAt:dd/MM/yyyy HH:mm} UTC<br>
        {(doc.ProviderSignedAt.HasValue ? $"Firma de HN Solutions: {E(doc.ProviderSignedByName)} · {doc.ProviderSignedAt:dd/MM/yyyy HH:mm} UTC" : "")}</p>
        <p>Adjuntamos el PDF firmado para su resguardo.</p>
        <p>Agradecemos su confianza y quedamos a su disposición.</p>
        """);

    private static string ContractPeriod(ClientLegalDocument document)
    {
        var contract = string.IsNullOrWhiteSpace(document.SigningSnapshotJson)
            ? document.ClientServiceContract
            : System.Text.Json.JsonSerializer.Deserialize<ClientSigningSnapshot>(document.SigningSnapshotJson)?.Contract;
        return TemplateDocxService.BuildContractPeriod(document, contract);
    }
}
