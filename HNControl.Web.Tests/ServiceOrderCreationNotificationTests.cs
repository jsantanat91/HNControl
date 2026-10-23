using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

internal static class ServiceOrderCreationNotificationTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        await using (var fixture = await Fixture.CreateAsync("ingenieria@example.test; OPERACIONES@example.test; ingenieria@example.test"))
        {
            var order = fixture.NewOrder();
            var contract = new ClientServiceContract
            {
                Client = order.Client!, Label = "Internet dedicado <10 MB>", Branch = "Sucursal & Centro"
            };
            order.ClientServiceContract = contract;
            order.Description = "INTERNAL-ONLY: acceso técnico <script>alert('prueba')</script>";
            fixture.Db.Add(order);
            fixture.Notices.QueueCreated(order);
            await fixture.Db.SaveChangesAsync();
            check(await fixture.Db.ServiceOrderAuditLogs.CountAsync(x => x.OrderId == order.Id
                && x.EventType == ServiceOrderAuditEvent.CreationNotificationRequested) == 1,
                "OS: solicitud de aviso se persiste junto con la orden");

            // Operational handlers replace StartedAt after a technician claims an order.
            order.StartedAt = new DateTime(2026, 11, 5, 18, 0, 0, DateTimeKind.Utc);
            order.EstimatedEndDate = new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);
            order.Status = ServiceOrderStatus.InProgress;
            await fixture.Db.SaveChangesAsync();
            var result = await fixture.Notices.NotifyCreatedAsync(order.Id);
            check(!result.HasPending && fixture.Email.Messages.Count == 2,
                "OS: envía avisos independientes a Ingeniería y cliente");
            var engineering = fixture.Email.Messages.Single(x => x.To == "ingenieria@example.test");
            var client = fixture.Email.Messages.Single(x => x.To == "cliente@example.test");
            check(engineering.Cc.SequenceEqual(new[] { "OPERACIONES@example.test" }) && client.Cc.Count == 0,
                "OS: deduplica lista interna sin compartirla con el cliente");
            check(engineering.Body.Contains("INTERNAL-ONLY") && !client.Body.Contains("INTERNAL-ONLY")
                && engineering.Body.Contains("/Admin/ServiceOrders/Details/") && !client.Body.Contains("/Admin/"),
                "OS: el cliente no recibe requerimientos internos ni enlace administrativo");
            check(client.Body.Contains("Empresa &amp; Asociados &lt;Prueba&gt;")
                && client.Body.Contains("Internet dedicado &lt;10 MB&gt;")
                && client.Body.Contains("Sucursal &amp; Centro")
                && !engineering.Body.Contains("<script>") && engineering.Body.Contains("&lt;script&gt;"),
                "OS: datos dinámicos y requerimientos se codifican como HTML");
            check(client.Body.Contains("29/10/2026") && client.Body.Contains("31/10/2026")
                && !client.Body.Contains("05/11/2026") && !client.Body.Contains("08/11/2026"),
                "OS: reintentos conservan programación original aunque StartedAt cambie");
            check(client.Body.Contains("HN-0022") && System.Net.WebUtility.HtmlDecode(client.Body).Contains("Ingeniería y Operaciones")
                && client.Body.Contains("https://wa.me/525526014566")
                && client.Body.Contains("background:#102c4a")
                && client.Body.Contains(ServiceOrderNotificationTemplates.Reference(order)),
                "OS: aviso al cliente conserva identidad y formato de bienvenida");
            check(engineering.Subject.All(c => c != '\r' && c != '\n') && engineering.Attachment == null && client.Attachment == null,
                "OS: asuntos sin saltos de línea y aviso inicial sin PDF incompleto");
            var state = await fixture.Notices.GetStateAsync(order.Id);
            check(state.Exists && state.EngineeringConfigured && state.EngineeringSent && state.ClientSent && !state.HasPending,
                "OS: estado registra ambos canales entregados");
            await fixture.Notices.NotifyCreatedAsync(order.Id);
            check(fixture.Email.Messages.Count == 2 && await fixture.Db.ServiceOrderAuditLogs.CountAsync(x => x.OrderId == order.Id
                && x.EventType == ServiceOrderAuditEvent.EmailSent) == 2,
                "OS: reintento de orden entregada no duplica mensajes ni recibos");
            check(order.Status == ServiceOrderStatus.InProgress && order.StartedAt.Value.Day == 5,
                "OS: notificación no altera el flujo de trabajo ni las fechas operativas");
            Directory.CreateDirectory("artifacts/service-order-notifications");
            await File.WriteAllTextAsync("artifacts/service-order-notifications/cliente.html", client.Body);
            await File.WriteAllTextAsync("artifacts/service-order-notifications/ingenieria.html", engineering.Body);
        }

        foreach (var failingChannel in new[] { "ingenieria@example.test", "cliente@example.test" })
        {
            await using var fixture = await Fixture.CreateAsync("ingenieria@example.test");
            var order = await fixture.SaveOrderAsync();
            fixture.Email.FailingRecipients.Add(failingChannel);
            var result = await fixture.Notices.NotifyCreatedAsync(order.Id);
            var state = await fixture.Notices.GetStateAsync(order.Id);
            check(result.HasPending && fixture.Email.Messages.Count == 1
                && fixture.Email.Messages.All(x => x.To != failingChannel)
                && state.ClientSent == (failingChannel != "cliente@example.test")
                && state.EngineeringSent == (failingChannel != "ingenieria@example.test"),
                $"OS: fallo SMTP de {failingChannel} conserva envío del otro canal");
            check(await fixture.Db.ServiceOrders.AnyAsync(x => x.Id == order.Id)
                && await fixture.Db.ServiceOrderAuditLogs.CountAsync(x => x.OrderId == order.Id
                    && x.EventType == ServiceOrderAuditEvent.EmailSent) == 1,
                "OS: fallo SMTP conserva la orden y solo registra entregas exitosas");
            fixture.Email.FailingRecipients.Clear();
            result = await fixture.Notices.NotifyCreatedAsync(order.Id);
            check(!result.HasPending && fixture.Email.Messages.Count == 2
                && fixture.Email.Messages.Select(x => x.To).Distinct().Count() == 2,
                "OS: reintento entrega únicamente el canal pendiente");
        }

        await using (var fixture = await Fixture.CreateAsync("cliente@example.test"))
        {
            var order = await fixture.SaveOrderAsync();
            await fixture.Notices.NotifyCreatedAsync(order.Id);
            check(fixture.Email.Messages.Count == 2 && fixture.Email.Messages.All(x => x.To == "cliente@example.test")
                && fixture.Email.Messages.Select(x => x.Subject).Distinct().Count() == 2,
                "OS: una dirección compartida recibe ambos mensajes con contenidos separados");
        }

        await using (var fixture = await Fixture.CreateAsync(""))
        {
            var order = await fixture.SaveOrderAsync();
            var result = await fixture.Notices.NotifyCreatedAsync(order.Id);
            var state = await fixture.Notices.GetStateAsync(order.Id);
            check(!result.HasPending && fixture.Email.Messages.Count == 1 && fixture.Email.Messages[0].To == "cliente@example.test"
                && !state.EngineeringConfigured && !state.EngineeringSent && state.ClientSent,
                "OS: Ingeniería vacía desactiva internos sin bloquear aviso al cliente");
            fixture.Settings.EngineeringNotificationEmails = "nuevo@example.test";
            await fixture.Db.SaveChangesAsync();
            state = await fixture.Notices.GetStateAsync(order.Id);
            check(state.HasPending, "OS: destinatarios configurados después permiten entregar aviso interno pendiente");
            await fixture.Notices.NotifyCreatedAsync(order.Id);
            check(fixture.Email.Messages.Count == 2 && fixture.Email.Messages[1].To == "nuevo@example.test",
                "OS: reintento usa destinatarios actuales sin repetir aviso al cliente");
        }

        foreach (var clientEmail in new string?[] { "correo-invalido", null })
        {
            await using var fixture = await Fixture.CreateAsync("ingenieria@example.test");
            var order = fixture.NewOrder();
            order.Client!.Email = clientEmail;
            fixture.Db.Add(order);
            fixture.Notices.QueueCreated(order);
            await fixture.Db.SaveChangesAsync();
            var result = await fixture.Notices.NotifyCreatedAsync(order.Id);
            check(result.HasPending && fixture.Email.Messages.Count == 1 && fixture.Email.Messages[0].To == "ingenieria@example.test",
                "OS: correo de cliente ausente o inválido no impide notificar a Ingeniería");
            order.Client.Email = "corregido@example.test";
            await fixture.Db.SaveChangesAsync();
            await fixture.Notices.NotifyCreatedAsync(order.Id);
            check(fixture.Email.Messages.Count == 2 && fixture.Email.Messages[1].To == "corregido@example.test",
                "OS: corrección de correo permite reintento del cliente sin reenviar a Ingeniería");
        }

        await using (var fixture = await Fixture.CreateAsync(""))
        {
            var order = fixture.NewOrder();
            order.Client!.Email = null;
            order.Client.LegalEmail = "legal@example.test";
            order.Client.BillingEmail = "facturas@example.test";
            fixture.Db.Add(order);
            fixture.Notices.QueueCreated(order, scheduledDateConfirmed: false);
            await fixture.Db.SaveChangesAsync();
            await fixture.Notices.NotifyCreatedAsync(order.Id);
            check(fixture.Email.Messages.Count == 1 && fixture.Email.Messages[0].To == "legal@example.test",
                "OS: destinatario alterno sigue prioridad de bienvenida");
            check(fixture.Email.Messages[0].Body.Contains("Por confirmar")
                && !fixture.Email.Messages[0].Body.Contains("29/10/2026") && !fixture.Email.Messages[0].Body.Contains("31/10/2026"),
                "OS: factibilidad sin programación confirmada no promete fechas automáticas");
        }

        await using (var fixture = await Fixture.CreateAsync(""))
        {
            var order = fixture.NewOrder();
            order.Client!.Email = null;
            order.Client.BillingEmail = "facturas@example.test";
            order.StartedAt = null;
            fixture.Db.Add(order);
            fixture.Notices.QueueCreated(order);
            await fixture.Db.SaveChangesAsync();
            await fixture.Notices.NotifyCreatedAsync(order.Id);
            check(fixture.Email.Messages.Count == 1 && fixture.Email.Messages[0].To == "facturas@example.test"
                && fixture.Email.Messages[0].Body.Contains("La fecha y el horario de atención están por confirmar"),
                "OS: sin fecha de inicio no anuncia la fecha límite como visita");
        }

        await using (var fixture = await Fixture.CreateAsync("ingenieria@example.test"))
        {
            var legacy = fixture.NewOrder();
            fixture.Db.Add(legacy);
            await fixture.Db.SaveChangesAsync();
            var result = await fixture.Notices.NotifyCreatedAsync(legacy.Id);
            var state = await fixture.Notices.GetStateAsync(legacy.Id);
            var missing = await fixture.Notices.NotifyCreatedAsync(Guid.NewGuid());
            check(!result.HasPending && !missing.HasPending && !state.Exists && fixture.Email.Messages.Count == 0,
                "OS: no envía notificaciones retroactivas ni para órdenes inexistentes");
        }
    }

    private sealed record Message(string To, string Subject, string Body, IReadOnlyCollection<string> Cc, byte[]? Attachment);

    private sealed class RecordingEmail : IEmailSender
    {
        public List<Message> Messages { get; } = new();
        public HashSet<string> FailingRecipients { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Task SendAsync(string toEmail, string subject, string htmlBody, byte[]? attachmentBytes = null,
            string? attachmentName = null, string? attachmentContentType = null, IReadOnlyCollection<string>? ccEmails = null)
        {
            if (FailingRecipients.Contains(toEmail)) throw new InvalidOperationException("SMTP simulado: canal no disponible.");
            Messages.Add(new(toEmail, subject, htmlBody, ccEmails?.ToArray() ?? Array.Empty<string>(), attachmentBytes));
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public ApplicationDbContext Db { get; }
        public RecordingEmail Email { get; } = new();
        public SystemConfiguration Settings { get; }
        public ServiceOrderCreationNotifications Notices { get; }

        private Fixture(SqliteConnection connection, ApplicationDbContext db, SystemConfiguration settings)
        {
            this.connection = connection;
            Db = db;
            Settings = settings;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PublicLinks:BaseUrl"] = "https://control.example.test"
            }).Build();
            Notices = new(db, Email, configuration, NullLogger<ServiceOrderCreationNotifications>.Instance);
        }

        public static async Task<Fixture> CreateAsync(string engineering)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var settings = new SystemConfiguration { EngineeringNotificationEmails = engineering };
            db.SystemConfigurations.Add(settings);
            await db.SaveChangesAsync();
            return new(connection, db, settings);
        }

        public ServiceOrder NewOrder() => new()
        {
            Client = new Client { Name = "Empresa & Asociados <Prueba>", Email = "cliente@example.test", ClientCode = "HN-0022" },
            Title = "Instalación de enlace\r\nSucursal Centro", Type = ServiceOrderType.NuevaInstalacion,
            StartedAt = new DateTime(2026, 10, 29, 0, 0, 0, DateTimeKind.Utc),
            EstimatedEndDate = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        public async Task<ServiceOrder> SaveOrderAsync()
        {
            var order = NewOrder();
            Db.Add(order);
            Notices.QueueCreated(order);
            await Db.SaveChangesAsync();
            return order;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
