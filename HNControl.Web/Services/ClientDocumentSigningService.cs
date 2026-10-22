using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public class ClientDocumentSigningService(
    ApplicationDbContext db, IFileStorage storage, ITemplateDocxService templates,
    IOfficePdfConverter converter, IEmailSender email, DocumentNotificationRecipients recipients)
{
    public static bool IsCompleted(ClientLegalDocument doc) => doc.SignedAt.HasValue
        && (doc.DocumentType == ClientLegalDocumentType.NDA || doc.ProviderSignedAt.HasValue);

    public async Task SaveSignedPdfAsync(ClientLegalDocument doc)
    {
        if (doc.Client == null) throw new InvalidOperationException("No se encontró al cliente.");
        doc.SigningSnapshotJson ??= ClientSigningSnapshot.Capture(doc.Client, doc.ClientServiceContract);
        doc.Status = IsCompleted(doc) ? ClientLegalDocumentStatus.Signed : ClientLegalDocumentStatus.SentForSignature;
        var word = templates.BuildClientLegalDocx(doc, doc.Client, doc.ClientServiceContract);
        var pdf = await converter.TryConvertDocxToPdfAsync(word, $"legal_{doc.Id:N}");
        if (pdf is not { Length: > 0 })
            throw new InvalidOperationException("No se pudo generar el PDF firmado. Verifique LibreOffice en el servidor y vuelva a intentar; la firma aún no se ha guardado.");

        var (path, _, _) = await storage.SaveBytesAsync(pdf, $"clients/{doc.ClientId}/legal",
            $"legal_{doc.Id:N}_{Guid.NewGuid():N}.pdf", "application/pdf");
        doc.PdfStoragePath = path;
        doc.PdfGeneratedAt = DateTime.UtcNow;
        doc.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // The row lock serializes retries on different application instances as well.
    public async Task NotifyCompletedAsync(Guid docId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var query = db.Database.IsNpgsql()
            ? db.ClientLegalDocuments.FromSqlInterpolated($"SELECT * FROM \"ClientLegalDocuments\" WHERE \"Id\" = {docId} FOR UPDATE")
            : db.ClientLegalDocuments.Where(x => x.Id == docId);
        var doc = await query.Include(x => x.Client).SingleAsync();
        await db.Entry(doc).ReloadAsync();
        if (!IsCompleted(doc) || doc.CompletionEmailSentAt.HasValue)
        {
            await transaction.CommitAsync();
            return;
        }
        if (doc.Client == null || string.IsNullOrWhiteSpace(doc.PdfStoragePath)
            || !doc.PdfGeneratedAt.HasValue || doc.PdfGeneratedAt < doc.SignedAt
            || (doc.ProviderSignedAt.HasValue && doc.PdfGeneratedAt < doc.ProviderSignedAt))
            throw new InvalidOperationException("El PDF final con las firmas todavía no está disponible.");
        var recipient = ClientCommunicationTemplates.Recipient(doc.Client);
        if (!ClientCommunicationTemplates.IsEmail(recipient))
            throw new InvalidOperationException("Registre un correo válido del cliente para enviar la confirmación.");
        var copies = (await recipients.GetAsync()).ToList();
        if (ClientCommunicationTemplates.IsEmail(doc.SignedByEmail)) copies.Add(doc.SignedByEmail!);
        var fileName = $"{(doc.DocumentType == ClientLegalDocumentType.Contract ? "Contrato" : "NDA")}_firmado_{doc.Id:N}.pdf";
        var (stream, _, _) = await storage.OpenAsync(doc.PdfStoragePath, fileName);
        await using (stream)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            await email.SendAsync(recipient, $"Documento firmado: {doc.Title}",
                ClientCommunicationTemplates.Completed(doc), buffer.ToArray(), fileName, "application/pdf", copies);
        }
        doc.CompletionEmailSentAt = DateTime.UtcNow;
        doc.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
