using System.Globalization;
using HNControl.Web.Models;
using static HNControl.Web.Services.ClientCommunicationTemplates;

namespace HNControl.Web.Services;

public static class ServiceOrderNotificationTemplates
{
    public static string Reference(ServiceOrder order) => "OS-" + order.Id.ToString("N")[..8].ToUpperInvariant();
    private static string Date(DateTime? date) => date?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Por confirmar";
    private static string Row(string name, string? value) => $"<tr><td style=\"padding:10px 14px;color:#64748b\">{E(name)}</td><td style=\"padding:10px 14px\"><strong>{E(value)}</strong></td></tr>";
    private static string Summary(ServiceOrder order, ServiceOrderScheduleSnapshot schedule)
    {
        var service = order.ClientServiceContract;
        return "<table role=\"presentation\" style=\"width:100%;background:#f8fafc;border-collapse:collapse;margin:18px 0\">"
            + Row("Referencia de orden", Reference(order)) + Row("Número de cliente", order.Client?.ClientCode)
            + Row("Orden de servicio", order.Title) + Row("Tipo de atención", order.Type.GetDisplayName())
            + (service == null ? "" : Row("Servicio contratado", service.Label)
                + (string.IsNullOrWhiteSpace(service.Branch) ? "" : Row("Sucursal", service.Branch)))
            + Row("Fecha programada de atención", schedule.ScheduledDateConfirmed ? Date(schedule.StartDate) : "Por confirmar")
            + (schedule.ScheduledDateConfirmed && schedule.ExpectedEndDate.HasValue ? Row("Fecha estimada de término", Date(schedule.ExpectedEndDate)) : "")
            + "</table>";
    }

    public static string Client(ServiceOrder order, ServiceOrderScheduleSnapshot schedule)
    {
        var status = order.Status is ServiceOrderStatus.Finalized ? "La orden se encuentra finalizada."
            : order.Status is ServiceOrderStatus.InReview or ServiceOrderStatus.PendingClientSignature ? "La orden se encuentra en revisión y cierre."
            : "Su solicitud se encuentra registrada y en proceso de coordinación por nuestro equipo de Ingeniería.";
        var scheduling = schedule.ScheduledDateConfirmed && schedule.StartDate.HasValue
            ? $"La fecha programada al registrar su orden es el <strong>{Date(schedule.StartDate)}</strong>. Nuestro equipo coordinará con usted el horario y los detalles de acceso al sitio."
            : "La fecha y el horario de atención están por confirmar. Nuestro equipo se comunicará con usted para coordinar la visita.";
        return Layout("Su orden de servicio está registrada", $"""
            <p>Estimado(a) <strong>{E(order.Client?.Name)}</strong>:</p>
            <p>Confirmamos el registro de su orden de servicio en HN Solutions. {status}</p>
            <h2 style="font-size:19px;color:#086db4;margin-top:28px">01 · Información de su orden</h2>
            {Summary(order, schedule)}
            <h2 style="font-size:19px;color:#086db4;margin-top:28px">02 · Programación de la atención</h2>
            <p>{scheduling}</p>
            <p>Le agradeceremos tener disponible a la persona de contacto y facilitar el acceso a las instalaciones que requieran atención. Si necesita solicitar algún ajuste, comuníquese con nosotros indicando la referencia de su orden.</p>
            <h2 style="font-size:19px;color:#086db4;margin-top:28px">03 · Estamos para ayudarle</h2>
            <p style="background:#edf6fc;padding:16px;border-left:3px solid #086db4">
            WhatsApp: <a href="https://wa.me/525526014566">55 2601 4566</a><br>
            Sistema de tickets: <a href="https://support.hubnet-solutions.net">support.hubnet-solutions.net</a><br>
            Correo: <a href="mailto:soporte@hubnet-solutions.com.mx">soporte@hubnet-solutions.com.mx</a></p>
            <p>Agradecemos su confianza. Le mantendremos informado durante la atención de su servicio.</p>
            """, "Ingeniería y Operaciones");
    }

    public static string Engineering(ServiceOrder order, ServiceOrderScheduleSnapshot schedule, string? baseUrl)
    {
        var link = "";
        if (Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
        {
            var url = uri.ToString().TrimEnd('/') + "/Admin/ServiceOrders/Details/" + order.Id;
            link = $"<p><a href=\"{E(url)}\" style=\"display:inline-block;background:#086db4;color:#fff;text-decoration:none;padding:10px 18px;border-radius:5px\">Consultar orden en HN Control</a></p>";
        }
        return Layout("Nueva orden de servicio", $"""
            <p>Equipo de <strong>Ingeniería</strong>:</p>
            <p>Se ha registrado una nueva orden para <strong>{E(order.Client?.Name)}</strong>. Revise los requerimientos y coordine los recursos necesarios para su atención.</p>
            {Summary(order, schedule)}
            <p><strong>Responsable asignado:</strong> {E(order.AssignedEmployee?.FullName ?? "Pendiente de asignación")}</p>
            <h2 style="font-size:19px;color:#086db4;margin-top:28px">Requerimientos registrados</h2>
            <div style="white-space:pre-wrap;background:#f8fafc;padding:16px;border-left:3px solid #086db4">{E(order.Description)}</div>
            {link}
            <p>Ingrese a HN Control con su cuenta autorizada para consultar y dar seguimiento a la orden.</p>
            """, "Ingeniería y Operaciones");
    }
}
