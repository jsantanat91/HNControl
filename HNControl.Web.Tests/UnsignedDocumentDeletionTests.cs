using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Pages.Clients;
using HNControl.Web.Pages.Public;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using static ClientCommunicationTests;

internal static class UnsignedDocumentDeletionTests
{
    public static async Task RunAsync(ITemplateDocxService templates, Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var files = new MemoryFiles();
        var client = new Client { Name = "Cliente prueba", ClientCode = "HN-DELETE-01", OwnerUserId = "seller" };
        var otherClient = new Client { Name = "Cliente ajeno", ClientCode = "HN-DELETE-02", OwnerUserId = "other" };
        db.Users.AddRange(new ApplicationUser { Id = "seller", UserName = "seller" }, new ApplicationUser { Id = "other", UserName = "other" });
        var contract = new ClientServiceContract { Client = client, Label = "Internet de prueba" };
        db.AddRange(client, otherClient, contract);
        await db.SaveChangesAsync();
        async Task<ClientLegalDocument> Document(bool signed = false, bool providerSigned = false)
        {
            var item = new ClientLegalDocument { Client = client, ClientServiceContract = contract,
                Title = "Contrato de prueba", PublicToken = Guid.NewGuid().ToString("N"),
                Status = ClientLegalDocumentStatus.SentForSignature, TokenExpiresAt = DateTime.UtcNow.AddDays(7),
                SignedAt = signed ? DateTime.UtcNow : null, ProviderSignedAt = providerSigned ? DateTime.UtcNow : null };
            item.PdfStoragePath = (await files.SaveBytesAsync("test PDF"u8.ToArray(), "test", item.Id + ".pdf", "application/pdf")).storagePath;
            db.Add(item); await db.SaveChangesAsync(); return item;
        }
        DocumentsModel Page(string role = AppRoles.Admin, string userId = "admin", params string[] actions)
        {
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, userId) }, "test")) };
            return new DocumentsModel(db, null!, files, null!, new ConfigurationBuilder().Build(), null!, null!, null!,
                new GrantedActions(actions), NullLogger<DocumentsModel>.Instance, new DocumentNotificationRecipients(db))
            { PageContext = new PageContext { HttpContext = context }, TempData = new TempDataDictionary(context, new MemoryTempData()) };
        }
        var unsigned = await Document();
        check(await Page().OnPostDeleteAsync(otherClient.Id, unsigned.Id) is NotFoundResult && await db.ClientLegalDocuments.AnyAsync(x => x.Id == unsigned.Id),
            "Eliminar documento no acepta identificadores de otro cliente");
        check(await Page(AppRoles.Employee, "seller").OnPostDeleteAsync(client.Id, unsigned.Id) is ForbidResult,
            "Empleado sin permiso de edición no elimina documentos");
        check(await Page(AppRoles.Employee, "other", AppActions.ClientsEdit, AppActions.ClientsViewOwn).OnPostDeleteAsync(client.Id, unsigned.Id) is ForbidResult,
            "Permiso de clientes propios no permite eliminar documento ajeno");
        check(await Page(AppRoles.Employee, "seller", AppActions.ClientsEdit, AppActions.ClientsViewOwn).OnPostDeleteAsync(client.Id, unsigned.Id) is RedirectToPageResult
            && !await db.ClientLegalDocuments.AnyAsync(x => x.Id == unsigned.Id)
            && await db.ClientServiceContracts.AnyAsync(x => x.Id == contract.Id),
            "Documento sin firmas se elimina por dueño autorizado y conserva servicio");
        bool removedPdf;
        try { await files.OpenAsync(unsigned.PdfStoragePath!, "test.pdf"); removedPdf = false; }
        catch (KeyNotFoundException) { removedPdf = true; }
        check(removedPdf, "Se retira PDF del documento eliminado");
        var publicPage = new ClientDocumentModel(db, files, null!, NullLogger<ClientDocumentModel>.Instance);
        check(await publicPage.OnGetAsync(unsigned.PublicToken) is NotFoundResult
            && await publicPage.OnGetDownloadPdfAsync(unsigned.PublicToken) is NotFoundResult
            && await publicPage.OnPostSignAsync(unsigned.PublicToken, "Ana Pérez", "ana@example.test", "", true) is NotFoundResult,
            "Enlace eliminado ya no permite leer, descargar ni firmar");
        foreach (var (signed, provider) in new[] { (true, false), (false, true), (true, true) })
        {
            var item = await Document(signed, provider);
            var page = Page(); await page.OnPostDeleteAsync(client.Id, item.Id);
            check(await db.ClientLegalDocuments.AnyAsync(x => x.Id == item.Id) && page.FlashType == "warning",
                $"No elimina documento con firmas cliente={signed}, proveedor={provider}");
        }
        var inconsistent = await Document();
        inconsistent.Status = ClientLegalDocumentStatus.Signed;
        await db.SaveChangesAsync();
        await Page().OnPostDeleteAsync(client.Id, inconsistent.Id);
        check(await db.ClientLegalDocuments.AnyAsync(x => x.Id == inconsistent.Id), "Estado firmado también impide eliminación");

        // Simulate deletion while the public signature PDF is being converted.
        var racing = await Document();
        var email = new RecordingEmail();
        var converter = new DeleteDuringConversion(async () => {
            await using var deletingDb = new ApplicationDbContext(options);
            await deletingDb.ClientLegalDocuments.Where(x => x.Id == racing.Id).ExecuteDeleteAsync();
        });
        var signing = new ClientDocumentSigningService(db, files, templates, converter, email, new DocumentNotificationRecipients(db));
        var racePage = new ClientDocumentModel(db, files, signing, NullLogger<ClientDocumentModel>.Instance);
        var png = File.ReadAllBytes("HNControl.Web.Tests/Fixtures/contract-signature.png");
        check(await racePage.OnPostSignAsync(racing.PublicToken, "Ana Pérez", "ana@example.test", "data:image/png;base64," + Convert.ToBase64String(png), true) is NotFoundResult
            && email.Messages.Count == 0, "Eliminación concurrente a firma responde no disponible, sin confirmar ni enviar correo");
    }

    private sealed class GrantedActions(IEnumerable<string> values) : IActionAccessService
    {
        private readonly HashSet<string> granted = new(values);
        public Task<bool> HasActionAsync(ClaimsPrincipal user, string actionKey) => Task.FromResult(granted.Contains(actionKey));
        public Task<HashSet<string>> GetAllowedActionsAsync(ClaimsPrincipal user) => Task.FromResult(granted);
    }
    private sealed class DeleteDuringConversion(Func<Task> delete) : IOfficePdfConverter
    {
        public async Task<byte[]?> TryConvertDocxToPdfAsync(byte[] docxBytes, string baseFileName, CancellationToken cancellationToken = default)
        { await delete(); return RecordingConverter.Pdf; }
    }
}
