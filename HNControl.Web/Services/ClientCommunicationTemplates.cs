using System.Net;
using HNControl.Web.Models;

namespace HNControl.Web.Services;

public static class ClientCommunicationTemplates
{
    public const string WelcomeSubject = "Bienvenido a HN Solutions: Proceso de Contratación, Facturación y Pago";
    public static string Recipient(Client client) => new[] { client.Email, client.LegalEmail, client.BillingEmail }
        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";
    public static bool IsEmail(string? value) => !string.IsNullOrWhiteSpace(value)
        && new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(value);
    public static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    public static string Layout(string title, string content) => $$"""
        <!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"></head>
        <body style="margin:0;background:#f1f5f9;color:#243247;font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:1.65">
        <table role="presentation" style="width:100%;border-collapse:collapse"><tr><td style="padding:24px 12px">
        <table role="presentation" style="width:100%;max-width:680px;margin:auto;border-collapse:collapse;background:#fff">
        <tr><td style="background:#102c4a;padding:28px 32px;color:#fff"><div style="font-size:12px;letter-spacing:2px;color:#9bd5ff">HN SOLUTIONS</div>
        <h1 style="font-size:25px;line-height:1.3;margin:12px 0 0">{{E(title)}}</h1></td></tr>
        <tr><td style="padding:28px 32px">{{content}}
        <p style="margin-top:30px;border-top:1px solid #e2e8f0;padding-top:22px">Cordialmente,<br><strong>Administración y Finanzas</strong><br>HN Solutions</p>
        </td></tr></table></td></tr></table></body></html>
        """;

    public static string Welcome(Client client, IEnumerable<(ClientLegalDocument Document, string Url)> documents)
    {
        var links = string.Join("", documents.Select(item => $"""
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
            <li><strong>Garantía de servicio:</strong> SLA del 99.85% de disponibilidad, con los compromisos de simetría, ancho de banda y latencia establecidos en el contrato, así como las penalizaciones o condiciones de cancelación aplicables en caso de incumplimiento.</li>
            <li><strong>Beneficios incluidos:</strong> instalación básica, equipo e IP pública, conforme al servicio contratado.</li>
            <li><strong>Atención y soporte 24/7/365:</strong> asistencia técnica y atención personalizada todos los días del año.</li>
            </ul>
            <p style="background:#edf6fc;padding:16px;border-left:3px solid #086db4">
            WhatsApp: <a href="https://wa.me/525526014566">55 2601 4566</a><br>
            Sistema de tickets: <a href="https://support.hubnet-solutions.net">support.hubnet-solutions.net</a><br>
            Correo: <a href="mailto:soporte@hubnet-solutions.com.mx">soporte@hubnet-solutions.com.mx</a></p>
            <h2 style="font-size:19px;color:#086db4;margin-top:28px">02 · Facturación y fechas de pago</h2>
            <p>Una vez contratado el servicio, las facturas se emitirán durante los primeros cinco días de cada mes y se enviarán en formato PDF y XML al correo de facturación registrado{{(string.IsNullOrWhiteSpace(client.BillingEmail) ? "." : $": <strong>{E(client.BillingEmail)}</strong>.")}}</p>
            <p>Los días de corte son el <strong>15 y el 30 de cada mes</strong>. Se asignará el corte más próximo posterior a la instalación de su servicio.</p>
            <p>La <strong>primera factura se paga contra entrega del servicio</strong>. Posteriormente, la fecha límite de pago será el día 30 para el corte del día 15, o el día 15 para el corte del día 30, según corresponda.</p>
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
            <p>El servicio se considerará entregado cuando reciba el correo de confirmación de que está activo y funciona conforme a lo solicitado. Ese mensaje incluirá la información técnica y de servicio necesaria para validar o configurar su conexión.</p>
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
