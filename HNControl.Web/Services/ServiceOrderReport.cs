using System.Security.Cryptography;
using System.Text.Json;
using HNControl.Web.Models;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HNControl.Web.Services;

internal sealed class ServiceOrderReport(
    ServiceOrder order, string company, string footer, byte[]? logo,
    IReadOnlyList<ServiceOrderReport.Attachment> attachments,
    ServiceOrderReport.Signature technician, ServiceOrderReport.Signature client)
{
    private const string Navy = "#173B5D";
    private const string Blue = "#086DB4";
    private const string Ink = "#203247";
    private const string Muted = "#50647A";
    private const string Pale = "#F2F6FA";
    private const string Rule = "#D5E2ED";

    internal sealed record Attachment(Guid Id, string Name, DateTime UploadedAt, byte[]? Image);
    internal sealed record Signature(string? Name, DateTime? At, byte[]? Image, string? Hash)
    {
        internal static Signature Capture(Guid orderId, SignatureRole role, ServiceOrderSignature? record, byte[]? image)
        {
            if (record == null) return new(null, null, null, null);
            var hash = image is { Length: > 0 } ? Digest(new
            {
                Version = 1, orderId, role, record.Id, record.SignedByName, Ticks = record.SignedAt.Ticks,
                ImageSha256 = Convert.ToHexString(SHA256.HashData(image))
            }) : null;
            return new(record.SignedByName, record.SignedAt, image, hash);
        }
    }

    private ClientServiceContract? Contract => order.ClientServiceContract?.ClientId == order.ClientId ? order.ClientServiceContract : null;
    private string Service => ClientServiceContractMetadata.ServiceName(Contract);
    private string Reference => ServiceOrderNotificationTemplates.Reference(order);
    private string? TechnicianName => order.ClaimedByEmployee?.FullName ?? order.AssignedEmployee?.FullName;
    private IEnumerable<ServiceOrderWorkItem> Activities => order.WorkItems.OrderBy(w => w.SortOrder).ThenBy(w => w.Id);
    private IEnumerable<ServiceOrderChecklistItem> Checks => order.Checklist.OrderBy(i => i.SortOrder).ThenBy(i => i.Id);
    private static string Digest<T>(T data) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data)));

    // Fingerprint of the order data (including captured signatures), independent of PDF pagination and generation time.
    private string OrderHash() => Digest(new
    {
        Version = 1, order.Id, order.Title, order.Type, order.Status, order.Description,
        Created = order.CreatedAt.Ticks, End = order.EstimatedEndDate?.Ticks, Closed = order.FinalizedAt?.Ticks,
        order.ClientId, order.Client?.Name, order.Client?.ClientCode, order.Client?.Email, order.Client?.Phone,
        order.ClientServiceContractId, Contract?.ContractNumber, ServiceName = Contract == null ? null : Service,
        Event = EventContract.IsEvent(Contract), Contract?.Branch, Contract?.BranchAddress,
        EventStart = Contract?.ContractStartDate?.Ticks, EventEnd = Contract?.ContractEndDate?.Ticks,
        ServiceTypes = ClientServiceContractMetadata.ParseTechnical(Contract?.Notes).ServiceTypes,
        TechnicianName, order.LevantamientoNotes, order.MaterialesNotes, order.AdminReviewNotes,
        Activities = Activities.Select(w => new { w.Id, w.SortOrder, w.Title, w.Type, w.Description, w.WorkPerformed, w.MaterialsUsed, w.TechnicianNotes }),
        Checks = Checks.Select(i => new { i.Id, i.WorkItemId, i.SortOrder, i.Title, i.IsDone, i.Notes }),
        Evidence = attachments.Select(a => new { a.Id, a.Name, Ticks = a.UploadedAt.Ticks, ImageHash = a.Image == null ? null : Convert.ToHexString(SHA256.HashData(a.Image)) }),
        Signatures = new[] { technician, client }.Select(s => new { s.Name, Ticks = s.At?.Ticks, s.Hash })
    });

    internal byte[] Render(bool withImages)
    {
        var hash = OrderHash();
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.MarginHorizontal(34);
            page.MarginVertical(25);
            page.DefaultTextStyle(t => t.FontSize(10).FontColor(Ink));
            page.Header().BorderBottom(1).BorderColor(Rule).PaddingBottom(8).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Element(area =>
                {
                    if (withImages && logo is { Length: > 0 }) DrawImage(area.Width(120).Height(40), logo, company);
                    else area.Text(company).FontSize(17).Bold().FontColor(Navy);
                });
                row.RelativeItem().AlignMiddle().Column(c =>
                {
                    c.Item().AlignRight().Text("Orden de servicio").FontSize(20).Bold().FontColor(Navy);
                    c.Item().AlignRight().Text(Reference).FontSize(11).SemiBold().FontColor(Blue);
                });
            });
            page.Content().PaddingVertical(10).Column(c =>
            {
                c.Spacing(10);
                c.Item().Background(Navy).PaddingHorizontal(13).PaddingVertical(9).Column(title =>
                {
                    title.Item().Text(order.Title).FontSize(16).SemiBold().FontColor(Colors.White);
                    title.Item().PaddingTop(3).Text($"{DisplayType(order.Type)}   ·   {StatusLabel()}")
                        .FontSize(10).FontColor("#E1EFFA");
                });
                c.Item().Row(row =>
                {
                    row.Spacing(16);
                    row.RelativeItem().Column(info =>
                    {
                        Heading(info, "Cliente");
                        info.Item().Text(order.Client?.Name ?? "Sin cliente registrado").SemiBold();
                        TextLine(info, order.Client?.ClientCode);
                        TextLine(info, order.Client?.Email);
                        TextLine(info, order.Client?.Phone);
                    });
                    row.RelativeItem().Column(info =>
                    {
                        Heading(info, "Datos de atención");
                        Field(info, "Técnico", TechnicianName);
                        Field(info, "Creación", Moment(order.CreatedAt));
                        Field(info, "Término estimado", order.EstimatedEndDate?.ToString("dd/MM/yyyy"));
                        if (order.FinalizedAt.HasValue) Field(info, "Cierre", Moment(order.FinalizedAt.Value));
                    });
                });
                if (Contract != null) ContractSection(c);
                if (!string.IsNullOrWhiteSpace(order.Description))
                    c.Item().EnsureSpace(50).Column(x => { Heading(x, "Descripción del trabajo"); x.Item().Text(order.Description); });
                if (!string.IsNullOrWhiteSpace(order.LevantamientoNotes) || !string.IsNullOrWhiteSpace(order.MaterialesNotes))
                    c.Item().EnsureSpace(50).Column(x =>
                    {
                        Heading(x, "Observaciones por etapa");
                        Field(x, "Levantamiento", order.LevantamientoNotes);
                        Field(x, "Materiales", order.MaterialesNotes);
                    });

                Checklist(c, Checks.Where(i => i.WorkItemId == null).ToList(), "Lista de verificación");
                foreach (var activity in Activities)
                {
                    c.Item().EnsureSpace(56).Column(x =>
                    {
                        Heading(x, $"Actividad {activity.SortOrder + 1} · {activity.Title}");
                        TextLine(x, DisplayType(activity.Type));
                        TextLine(x, activity.Description);
                        Field(x, "Trabajo realizado", activity.WorkPerformed);
                        Field(x, "Material utilizado", activity.MaterialsUsed);
                        Field(x, "Observaciones", activity.TechnicianNotes);
                        Checklist(x, Checks.Where(i => i.WorkItemId == activity.Id).ToList(), null);
                    });
                }
                if (attachments.Count > 0) Evidence(c, withImages);
                if (!string.IsNullOrWhiteSpace(order.AdminReviewNotes))
                    c.Item().EnsureSpace(50).Column(x => { Heading(x, "Notas de revisión"); x.Item().Text(order.AdminReviewNotes); });
                c.Item().ShowEntire().Background(Pale).BorderLeft(3).BorderColor(Blue).Padding(9).Column(x =>
                {
                    Heading(x, "NOC · Atención y soporte técnico");
                    x.Item().Text("WhatsApp y llamadas: 55 2601 4566 · WhatsApp es el canal preferente del NOC.").FontSize(9).SemiBold();
                    x.Item().Text("Correo: soporte@hubnet-solutions.com.mx").FontSize(9);
                    x.Item().Text("Portal de tickets: https://support.hubnet-solutions.net").FontSize(9);
                    x.Item().PaddingTop(3).Text($"Al reportar, indique su cliente {order.Client?.ClientCode ?? ""}, la orden {Reference}, la ubicación y una descripción de la incidencia.").FontSize(8).FontColor(Muted);
                });
                c.Item().ShowEntire().Column(x =>
                {
                    Heading(x, "Firmas de la orden");
                    x.Item().Row(row =>
                    {
                        row.Spacing(12);
                        SignatureCard(row.RelativeItem(), "Técnico", technician, withImages);
                        SignatureCard(row.RelativeItem(), "Cliente", client, withImages);
                    });
                });
            });
            page.Footer().BorderTop(1).BorderColor(Rule).PaddingTop(5).Column(c =>
            {
                c.Item().Row(row =>
                {
                    row.RelativeItem().Text($"{footer}   ·   {Reference}").FontSize(8).SemiBold().FontColor(Navy);
                    row.ConstantItem(90).AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                        t.Span("Página "); t.CurrentPageNumber(); t.Span(" de "); t.TotalPages();
                    });
                });
                c.Item().Text($"Huella de la orden · SHA-256   {hash}").FontSize(7.3f).FontColor(Muted);
            });
        })).WithMetadata(new DocumentMetadata
        {
            Title = $"Orden de servicio {Reference}", Author = company,
            Subject = $"Huella de la orden · SHA-256 {hash}"
        }).GeneratePdf();
    }

    private void ContractSection(ColumnDescriptor column)
    {
        var contract = Contract!;
        var isEvent = EventContract.IsEvent(contract);
        column.Item().EnsureSpace(65).Background(Pale).BorderLeft(3).BorderColor(Blue).Padding(9).Column(x =>
        {
            x.Item().Text(isEvent ? "SERVICIO CONTRATADO PARA EVENTO" : "SERVICIO CONTRATADO").FontSize(8).Bold().FontColor(Blue);
            x.Item().PaddingVertical(2).Text(Service).FontSize(13).SemiBold().FontColor(Navy);
            var types = ClientServiceContractMetadata.ParseTechnical(contract.Notes).ServiceTypes;
            if (types.Count > 1) Field(x, "Servicios incluidos", string.Join(" · ", types));
            Field(x, "Contrato", contract.ContractNumber);
            Field(x, "Sucursal", contract.Branch);
            Field(x, "Domicilio del servicio", contract.BranchAddress);
            if (isEvent && contract.ContractStartDate.HasValue && contract.ContractEndDate.HasValue)
                Field(x, "Periodo del evento", contract.ContractStartDate.Value.Date == contract.ContractEndDate.Value.Date
                    ? contract.ContractStartDate.Value.ToString("dd/MM/yyyy")
                    : $"{contract.ContractStartDate:dd/MM/yyyy} al {contract.ContractEndDate:dd/MM/yyyy}");
        });
    }

    private static void Heading(ColumnDescriptor c, string title) =>
        c.Item().PaddingBottom(4).Text(title).FontSize(11.5f).Bold().FontColor(Navy);

    private static void TextLine(ColumnDescriptor c, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) c.Item().Text(value).FontColor(Muted);
    }

    private static void Field(ColumnDescriptor c, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        c.Item().Text(t =>
        {
            t.Span(label + ": ").SemiBold().FontColor(Muted);
            t.Span(value);
        });
    }

    private static void Checklist(ColumnDescriptor c, IReadOnlyList<ServiceOrderChecklistItem> items, string? heading)
    {
        if (items.Count == 0) return;
        c.Item().EnsureSpace(58).Column(x =>
        {
            if (heading != null) Heading(x, heading);
            x.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(27); cols.RelativeColumn(1.55f);
                    cols.ConstantColumn(69); cols.RelativeColumn();
                });
                table.Header(h =>
                {
                    foreach (var label in new[] { "N.º", "Punto de verificación", "Estado", "Notas" })
                        h.Cell().Background(Navy).Padding(5).Text(label).FontSize(9).SemiBold().FontColor(Colors.White);
                });
                for (var n = 0; n < items.Count; n++)
                {
                    var item = items[n];
                    IContainer Cell() => table.Cell().Background(n % 2 == 0 ? Pale : Colors.White)
                        .BorderBottom(0.5f).BorderColor(Rule).Padding(5);
                    Cell().Text(item.SortOrder.ToString()).FontSize(9).FontColor(Muted);
                    Cell().Text(item.Title).FontSize(9);
                    Cell().Text(item.IsDone ? "Completado" : "Pendiente").FontSize(8.5f).SemiBold()
                        .FontColor(item.IsDone ? "#21604B" : Muted);
                    Cell().Text(item.Notes ?? "").FontSize(9);
                }
            });
        });
    }

    private void Evidence(ColumnDescriptor c, bool withImages)
    {
        c.Item().EnsureSpace(80).Column(x =>
        {
            Heading(x, "Evidencias");
            var photos = attachments.Where(a => withImages && a.Image is { Length: > 0 }).ToList();
            if (photos.Count > 0)
                x.Item().Table(t =>
                {
                    t.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.RelativeColumn(); });
                    foreach (var image in photos)
                        t.Cell().ShowEntire().Padding(3).Border(0.5f).BorderColor(Rule).Padding(5).Column(photo =>
                        {
                            DrawImage(photo.Item().Height(128).Background(Pale).AlignCenter().AlignMiddle(), image.Image!, "Imagen no disponible");
                            photo.Item().PaddingTop(3).Text(image.Name).FontSize(9).SemiBold();
                            photo.Item().Text(Moment(image.UploadedAt)).FontSize(8).FontColor(Muted);
                        });
                });
            foreach (var file in attachments.Except(photos))
                x.Item().PaddingVertical(2).Text($"{file.Name}   ·   {Moment(file.UploadedAt)}").FontSize(9);
        });
    }

    private static void SignatureCard(IContainer card, string role, Signature sig, bool withImages)
    {
        card.Border(0.7f).BorderColor(Rule).Padding(8).Column(x =>
        {
            x.Item().Text(role).FontSize(10).Bold().FontColor(Blue);
            if (!sig.At.HasValue)
            {
                x.Item().PaddingVertical(6).Text("Firma pendiente").FontSize(9).FontColor(Muted);
                return;
            }
            var visibleCapture = false;
            if (withImages && sig.Image is { Length: > 0 })
                visibleCapture = DrawImage(x.Item().Height(37).AlignLeft(), sig.Image, "Imagen de firma no disponible");
            else x.Item().PaddingVertical(4).Text("Imagen de firma no disponible").FontSize(8).FontColor(Muted);
            TextLine(x, sig.Name);
            x.Item().Text(Moment(sig.At.Value)).FontSize(8).FontColor(Muted);
            if (visibleCapture && sig.Hash != null)
            {
                x.Item().PaddingTop(3).Text("Huella de la firma · SHA-256").FontSize(7.5f).SemiBold().FontColor(Navy);
                x.Item().Text(sig.Hash[..32] + "\n" + sig.Hash[32..]).FontSize(7.5f).FontColor(Muted);
            }
        });
    }

    private static bool DrawImage(IContainer area, byte[] image, string unavailable)
    {
        try { area.Image(image).FitArea(); return true; }
        catch (DocumentComposeException)
        {
            area.Text(unavailable).FontSize(8).FontColor(Muted);
            return false;
        }
    }

    private static string Moment(DateTime date) => date.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    private static string DisplayType(ServiceOrderType type) => type switch
    {
        ServiceOrderType.NuevaInstalacion => "Nueva instalación",
        ServiceOrderType.LevantamientoTecnico => "Levantamiento técnico",
        ServiceOrderType.Global => "Global (múltiple)",
        _ => type.GetDisplayName()
    };
    private string StatusLabel() => order.Status switch
    {
        ServiceOrderStatus.Finalized => "Finalizada",
        ServiceOrderStatus.InReview => "En revisión",
        _ => order.Status.GetDisplayName()
    };
}
