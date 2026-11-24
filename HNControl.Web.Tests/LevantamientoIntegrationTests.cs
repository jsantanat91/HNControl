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

internal static class LevantamientoIntegrationTests
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        var key = new string('k',64);
        async Task<(bool invoked,int? status)> Authenticate(string? configured,string? supplied,string header="X-HN-Levantamiento-Key") {
            var cfg=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["LevantamientoIntegration:ApiKey"]=configured,["SupportIntegration:ApiKey"]=key }).Build();
            var http=new DefaultHttpContext();if(supplied!=null)http.Request.Headers[header]=supplied;
            var action=new ActionContext(http,new RouteData(),new ActionDescriptor());
            var context=new ActionExecutingContext(action,new List<IFilterMetadata>(),new Dictionary<string,object?>(),new object());
            var invoked=false;
            await new LevantamientoIntegrationAuth(cfg).OnActionExecutionAsync(context,()=>{invoked=true;return Task.FromResult(new ActionExecutedContext(action,new List<IFilterMetadata>(),new object()));});
            return(invoked,(context.Result as ObjectResult)?.StatusCode);
        }
        check(await Authenticate(null,key)==(false,503) && await Authenticate("corta",key)==(false,503)
            && await Authenticate(key,null)==(false,401) && await Authenticate(key,"incorrecta")==(false,401)
            && (await Authenticate(key,key)).invoked,
            "Levantamiento: filtro exige su propia clave y devuelve 401/503");
        check(await Authenticate(new string('z',64),key,"X-HN-Support-Key")==(false,401),
            "Levantamiento: la clave de Support no abre la consulta de clientes");

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var client = new Client { Name="Empresa Lev",ClientCode="CLI-100",Rfc="ABC010101XY1",Email="admin@lev.test; compras@lev.test",
            BillingEmail="factura@lev.test",Phone="5550001111",ContactName="Ana Pérez",Address="Av. Reforma 1",Municipality="Cuauhtémoc",State="CDMX",
            BusinessLine="Manufactura",FiscalAddress="DOMICILIO FISCAL SECRETO",FiscalZipCode="06000",LegalRepresentative="Rep Legal",LegalEmail="legal@lev.test",IsActive=true };
        client.Contacts.Add(new ClientContact { Name="Luis Gómez",Email="luis@lev.test",Phone="5552223333",Role="Sistemas" });
        var inactive = new Client { Name="Inactivo",ClientCode="CLI-200",Rfc="INA010101AA1",Email="x@inactivo.test",IsActive=false };
        var lead = new Client { Name="Prospecto",ClientCode="CLI-300",Rfc="PRO010101AA1",Email="x@lead.test",IsActive=true,IsTemporaryLead=true };
        db.Clients.AddRange(client,inactive,lead);
        await db.SaveChangesAsync();

        var controller = new LevantamientoController(db);
        JsonElement Json(IActionResult result) => JsonSerializer.SerializeToElement(((ObjectResult)result).Value,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Task<IActionResult> Lookup(string rfc,string email) => controller.Lookup(new ClientLookupInput { Rfc=rfc,Email=email });

        var found = Json(await Lookup("abc-010101 xy1","COMPRAS@lev.test"));
        var data = found.GetProperty("client");
        check(found.GetProperty("found").GetBoolean() && data.GetProperty("clientCode").GetString()=="CLI-100"
            && data.GetProperty("contactName").GetString()=="Ana Pérez" && data.GetProperty("city").GetString()=="Cuauhtémoc, CDMX"
            && data.GetProperty("businessLine").GetString()=="Manufactura",
            "Levantamiento: RFC normalizado + correo del cliente devuelve datos básicos");
        var byContact = Json(await Lookup("cli-100","luis@lev.test")).GetProperty("client");
        check(byContact.GetProperty("contactName").GetString()=="Luis Gómez" && byContact.GetProperty("contactRole").GetString()=="Sistemas"
            && byContact.GetProperty("phone").GetString()=="5552223333",
            "Levantamiento: número de cliente + correo de un contacto devuelve ese contacto");
        check(await Lookup("ABC010101XY1","factura@lev.test") is OkObjectResult,
            "Levantamiento: acepta correo de facturación registrado");
        var text = found.ToString();
        check(!text.Contains("SECRETO") && !text.Contains("06000") && !text.Contains("Rep Legal") && !text.Contains("legal@lev.test")
            && !text.Contains("admin@lev.test") && !data.TryGetProperty("id",out _),
            "Levantamiento: no exporta datos fiscales, representante legal, correos ni ids internos");
        check(await Lookup("ABC010101XY1","otro@lev.test") is NotFoundObjectResult && await Lookup("ZZZ010101XY1","admin@lev.test") is NotFoundObjectResult
            && await Lookup("ABC010101XY1","luis.lev.test") is BadRequestObjectResult,
            "Levantamiento: RFC o correo que no coinciden no revelan si el cliente existe");
        check(await Lookup("INA010101AA1","x@inactivo.test") is NotFoundObjectResult && await Lookup("PRO010101AA1","x@lead.test") is NotFoundObjectResult,
            "Levantamiento: excluye clientes inactivos y prospectos temporales");
        check(await Lookup("ab","admin@lev.test") is BadRequestObjectResult && await Lookup(new string('A',25),"admin@lev.test") is BadRequestObjectResult,
            "Levantamiento: valida longitud del RFC / número de cliente");
    }
}
