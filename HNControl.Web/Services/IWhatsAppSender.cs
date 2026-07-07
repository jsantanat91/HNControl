namespace HNControl.Web.Services;

public interface IWhatsAppSender
{
    /// <summary>
    /// Envia texto libre. Con la Cloud API de Meta esto SOLO se entrega dentro de la
    /// ventana de 24h (cliente escribio primero) o a numeros de prueba. Para mensajes
    /// iniciados por el negocio usa <see cref="SendTemplateAsync"/> con una plantilla aprobada.
    /// </summary>
    Task SendAsync(string phone, string message, CancellationToken ct = default);

    /// <summary>
    /// Envia una plantilla HSM aprobada en Meta. Si el nombre de plantilla viene vacio,
    /// cae a texto libre usando <see cref="WhatsAppTemplateMessage.FallbackText"/>.
    /// </summary>
    Task SendTemplateAsync(WhatsAppTemplateMessage message, CancellationToken ct = default);

    Task<bool> IsConfiguredAsync(CancellationToken ct = default);
}

/// <summary>
/// Mensaje de plantilla. Los parametros son posicionales ({{1}}, {{2}}...) y su ORDEN
/// debe coincidir con la plantilla aprobada en Meta.
/// </summary>
/// <param name="Phone">Telefono destino (se normaliza a E.164 sin '+').</param>
/// <param name="TemplateName">Nombre exacto de la plantilla aprobada. Si es null/vacio, se usa FallbackText como texto libre.</param>
/// <param name="Parameters">Parametros del cuerpo, en orden.</param>
/// <param name="FallbackText">Texto a enviar si no hay plantilla configurada (ventana 24h / pruebas).</param>
/// <param name="Language">Idioma de la plantilla (ej. es_MX). Si null, usa el default de la config.</param>
/// <param name="AuthenticationCode">true para plantillas de categoria Authentication (OTP): agrega el boton copy-code con el codigo.</param>
public sealed record WhatsAppTemplateMessage(
    string Phone,
    string? TemplateName,
    IReadOnlyList<string> Parameters,
    string? FallbackText = null,
    string? Language = null,
    bool AuthenticationCode = false);
