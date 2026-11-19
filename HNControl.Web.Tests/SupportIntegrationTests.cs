using System.Text.Json;
using HNControl.Web.Controllers.Integrations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

internal static class SupportIntegrationTests
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        var key = new string('a',64);
        check(SupportIntegrationAuth.Matches(key,key) && !SupportIntegrationAuth.Matches(key,new string('b',64))
            && !SupportIntegrationAuth.Matches(null,key) && !SupportIntegrationAuth.Matches("short","short")
            && !SupportIntegrationAuth.Matches(key,null), "Support: credencial dedicada, comparación exacta y cierre sin configuración");
        async Task<(bool invoked,int? status)> Authenticate(string? configured,string? supplied) {
            var cfg=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["SupportIntegration:ApiKey"]=configured }).Build();
            var http=new DefaultHttpContext();if(supplied!=null)http.Request.Headers["X-HN-Support-Key"]=supplied;
            var action=new ActionContext(http,new RouteData(),new ActionDescriptor());
            var context=new ActionExecutingContext(action,new List<IFilterMetadata>(),new Dictionary<string,object?>(),new object());
            var invoked=false;
            await new SupportIntegrationAuth(cfg).OnActionExecutionAsync(context,()=>{invoked=true;return Task.FromResult(new ActionExecutedContext(action,new List<IFilterMetadata>(),new object()));});
            return(invoked,(context.Result as ObjectResult)?.StatusCode);
        }
        check(await Authenticate(null,key)==(false,503) && await Authenticate(key,null)==(false,401)
            && await Authenticate(key,"incorrecta")== (false,401) && (await Authenticate(key,key)).invoked,
            "Support: filtro impide ejecutar acciones sin clave válida y devuelve 401/503");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["PublicLinks:BaseUrl"]="https://control.example.test" }).Build();
        var mail = new TestEmail();
        var notices = new ServiceOrderCreationNotifications(db,mail,config,NullLogger<ServiceOrderCreationNotifications>.Instance);
        var controller = new SupportController(db,config,notices);
        var client = new Client { Name="Empresa A",ClientCode="SUP-A",Email="a@example.test",IsActive=true };
        var other = new Client { Name="Empresa B",ClientCode="SUP-B",Email="b@example.test",IsActive=true };
        var inactive = new Client { Name="Cliente inactivo",ClientCode="SUP-C",IsActive=false };
        var account = new ServiceInventoryAccount { Label="Cuenta Telmex",CarrierKey="telmex",AccountNumber="ACCOUNT-001",PasswordProtected="SECRET-NOT-EXPORTED",IsActive=true };
        var service = new ServiceInventoryItem { Name="Enlace principal",CarrierKey="telmex",AccountId=account.Id,ClientId=client.Id,AccountNumber="LINE-002",SerialNumber="SERIAL-003",IsActive=true };
        db.Clients.AddRange(client,other,inactive); db.ServiceInventoryAccounts.Add(account); db.ServiceInventoryItems.Add(service);
        db.ServiceInventoryItems.Add(new ServiceInventoryItem { Name="Servicio inactivo",IsActive=false });
        await db.SaveChangesAsync();
        JsonElement Json(IActionResult result) => JsonSerializer.SerializeToElement(((ObjectResult)result).Value,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var catalog = Json(await controller.List());
        var item = catalog.GetProperty("items")[0];
        check(catalog.GetProperty("total").GetInt32()==1 && item.GetProperty("providerAccountNumber").GetString()=="ACCOUNT-001"
            && item.GetProperty("serviceNumber").GetString()=="LINE-002", "Support: inventario separa cuenta del proveedor y número de servicio");
        check(!catalog.ToString().Contains("SECRET") && !item.TryGetProperty("passwordProtected",out _) && !item.TryGetProperty("monthlyCost",out _),
            "Support: contrato de API no exporta contraseñas, costos ni entidades completas");
        check(Json(await controller.List("SERIAL-003")).GetProperty("total").GetInt32()==1
            && Json(await controller.List("inexistente")).GetProperty("total").GetInt32()==0
            && Json(await controller.List(activeOnly:false)).GetProperty("total").GetInt32()==2,
            "Support: búsqueda y filtro de activos se ejecutan en base de datos");
        check(Json(await controller.List(activeOnly:false,page:2,pageSize:1)).GetProperty("items").GetArrayLength()==1,
            "Support: paginación de inventario conserva el total y limita filas");
        check(Json(await controller.Service(service.Id)).GetProperty("id").GetGuid()==service.Id
            && await controller.Service(Guid.NewGuid()) is NotFoundResult, "Support: consulta individual y servicio ausente");
        check(Json(await controller.Options()).GetProperty("clients").GetArrayLength()==2, "Support: opciones solo incluyen clientes activos");
        CreateOrderInput Input(long ticket,Guid clientId) => new() { TicketId=ticket,ClientId=clientId,ServiceId=service.Id,
            Title="Atender enlace de cliente",Description="Revisar conexión y validar disponibilidad.",RequestedBy="Agente de prueba",
            StartDate=new DateOnly(2026,11,19),EndDate=new DateOnly(2026,11,20),Type=ServiceOrderType.Correctivo };
        check(await controller.Create(Input(1,other.Id)) is BadRequestObjectResult && !await db.ServiceOrders.AnyAsync(),
            "Support: rechaza servicio de otro cliente antes de guardar o notificar");
        var invalidTech=Input(1,client.Id);invalidTech.AssignedUserId="missing";
        check(await controller.Create(invalidTech) is BadRequestObjectResult, "Support: no asigna técnicos inexistentes");
        var invalidDate=Input(1,client.Id);invalidDate.EndDate=invalidDate.StartDate.AddDays(-1);
        check(await controller.Create(invalidDate) is BadRequestObjectResult, "Support: valida fechas de la orden");
        var created=await controller.Create(Input(1,client.Id));
        var order=await db.ServiceOrders.SingleAsync();var attempts=mail.Attempts;
        check(created is ObjectResult {StatusCode:201} && order.Status==ServiceOrderStatus.Created
            && order.CurrentArea==ServiceOrderWorkflowArea.Levantamiento && order.Description.Contains("HN-000001")
            && await db.SupportOrderLinks.CountAsync()==1, "Support: guarda orden y origen juntos y conserva flujo técnico");
        check(attempts>0 && await db.ServiceOrderAuditLogs.AnyAsync(x=>x.OrderId==order.Id && x.EventType==ServiceOrderAuditEvent.CreationNotificationRequested),
            "Support: utiliza avisos persistentes de Control (correo simulado, sin envíos reales)");
        var repeat=Json(await controller.Create(Input(1,client.Id)));
        check(repeat.GetProperty("reused").GetBoolean() && await db.ServiceOrders.CountAsync()==1 && mail.Attempts==attempts,
            "Support: reintento devuelve misma orden sin duplicar avisos");
        check(Json(await controller.Order(1)).GetProperty("order").GetProperty("id").GetGuid()==order.Id
            && Json(await controller.Order(999)).GetProperty("order").ValueKind==JsonValueKind.Null,
            "Support: consulta de estado recupera orden tras respuesta perdida");
        var manual=Input(2,other.Id);manual.ServiceId=null;
        check(await controller.Create(manual) is ObjectResult {StatusCode:201}, "Support: permite orden manual sin monitoreo asociado");
        check(db.Model.FindEntityType(typeof(SupportOrderLink))!.GetIndexes().Any(x=>x.IsUnique && x.Properties.Select(p=>p.Name).SequenceEqual(new[]{"SourceId","TicketId"})),
            "Support: unicidad de origen/ticket protegida por base de datos");
        db.SupportOrderLinks.Add(new SupportOrderLink { TicketId=1,OrderId=order.Id });
        var rejected=false;try{await db.SaveChangesAsync();}catch(DbUpdateException){rejected=true;db.ChangeTracker.Clear();}
        check(rejected && await db.SupportOrderLinks.CountAsync()==2, "Support: la base rechaza físicamente un segundo vínculo con el mismo ticket");
    }
    private sealed class TestEmail:IEmailSender {
        public int Attempts {get;private set;}
        public Task SendAsync(string toEmail,string subject,string htmlBody,byte[]? attachmentBytes=null,string? attachmentName=null,string? attachmentContentType=null,IReadOnlyCollection<string>? ccEmails=null) {
            Attempts++;throw new InvalidOperationException("SMTP simulado; no se envía correo real.");
        }
    }
}
