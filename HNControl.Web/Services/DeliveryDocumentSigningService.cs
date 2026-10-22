using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public class DeliveryDocumentSigningService(ApplicationDbContext db, IFileStorage storage,
    ITemplateDocxService templates, IOfficePdfConverter converter, IProjectDeliveryPdfRenderer evidenceRenderer,
    IEmailSender email, DocumentNotificationRecipients recipients)
{
    public async Task SavePdfAsync(ProjectDeliveryFormat item)
    {
        if (item.Client == null) throw new InvalidOperationException("No se encontró al cliente del acta.");
        item.SigningSnapshotJson ??= DeliverySigningSnapshot.Capture(item.Client, item.Project);
        var docx = templates.BuildDeliveryDocx(item, item.Client, item.Project);
        var pdf = await converter.TryConvertDocxToPdfAsync(docx, $"acta_{item.Id:N}");
        if (pdf is not { Length: > 0 })
            throw new InvalidOperationException("No se pudo convertir el acta Word a PDF. Revise LibreOffice en el servidor. La firma no se ha guardado.");
        pdf = await evidenceRenderer.AppendEvidencePagesAsync(item, pdf);
        var (path, _, _) = await storage.SaveBytesAsync(pdf, $"projects/delivery/{item.Id}",
            $"acta_{item.Id:N}_{Guid.NewGuid():N}.pdf", "application/pdf");
        item.PdfStoragePath = path;
        item.PdfGeneratedAt = DateTime.UtcNow;
        if (item.SignedAt.HasValue || item.ProviderSignedAt.HasValue)
            item.Status = item.IsFullySigned ? ProjectDeliveryFormatStatus.Signed : ProjectDeliveryFormatStatus.SentForSignature;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task NotifyCompletedAsync(Guid id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var query = db.Database.IsNpgsql()
            ? db.ProjectDeliveryFormats.FromSqlInterpolated($"SELECT * FROM \"ProjectDeliveryFormats\" WHERE \"Id\" = {id} FOR UPDATE")
            : db.ProjectDeliveryFormats.Where(x => x.Id == id);
        var item = await query.Include(x => x.Client).SingleAsync();
        await db.Entry(item).ReloadAsync();
        if (!item.IsFullySigned || item.CompletionEmailSentAt.HasValue)
        {
            await transaction.CommitAsync();
            return;
        }
        if (item.Client == null || string.IsNullOrWhiteSpace(item.PdfStoragePath) || !item.PdfGeneratedAt.HasValue
            || item.PdfGeneratedAt < item.SignedAt || item.PdfGeneratedAt < item.ProviderSignedAt)
            throw new InvalidOperationException("Todavía no está disponible el acta PDF con ambas firmas.");
        var recipient = ClientCommunicationTemplates.Recipient(item.Client);
        if (!ClientCommunicationTemplates.IsEmail(recipient))
            throw new InvalidOperationException("Registre un correo válido del cliente antes de enviar la confirmación.");
        var copies = (await recipients.GetAsync()).ToList();
        foreach (var address in new[] { item.ReceiverEmail, item.SignedByEmail })
            if (ClientCommunicationTemplates.IsEmail(address)) copies.Add(address!);
        var fileName = $"Acta_entrega_firmada_{item.Id:N}.pdf";
        var body = ClientCommunicationTemplates.Layout("Acta de entrega firmada", $"""
            <p>Estimado(a) <strong>{ClientCommunicationTemplates.E(item.Client.Name)}</strong>:</p>
            <p>Confirmamos que el cliente y HN Solutions han completado las firmas del acta <strong>{ClientCommunicationTemplates.E(item.Title)}</strong>.</p>
            <p>Firma del cliente: {ClientCommunicationTemplates.E(item.SignedByName)} · {item.SignedAt:dd/MM/yyyy HH:mm} UTC<br>
            Firma de HN Solutions: {ClientCommunicationTemplates.E(item.ProviderSignedByName)} · {item.ProviderSignedAt:dd/MM/yyyy HH:mm} UTC</p>
            <p>Adjuntamos el acta final firmada en PDF para su resguardo, con el detalle de los servicios, equipos y evidencias de entrega.</p>
            <p>Quedamos a su disposición para cualquier aclaración.</p>
            """);
        var (stream, _, _) = await storage.OpenAsync(item.PdfStoragePath, fileName);
        await using (stream)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            await email.SendAsync(recipient, $"Acta de entrega firmada: {item.Title}", body,
                buffer.ToArray(), fileName, "application/pdf", copies.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }
        item.CompletionEmailSentAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
