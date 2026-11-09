using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using HNControl.Web.Pages.Admin.ServiceOrders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

internal static class ServiceOrderReportEmailTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var folder = Path.GetFullPath("artifacts/service-order-report-email");
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:BasePath"] = folder }).Build();
        var storage = new LocalFileStorage(cfg);
        var email = new RecordingEmail();
        var renderer = new RecordingRenderer();
        var notices = new ServiceOrderCreationNotifications(db, email, cfg, NullLogger<ServiceOrderCreationNotifications>.Instance);
        var settings = new SystemConfiguration { EngineeringNotificationEmails = "admin@example.test; ADMIN@example.test; operaciones@example.test; CLIENTE@example.test" };
        var order = new ServiceOrder
        {
            Client = new Client { Name = "Cliente <prueba>", ClientCode = "HN-DEMO", Email = "cliente@example.test" },
            Title = "Internet <20 MB>\r\nSucursal", Status = ServiceOrderStatus.Finalized, PdfStoragePath = "old.pdf"
        };
        db.AddRange(settings, order);
        notices.QueueCreated(order);
        await db.SaveChangesAsync();
        DetailsModel Page() => new(db, renderer, storage, email, cfg, notices)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };
        var page = Page();
        await page.OnGetAsync(order.Id);
        check(page.PdfEmailRecipient == "cliente@example.test" && page.PdfEmailCopies.SequenceEqual(new[] { "admin@example.test", "operaciones@example.test" }),
            "PDF OS: muestra cliente y copias de Ingeniería sin duplicar direcciones");
        await page.OnPostSendEmailAsync(order.Id);
        check(email.Count == 1 && email.To == "cliente@example.test" && email.Cc.SequenceEqual(page.PdfEmailCopies),
            "PDF OS: envía al cliente con todos los destinatarios configurados en CC");
        check(renderer.Count == 1 && email.Attachment!.SequenceEqual(renderer.Bytes) && order.PdfStoragePath != "old.pdf" && order.PdfGeneratedAt.HasValue,
            "PDF OS: regenera y adjunta el PDF actual en lugar de la copia antigua");
        check(email.Body.Contains("Cliente &lt;prueba&gt;") && email.Body.Contains("Internet &lt;20 MB&gt;") && !email.Subject.Contains('\n')
            && email.Body.Contains("https://wa.me/525526014566") && email.Body.Contains("soporte@hubnet-solutions.com.mx")
            && email.Body.Contains("https://support.hubnet-solutions.net") && email.Body.Contains("background:#102c4a"),
            "PDF OS: correo corporativo con NOC, datos codificados y asunto seguro");
        check(page.Info!.Contains("admin@example.test") && page.Info.Contains("cliente@example.test"), "PDF OS: confirma los destinatarios reales del envío");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "correo-cliente.html"), email.Body);

        settings.EngineeringNotificationEmails = "";
        await db.SaveChangesAsync();
        await Page().OnPostSendEmailAsync(order.Id);
        check(email.Cc.Count == 0, "PDF OS: lista de Ingeniería vacía desactiva copias sin agregar correos fijos");
        settings.EngineeringNotificationEmails = "nuevo@example.test";
        order.Client!.Email = "";
        order.Client.BillingEmail = "facturacion@example.test";
        await db.SaveChangesAsync();
        await Page().OnPostSendEmailAsync(order.Id);
        check(email.To == "facturacion@example.test" && email.Cc.SequenceEqual(new[] { "nuevo@example.test" }),
            "PDF OS: toma cambios de Configuración y correo alternativo del cliente");
        var sent = email.Count;
        order.Client.BillingEmail = "invalido";
        await db.SaveChangesAsync();
        await Page().OnPostSendEmailAsync(order.Id);
        check(email.Count == sent, "PDF OS: no envía si el cliente no tiene un correo válido");
        order.Client.BillingEmail = "facturacion@example.test";
        settings.EngineeringNotificationEmails = "invalido";
        await db.SaveChangesAsync();
        await Page().OnPostSendEmailAsync(order.Id);
        check(email.Count == sent, "PDF OS: no envía si las copias configuradas son inválidas");
        settings.EngineeringNotificationEmails = "nuevo@example.test";
        await db.SaveChangesAsync();
        email.Fail = true;
        page = Page();
        await page.OnPostSendEmailAsync(order.Id);
        check(page.Info!.StartsWith("No se pudo confirmar") && email.Count == sent, "PDF OS: fallo SMTP no se anuncia como correo enviado");
        check(order.Status == ServiceOrderStatus.Finalized, "PDF OS: envío no modifica estado operativo de la orden");
    }

    private sealed class RecordingRenderer : IServiceOrderPdfRenderer
    {
        public int Count;
        public byte[] Bytes = "%PDF-test-current"u8.ToArray();
        public Task<byte[]> RenderAsync(ServiceOrder order) { Count++; return Task.FromResult(Bytes); }
    }
    private sealed class RecordingEmail : IEmailSender
    {
        public int Count;
        public bool Fail;
        public string To = "", Subject = "", Body = "";
        public IReadOnlyCollection<string> Cc = Array.Empty<string>();
        public byte[]? Attachment;
        public Task SendAsync(string toEmail, string subject, string htmlBody, byte[]? attachmentBytes = null,
            string? attachmentName = null, string? attachmentContentType = null, IReadOnlyCollection<string>? ccEmails = null)
        {
            if (Fail) throw new InvalidOperationException("SMTP no disponible");
            Count++; To = toEmail; Subject = subject; Body = htmlBody; Attachment = attachmentBytes;
            Cc = ccEmails ?? Array.Empty<string>();
            return Task.CompletedTask;
        }
    }
}
