using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Pages.Clients;
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

internal static class WelcomeContractSelectionTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var email = new RecordingEmail();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["PublicLinks:BaseUrl"] = "https://example.test" }).Build();
        var client = new Client { Name = "CONNECT PRUEBA", ClientCode = "HN-0017", Email = "cliente@example.test" };
        ClientServiceContract Contract(string number, string branch, int term) => new()
        {
            Client = client, ContractNumber = number, Label = "Internet Dedicado " + branch,
            Branch = branch, Notes = $"[META] Plazo={term}"
        };
        var first = Contract("HN-0017-01", "Tehuacán", 12);
        var second = Contract("HN-0017-02", "Cuautla", 24);
        var third = Contract("HN-0017-03", "Monclova", 36);
        var originalTime = DateTime.UtcNow.AddDays(-10);
        ClientLegalDocument Document(ClientServiceContract contract, string? pdf) => new()
        {
            Client = client, ClientServiceContract = contract, Title = "Contrato " + contract.Branch,
            PublicToken = Guid.NewGuid().ToString("N"), PdfStoragePath = pdf,
            UpdatedAt = originalTime, TokenExpiresAt = originalTime, Status = ClientLegalDocumentStatus.Draft
        };
        var firstDoc = Document(first, "first.pdf");
        var secondDoc = Document(second, "second.pdf");
        var thirdDoc = Document(third, null);
        var nda = Document(second, "nda.pdf");
        nda.DocumentType = ClientLegalDocumentType.NDA;
        var otherClient = new Client { Name = "Otro cliente", Email = "otro@example.test" };
        var foreign = new ClientServiceContract { Client = otherClient, Label = "Contrato ajeno" };
        db.AddRange(firstDoc, secondDoc, thirdDoc, nda, foreign);
        await db.SaveChangesAsync();
        WelcomeModel Page(Guid? selected = null)
        {
            var model = new WelcomeModel(db, null!, email, configuration, NullLogger<WelcomeModel>.Instance, new DocumentNotificationRecipients(db))
                { ContractId = selected };
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, AppRoles.Admin) }, "test"))
            };
            model.PageContext = new PageContext { HttpContext = context };
            model.TempData = new TempDataDictionary(context, new MemoryTempData());
            return model;
        }
        var choose = Page();
        await choose.OnGetAsync(client.Id);
        check(choose.ContractOptions.Count == 3 && choose.ContractId == null && !choose.CanSend && choose.HtmlBody == "",
            "Varios contratos requieren selección antes de mostrar o enviar bienvenida");
        check(choose.ContractOptions.Any(x => x.Id == second.Id && x.Label.Contains("HN-0017-02") && x.Label.Contains("Sucursal: Cuautla")),
            "Selector identifica contrato por número, servicio y sucursal");
        check(await Page().OnPostAsync(client.Id) is PageResult && email.Messages.Count == 0
            && firstDoc.UpdatedAt == originalTime && secondDoc.UpdatedAt == originalTime,
            "POST sin selección no envía ni cambia documentos");
        check(await Page(foreign.Id).OnPostAsync(client.Id) is PageResult && email.Messages.Count == 0,
            "No permite enviar bienvenida de un contrato de otro cliente");
        check(await Page(Guid.NewGuid()).OnPostAsync(client.Id) is PageResult && email.Messages.Count == 0,
            "Contrato inexistente o eliminado bloquea el envío");
        var malformed = Page();
        malformed.ModelState.AddModelError("ContractId", "Invalid GUID");
        check(await malformed.OnPostAsync(client.Id) is PageResult && email.Messages.Count == 0,
            "Identificador malformado no permite enviar bienvenida");
        var selected = Page(second.Id);
        await selected.OnGetAsync(client.Id);
        check(selected.CanSend && selected.HtmlBody.Contains(secondDoc.PublicToken) && selected.HtmlBody.Contains("24 meses")
            && !selected.HtmlBody.Contains(firstDoc.PublicToken) && !selected.HtmlBody.Contains(thirdDoc.PublicToken)
            && !selected.HtmlBody.Contains(nda.PublicToken), "Vista previa contiene solo el contrato elegido y su plazo");
        check(secondDoc.Status == ClientLegalDocumentStatus.Draft && secondDoc.UpdatedAt == originalTime,
            "Vista previa no activa ni renueva enlaces de firma");
        check(await selected.OnPostAsync(client.Id) is RedirectToPageResult && email.Messages.Count == 1
            && email.Bodies[0] == selected.HtmlBody && email.Bodies[0].Contains(secondDoc.PublicToken)
            && !email.Bodies[0].Contains(firstDoc.PublicToken), "SMTP recibe únicamente la bienvenida del contrato seleccionado");
        check(secondDoc.SentAt.HasValue && secondDoc.Status == ClientLegalDocumentStatus.SentForSignature
            && secondDoc.TokenExpiresAt > DateTime.UtcNow && firstDoc.SentAt == null && thirdDoc.SentAt == null
            && nda.SentAt == null && firstDoc.UpdatedAt == originalTime && thirdDoc.UpdatedAt == originalTime,
            "Envío actualiza solo el documento elegido y conserva los demás");
        check(email.Messages[0].Cc.Count == 2, "Selección conserva las copias internas configuradas");
        var withoutPdf = Page(third.Id);
        check(await withoutPdf.OnPostAsync(client.Id) is PageResult && !withoutPdf.CanSend
            && withoutPdf.Error!.Contains("PDF") && email.Messages.Count == 1,
            "Contrato seleccionado sin PDF muestra instrucción y no envía otro contrato");
        var newContract = Contract("HN-0017-04", "Sin documento", 12);
        db.Add(newContract); await db.SaveChangesAsync();
        check(await Page(newContract.Id).OnPostAsync(client.Id) is PageResult && email.Messages.Count == 1,
            "Servicio sin documento legal no usa las ligas de otro servicio");
        var general = new ClientLegalDocument { Client = client, PublicToken = "general-test", PdfStoragePath = "general.pdf", Title = "Contrato general" };
        db.Add(general); await db.SaveChangesAsync();
        var legacy = Page(Guid.Empty);
        await legacy.OnGetAsync(client.Id);
        check(legacy.CanSend && legacy.HtmlBody.Contains(general.PublicToken) && !legacy.HtmlBody.Contains(secondDoc.PublicToken),
            "Contrato general anterior sigue disponible como selección independiente");
        email.Fail = true;
        var failed = Page(first.Id);
        check(await failed.OnPostAsync(client.Id) is PageResult && failed.ContractId == first.Id
            && failed.Error != null && failed.HtmlBody.Contains(firstDoc.PublicToken) && firstDoc.SentAt == null,
            "Fallo SMTP conserva selección y no marca el correo como enviado");
        email.Fail = false;
        var singleClient = new Client { Name = "Contrato único", ClientCode = "HN-0099", Email = "unico@example.test" };
        var singleContract = new ClientServiceContract { Client = singleClient, Label = "Servicio único" };
        db.Add(new ClientLegalDocument { Client = singleClient, ClientServiceContract = singleContract, PublicToken = "single-test", PdfStoragePath = "single.pdf" });
        await db.SaveChangesAsync();
        var single = Page();
        await single.OnGetAsync(singleClient.Id);
        check(single.ContractId == singleContract.Id && single.CanSend, "Cliente con un solo contrato lo tiene preseleccionado");
    }
}
