using System.Security.Claims;
using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using AdminCreate = HNControl.Web.Pages.Admin.ServiceOrders.CreateModel;
using AdminDetails = HNControl.Web.Pages.Admin.ServiceOrders.DetailsModel;
using FeasibilityPage = HNControl.Web.Pages.Sales.FeasibilityModel;
using TicketsPage = HNControl.Web.Pages.Tickets.IndexModel;

internal static class ServiceOrderCreationPathsTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["PublicLinks:BaseUrl"] = "https://control.example.test" }).Build();
        var email = new FailingEmail();
        var notices = new ServiceOrderCreationNotifications(db, email, config, NullLogger<ServiceOrderCreationNotifications>.Instance);
        var client = new Client { Name = "Cliente de órdenes", Email = "cliente@example.test", IsActive = true };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        T Page<T>(T page, string? role = AppRoles.Admin) where T : PageModel
        {
            var claims = role == null ? Array.Empty<Claim>() : new[]
                { new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "order-admin") };
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, role == null ? null : "test")) };
            page.PageContext = new PageContext { HttpContext = context };
            page.TempData = new TempDataDictionary(context, new ClientCommunicationTests.MemoryTempData());
            return page;
        }
        async Task<ServiceOrderScheduleSnapshot?> RequestSnapshot(Guid id)
        {
            var audit = await db.ServiceOrderAuditLogs.AsNoTracking().SingleAsync(x => x.OrderId == id
                && x.EventType == ServiceOrderAuditEvent.CreationNotificationRequested);
            return JsonSerializer.Deserialize<ServiceOrderScheduleSnapshot>(audit.Details!);
        }
        bool PendingNotice(PageModel page) => page.TempData[ServiceOrderCreationNotifications.NoticeKey]?.ToString()
            .Contains("envío pendiente", StringComparison.OrdinalIgnoreCase) == true;

        var create = Page(new AdminCreate(db, notices));
        var selectedDay = new DateTime(2026, 10, 28);
        create.Input = new AdminCreate.InputModel
        {
            ClientId = client.Id, Title = "Alta administrativa", Type = ServiceOrderType.Preventivo,
            StartDate = selectedDay, ExpectedEndDate = selectedDay.AddDays(1), Description = "Prueba de persistencia"
        };
        var createdResult = await create.OnPostAsync() as RedirectToPageResult;
        var createdOrder = await db.ServiceOrders.AsNoTracking().SingleAsync(x => x.Title == "Alta administrativa");
        var createdSchedule = await RequestSnapshot(createdOrder.Id);
        check(createdResult?.RouteValues?["id"]?.Equals(createdOrder.Id) == true
            && createdOrder.Status == ServiceOrderStatus.Created && createdSchedule?.StartDate?.Date == selectedDay
            && createdSchedule.ScheduledDateConfirmed,
            "Alta administrativa guarda orden y programación solicitada antes del intento SMTP");
        check(PendingNotice(create) && email.Attempts == 2 && !await db.ServiceOrderAuditLogs.AnyAsync(x => x.EventType == ServiceOrderAuditEvent.EmailSent),
            "Fallo SMTP en alta administrativa informa pendientes sin registrar correos enviados ni perder la orden");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>();
        using var provider = services.BuildServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var feasibility = new ServiceFeasibility
        {
            ClientId = client.Id, Title = "Factibilidad aceptada", SiteAddress = "Dirección de prueba",
            SiteContactName = "Contacto", SiteContactPhone = "5500000000", Status = ServiceFeasibilityStatus.Accepted
        };
        db.ServiceFeasibilities.Add(feasibility);
        await db.SaveChangesAsync();
        var convert = Page(new FeasibilityPage(db, userManager, null!, notices));
        var conversionResult = await convert.OnPostConvertAsync(feasibility.Id) as RedirectToPageResult;
        await db.Entry(feasibility).ReloadAsync();
        var convertedId = feasibility.ConvertedServiceOrderId!.Value;
        var convertedSchedule = await RequestSnapshot(convertedId);
        check(conversionResult?.RouteValues?["id"]?.Equals(convertedId) == true
            && feasibility.Status == ServiceFeasibilityStatus.ConvertedToOrder
            && await db.ServiceOrders.AnyAsync(x => x.Id == convertedId) && convertedSchedule?.ScheduledDateConfirmed == false,
            "Conversión de factibilidad guarda orden, vínculo y solicitud de avisos aunque SMTP falle");
        check(PendingNotice(convert) && email.Attempts == 4,
            "Conversión de factibilidad muestra aviso de entrega pendiente para Ingeniería y cliente");
        var countBeforeRepeat = await db.ServiceOrders.CountAsync();
        var repeatResult = await convert.OnPostConvertAsync(feasibility.Id) as RedirectToPageResult;
        check(repeatResult?.RouteValues?["id"]?.Equals(convertedId) == true
            && await db.ServiceOrders.CountAsync() == countBeforeRepeat && email.Attempts == 4
            && await db.ServiceOrderAuditLogs.CountAsync(x => x.OrderId == convertedId
                && x.EventType == ServiceOrderAuditEvent.CreationNotificationRequested) == 1,
            "Repetir conversión de factibilidad no crea otra orden ni duplica solicitudes o intentos de correo");

        var ticket = new Ticket
        {
            ClientId = client.Id, TicketNumber = "TK-TEST-ORDEN", Title = "Ticket de prueba",
            SlaResolutionDueAt = new DateTime(2026, 10, 29, 0, 0, 0, DateTimeKind.Utc)
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        var tickets = Page(new TicketsPage(db, null!, notices));
        tickets.CreateOrder = new TicketsPage.CreateOrderInput
            { TicketId = ticket.Id, Title = "Orden desde ticket", EstimatedEndDate = selectedDay.AddDays(2) };
        var ticketResult = await tickets.OnPostCreateOrderAsync() as RedirectToPageResult;
        var ticketOrder = await db.ServiceOrders.AsNoTracking().SingleAsync(x => x.Title == "Orden desde ticket");
        var ticketSchedule = await RequestSnapshot(ticketOrder.Id);
        check(ticketResult?.RouteValues?["id"]?.Equals(ticketOrder.Id) == true && ticketSchedule?.ScheduledDateConfirmed == false
            && ticketSchedule.ExpectedEndDate?.Date == selectedDay.AddDays(2) && ticketOrder.Status == ServiceOrderStatus.Created,
            "Orden desde ticket persiste solicitud de correo sin presentar la fecha límite como visita confirmada");
        check(PendingNotice(tickets) && email.Attempts == 6,
            "Fallo SMTP al generar orden desde ticket conserva la orden y avisa del envío pendiente");

        AdminDetails Details(string? role) => Page(new AdminDetails(db, null!, null!, email, config, notices), role);
        foreach (var role in new string?[] { null, AppRoles.Employee })
            check(await Details(role).OnPostRetryCreationNotificationsAsync(createdOrder.Id) is ForbidResult && email.Attempts == 6,
                "Reintentar avisos de creación exige administrador y no envía como empleado o anónimo");
        check(await Details(AppRoles.Admin).OnPostRetryCreationNotificationsAsync(Guid.NewGuid()) is NotFoundResult && email.Attempts == 6,
            "Reintento de orden inexistente no envía correos");

        var legacy = new ServiceOrder { ClientId = client.Id, Title = "Orden histórica sin solicitud de avisos", PublicToken = Guid.NewGuid().ToString("N") };
        db.ServiceOrders.Add(legacy);
        await db.SaveChangesAsync();
        var legacyDetails = Details(AppRoles.Admin);
        check(await legacyDetails.OnPostRetryCreationNotificationsAsync(legacy.Id) is RedirectToPageResult && email.Attempts == 6
            && !await db.ServiceOrderAuditLogs.AnyAsync(x => x.OrderId == legacy.Id),
            "Reintentar una orden histórica no crea solicitudes ni envía notificaciones retroactivas");

        email.Fail = false;
        var retryDetails = Details(AppRoles.Admin);
        check(await retryDetails.OnPostRetryCreationNotificationsAsync(createdOrder.Id) is RedirectToPageResult && email.Attempts == 8
            && await db.ServiceOrderAuditLogs.CountAsync(x => x.OrderId == createdOrder.Id && x.EventType == ServiceOrderAuditEvent.EmailSent) == 2,
            "Administrador puede completar los dos avisos pendientes después de corregir SMTP");

        var secondTicket = new Ticket
        {
            ClientId = client.Id, TicketNumber = "TK-TEST-ORDEN-2", Title = "Segundo ticket",
            SlaResolutionDueAt = new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc)
        };
        db.Tickets.Add(secondTicket);
        await db.SaveChangesAsync();
        tickets.CreateOrder = new TicketsPage.CreateOrderInput
            { TicketId = secondTicket.Id, Title = "Segunda orden desde ticket", EstimatedEndDate = selectedDay.AddDays(3) };
        var secondTicketResult = await tickets.OnPostCreateOrderAsync() as RedirectToPageResult;
        var secondTicketOrder = db.ServiceOrders.Local.Single(x => x.Title == "Segunda orden desde ticket");
        var firstTrackedTicketOrder = db.ServiceOrders.Local.Single(x => x.Id == ticketOrder.Id);
        check(secondTicketResult?.RouteValues?["id"]?.Equals(secondTicketOrder.Id) == true
            && !string.IsNullOrWhiteSpace(secondTicketOrder.PublicToken) && !string.IsNullOrWhiteSpace(firstTrackedTicketOrder.PublicToken)
            && secondTicketOrder.PublicToken != firstTrackedTicketOrder.PublicToken
            && secondTicketOrder.EstimatedEndDate?.Kind == DateTimeKind.Utc
            && firstTrackedTicketOrder.EstimatedEndDate?.Kind == DateTimeKind.Utc
            && secondTicketOrder.EstimatedEndDate?.Date == selectedDay.AddDays(3),
            "Altas sucesivas desde tickets generan tokens públicos únicos y guardan fechas UTC sin cambiar el día seleccionado");
    }

    private sealed class FailingEmail : IEmailSender
    {
        public bool Fail { get; set; } = true;
        public int Attempts { get; private set; }
        public Task SendAsync(string toEmail, string subject, string htmlBody, byte[]? attachmentBytes = null,
            string? attachmentName = null, string? attachmentContentType = null, IReadOnlyCollection<string>? ccEmails = null)
        {
            Attempts++;
            if (Fail) throw new InvalidOperationException("Fallo SMTP simulado para comprobar persistencia.");
            return Task.CompletedTask;
        }
    }
}
