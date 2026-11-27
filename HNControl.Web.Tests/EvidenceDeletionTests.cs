using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Pages.Admin.ServiceOrders;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using static ClientCommunicationTests;

internal static class EvidenceDeletionTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var files = new MemoryFiles();
        var client = new Client { Name = "Cliente demo" };
        var order = new ServiceOrder { Client = client, Title = "Orden firmada", PublicToken = "test-signed", Status = ServiceOrderStatus.Finalized,
            PdfStoragePath = "anterior.pdf", PdfGeneratedAt = DateTime.UtcNow, FinalizedAt = DateTime.UtcNow };
        var other = new ServiceOrder { Client = client, Title = "Otra orden", PublicToken = "test-other" };
        db.AddRange(order, other);
        db.ServiceOrderSignatures.AddRange(new ServiceOrderSignature { Order = order, Role = SignatureRole.Client, SignedByName = "Cliente", StoragePath = "cliente.png" },
            new ServiceOrderSignature { Order = order, Role = SignatureRole.Technician, SignedByName = "Técnico", StoragePath = "tecnico.png" });
        async Task<ServiceOrderEvidence> Evidence(ServiceOrder parent, string? shared = null)
        {
            var id = Guid.NewGuid();
            var path = shared ?? (await files.SaveBytesAsync("image"u8.ToArray(), "evidence", id + ".png", "image/png")).storagePath;
            var evidence = new ServiceOrderEvidence { Id = id, Order = parent, StoragePath = path, OriginalFileName = "Prueba 'con comillas'.png" };
            db.Add(evidence); await db.SaveChangesAsync(); return evidence;
        }
        DetailsModel Page(string? role = AppRoles.Admin)
        {
            var claims = role == null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "admin-demo"), new Claim(ClaimTypes.Name, "Admin demo") };
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, role == null ? null : "test")) };
            http.Request.QueryString = new QueryString("?panel=1");
            return new DetailsModel(db, null!, files, null!, null!, null!)
            { PageContext = new PageContext { HttpContext = http }, TempData = new TempDataDictionary(http, new MemoryTempData()) };
        }
        var selected = await Evidence(order);
        var keep = await Evidence(order);
        var foreign = await Evidence(other);
        foreach (var role in new[] { null, AppRoles.Employee, AppRoles.Seller })
            check(await Page(role).OnPostDeleteEvidenceAsync(order.Id, selected.Id) is ForbidResult,
                "Evidencias: rechaza eliminación por usuario anónimo o sin rol de Administración");
        check(await Page().OnPostDeleteEvidenceAsync(order.Id, foreign.Id) is NotFoundResult && await db.ServiceOrderEvidences.CountAsync() == 3,
            "Evidencias: no permite eliminar un adjunto de otra orden");
        check(await Page().OnPostDeleteEvidenceAsync(Guid.NewGuid(), selected.Id) is NotFoundResult,
            "Evidencias: orden inexistente no modifica adjuntos");
        var finalized = order.FinalizedAt;
        var page = Page();
        var result = await page.OnPostDeleteEvidenceAsync(order.Id, selected.Id);
        check(result is RedirectToPageResult redirect && (string?)redirect.RouteValues!["panel"] == "1",
            "Evidencias: redirección conserva el modal después de eliminar");
        check(!await db.ServiceOrderEvidences.AnyAsync(e => e.Id == selected.Id) && await db.ServiceOrderEvidences.CountAsync() == 2,
            "Evidencias: elimina únicamente el registro seleccionado");
        var absent = false;
        try { await files.OpenAsync(selected.StoragePath, "test"); } catch (KeyNotFoundException) { absent = true; }
        check(absent, "Evidencias: retira el archivo del almacenamiento");
        using var keptFile = (await files.OpenAsync(keep.StoragePath, "test")).stream;
        check(keptFile.Length > 0 && order.Status == ServiceOrderStatus.Finalized && order.FinalizedAt == finalized
            && await db.ServiceOrderSignatures.CountAsync() == 2, "Evidencias: conserva otros archivos, ambas firmas y finalización");
        check(order.PdfStoragePath == null && order.PdfGeneratedAt == null && page.EvidenceNotice!.Contains("PDF"),
            "Evidencias: invalida PDF anterior e informa que debe regenerarse");
        var audit = await db.ServiceOrderAuditLogs.SingleAsync();
        check(audit.EventType == ServiceOrderAuditEvent.EvidenceDeleted && audit.OrderId == order.Id && audit.ActorId == "admin-demo"
            && audit.Details!.Contains(selected.Id.ToString()) && audit.Details.Contains(selected.OriginalFileName),
            "Evidencias: registra actor, orden e identidad del adjunto eliminado");
        check(await Page().OnPostDeleteEvidenceAsync(order.Id, selected.Id) is NotFoundResult && await db.ServiceOrderAuditLogs.CountAsync() == 1,
            "Evidencias: un doble envío no elimina otros adjuntos ni duplica auditoría");
        var shared = await Evidence(other, keep.StoragePath);
        await Page(AppRoles.SuperAdmin).OnPostDeleteEvidenceAsync(order.Id, keep.Id);
        using var sharedFile = (await files.OpenAsync(shared.StoragePath, "test")).stream;
        check(sharedFile.Length > 0 && await db.ServiceOrderEvidences.AnyAsync(e => e.Id == shared.Id),
            "Evidencias: SuperAdmin elimina el vínculo y conserva archivos todavía usados por otro adjunto");
        var download = new DownloadEvidenceModel(db, files);
        check(await download.OnGetAsync(selected.Id) is NotFoundResult, "Evidencias: enlace del adjunto eliminado deja de descargarlo");
    }
}
