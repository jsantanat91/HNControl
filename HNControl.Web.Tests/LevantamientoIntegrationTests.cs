using System.Text;
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
            BusinessLine="Manufactura",FiscalAddress="DOMICILIO FISCAL SECRETO",FiscalZipCode="06000",LegalRepresentative="Rep Legal",
            LegalPosition="Director General",LegalEmail="legal@lev.test",IsActive=true };
        client.Contacts.Add(new ClientContact { Name="Luis Gómez",Email="luis@lev.test",Phone="5552223333",Role="Sistemas" });
        var inactive = new Client { Name="Inactivo",ClientCode="CLI-200",Rfc="INA010101AA1",Email="x@inactivo.test",IsActive=false };
        var lead = new Client { Name="Prospecto",ClientCode="CLI-300",Rfc="PRO010101AA1",Email="x@lead.test",IsActive=true,IsTemporaryLead=true };
        db.Clients.AddRange(client,inactive,lead);
        await db.SaveChangesAsync();

        var surveys = new LevantamientoSurveyService(db);
        var controller = new LevantamientoController(db, surveys);
        JsonElement Json(IActionResult result) => JsonSerializer.SerializeToElement(((ObjectResult)result).Value,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Task<IActionResult> Lookup(string rfc,string email) => controller.Lookup(new ClientLookupInput { Rfc=rfc,Email=email });

        // ---------- Consulta de cliente ----------
        var soloRfc = Json(await Lookup("abc 010101-xy1",""));
        var empresa = soloRfc.GetProperty("client");
        check(soloRfc.GetProperty("found").GetBoolean() && !soloRfc.GetProperty("contactVerified").GetBoolean()
            && empresa.GetProperty("clientCode").GetString()=="CLI-100" && empresa.GetProperty("name").GetString()=="Empresa Lev"
            && empresa.GetProperty("contactName").GetString()=="Rep Legal" && empresa.GetProperty("contactRole").GetString()=="Director General"
            && empresa.GetProperty("phone").GetString()=="5550001111" && empresa.GetProperty("email").GetString()=="admin@lev.test"
            && empresa.GetProperty("city").GetString()=="Cuauhtémoc, CDMX" && empresa.GetProperty("address").GetString()=="Av. Reforma 1"
            && empresa.GetProperty("businessLine").GetString()=="Manufactura",
            "Levantamiento: solo RFC devuelve representante legal, teléfono y correo principal");
        var text = soloRfc.ToString();
        check(!text.Contains("SECRETO") && !text.Contains("06000") && !text.Contains("legal@lev.test") && !text.Contains("factura@lev.test")
            && !text.Contains("compras@lev.test") && !empresa.TryGetProperty("id",out _),
            "Levantamiento: no exporta datos fiscales, correos de facturación/legales, correos secundarios ni ids internos");
        var byContact = Json(await Lookup("cli-100","luis@lev.test"));
        var contacto = byContact.GetProperty("client");
        check(byContact.GetProperty("contactVerified").GetBoolean() && contacto.GetProperty("contactName").GetString()=="Luis Gómez"
            && contacto.GetProperty("contactRole").GetString()=="Sistemas" && contacto.GetProperty("phone").GetString()=="5552223333"
            && contacto.GetProperty("email").GetString()=="luis@lev.test",
            "Levantamiento: número de cliente + correo de un contacto devuelve ese contacto");
        var compras = Json(await Lookup("ABC010101XY1","COMPRAS@lev.test"));
        check(compras.GetProperty("contactVerified").GetBoolean() && compras.GetProperty("client").GetProperty("email").GetString()=="compras@lev.test"
            && compras.GetProperty("client").GetProperty("contactName").GetString()=="Rep Legal",
            "Levantamiento: correo propio del cliente lo verifica y conserva ese correo");
        check(await Lookup("ABC010101XY1","factura@lev.test") is OkObjectResult,
            "Levantamiento: acepta correo de facturación registrado");
        var otroCorreo = Json(await Lookup("ABC010101XY1","otro@lev.test"));
        check(!otroCorreo.GetProperty("contactVerified").GetBoolean() && otroCorreo.GetProperty("client").GetProperty("email").GetString()=="admin@lev.test",
            "Levantamiento: RFC con correo no registrado responde como búsqueda solo por RFC");
        check(await Lookup("CLI-100","") is BadRequestObjectResult && await Lookup("CLI-100","otro@lev.test") is NotFoundObjectResult,
            "Levantamiento: número de cliente (consecutivo) exige correo registrado");
        check(await Lookup("ZZZ010101XY1","") is NotFoundObjectResult && await Lookup("ZZZ010101XY1","admin@lev.test") is NotFoundObjectResult
            && await Lookup("ABC010101XY1","luis.lev.test") is BadRequestObjectResult && await Lookup("ABC0101","") is BadRequestObjectResult,
            "Levantamiento: RFC inexistente, mal formado o correo inválido no devuelven datos");
        check(await Lookup("INA010101AA1","x@inactivo.test") is NotFoundObjectResult && await Lookup("PRO010101AA1","x@lead.test") is NotFoundObjectResult
            && await Lookup("INA010101AA1","") is NotFoundObjectResult && await Lookup("PRO010101AA1","") is NotFoundObjectResult,
            "Levantamiento: excluye clientes inactivos y prospectos temporales");
        check(await Lookup("ab","admin@lev.test") is BadRequestObjectResult && await Lookup(new string('A',25),"admin@lev.test") is BadRequestObjectResult,
            "Levantamiento: valida longitud del RFC / número de cliente");

        // ---------- Recepción de levantamientos ----------
        var pdf = Convert.ToBase64String(Encoding.ASCII.GetBytes("%PDF-1.4 prueba"));
        var sections = JsonSerializer.SerializeToElement(new object[] {
            new { titulo="Internet dedicado", items=new object[] { new { h="Sitio" }, new { k="Ancho de banda", v="100 Mbps" },
                new { k="Ubicación", v="https://www.google.com/maps?q=19.4,-99.1" } } } });
        LevantamientoSurveyInput Input(string externalId, string company, string? rfc=null, string? code=null, string? sitesName=null, byte[]? sites=null) => new()
        {
            ExternalId=externalId, Company=company, Rfc=rfc, ClientCode=code, ContactName="Marta Ruiz", ContactRole="Compras",
            Email="Marta@Nueva.test", Phone="5559998888", City="Monterrey, Nuevo León", Address="Calle 1", BusinessLine="Logística",
            Services=["Internet dedicado"], Urgency="Este mes", Sections=sections, PdfFileName="lev.pdf", PdfBase64=pdf,
            SitesFileName=sitesName, SitesBase64=sites==null ? null : Convert.ToBase64String(sites)
        };

        var created = await controller.Survey(Input("lev-1","Empresa Lev",rfc:"abc-010101-xy1"), default) as ObjectResult;
        var createdBody = Json(created!);
        var repeated = await controller.Survey(Input("lev-1","Otra razón"), default) as ObjectResult;
        check(created.StatusCode==201 && createdBody.GetProperty("clientLinked").GetBoolean() && repeated is OkObjectResult
            && Json(repeated).GetProperty("reused").GetBoolean() && await db.LevantamientoSurveys.CountAsync()==1,
            "Factibilidades: recibe el levantamiento, lo liga por RFC y es idempotente por externalId");
        var first = await db.LevantamientoSurveys.AsNoTracking().SingleAsync();
        check(first.ClientId==client.Id && first.Email=="marta@nueva.test" && first.Rfc=="ABC010101XY1" && first.PdfBytes!.Length>5
            && LevantamientoSurveyService.ParseSections(first.SectionsJson) is [{ Title: "Internet dedicado", Items: [{ Heading: "Sitio" }, _, { IsLink: true }] }],
            "Factibilidades: guarda datos normalizados, PDF y secciones legibles");
        var badPdf = Input("lev-3","X"); badPdf.PdfBase64 = Convert.ToBase64String(Encoding.ASCII.GetBytes("no es pdf"));
        check(await controller.Survey(Input("lev 2","X"), default) is BadRequestObjectResult
            && await controller.Survey(badPdf, default) is BadRequestObjectResult
            && await controller.Survey(Input("lev-4","X",sitesName:"sitios.exe",sites:[1,2,3]), default) is BadRequestObjectResult
            && !await db.LevantamientoSurveys.AnyAsync(x => x.ExternalId != "lev-1"),
            "Factibilidades: rechaza externalId, PDF o archivo de sitios inválidos sin guardar nada");
        check(Json(await controller.Survey(Input("lev-5","Empresa Lev",code:"cli 100"), default)).GetProperty("clientLinked").GetBoolean()
            && Json(await controller.Survey(Input("lev-6","Prospecto",rfc:"PRO010101AA1"), default)).GetProperty("clientLinked").GetBoolean()
            && !Json(await controller.Survey(Input("lev-6b","Inactivo",rfc:"INA010101AA1"), default)).GetProperty("clientLinked").GetBoolean(),
            "Factibilidades: liga por número de cliente o RFC (incluye prospecto activo, nunca un cliente inactivo)");

        // ---------- Archivo de varios sitios ----------
        var csv = Encoding.UTF8.GetBytes("Dirección;MB;Contacto\nAv. Uno 10, CDMX;50;Pedro\nCalle Dos 5, GDL;100;\n");
        var csvSites = LevantamientoSurveyService.ReadSites("sitios.csv", csv);
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.AddWorksheet("Sitios");
        ws.Cell(1,1).Value="Sitio"; ws.Cell(1,2).Value="Dirección completa"; ws.Cell(1,3).Value="Capacidad (MB)";
        ws.Cell(2,1).Value="Matriz"; ws.Cell(2,2).Value="Av. Tres 1, MTY"; ws.Cell(2,3).Value=200;
        using var ms = new MemoryStream(); wb.SaveAs(ms);
        var xlsxSites = LevantamientoSurveyService.ReadSites("sitios.xlsx", ms.ToArray());
        check(csvSites.Count==2 && csvSites[0]==new LevantamientoSite("Av. Uno 10, CDMX","50","Pedro") && csvSites[1].Capacity=="100"
            && xlsxSites is [{ Address: "Av. Tres 1, MTY", Capacity: "200", Notes: "Matriz" }]
            && LevantamientoSurveyService.ReadSites("x.xlsx",[1,2,3]).Count==0,
            "Factibilidades: lee sitios (dirección y MB) de CSV y Excel, y tolera archivos dañados");

        // ---------- Cliente y orden desde la factibilidad ----------
        var nuevo = Json(await controller.Survey(Input("lev-7","Nueva Empresa SA",rfc:"NUE010101AB2",sitesName:"sitios.xlsx",sites:ms.ToArray()), default));
        var nuevoId = nuevo.GetProperty("id").GetGuid();
        var hermano = Json(await controller.Survey(Input("lev-8","Nueva Empresa SA",rfc:"NUE010101AB2"), default)).GetProperty("id").GetGuid();
        var (sinCliente, sinClienteError) = await surveys.CreateOrderAsync(nuevoId, "u1", "https://x/Ventas/Factibilidad/1");
        check(!nuevo.GetProperty("clientLinked").GetBoolean() && sinCliente==null && sinClienteError!.Contains("cliente"),
            "Factibilidades: no permite crear orden sin cliente");
        var (alta, _) = await surveys.CreateClientAsync(nuevoId, "u1");
        var altaDb = await db.Clients.AsNoTracking().Include(x => x.Contacts).SingleAsync(x => x.Id == alta!.Id);
        check(altaDb.ClientCode=="HN-0001" && altaDb.Name=="Nueva Empresa SA" && altaDb.Rfc=="NUE010101AB2" && altaDb.Kind==ClientKind.Moral
            && altaDb.Email=="marta@nueva.test" && altaDb.Municipality=="Monterrey" && altaDb.State=="Nuevo León" && altaDb.OwnerUserId=="u1"
            && altaDb.Contacts is [{ Name: "Marta Ruiz", IsPrimary: true, Role: "Compras" }]
            && await db.LevantamientoSurveys.CountAsync(x => (x.Id==nuevoId || x.Id==hermano) && x.ClientId==alta.Id)==2,
            "Factibilidades: crear cliente usa los datos del levantamiento y liga las otras factibilidades del mismo RFC");
        var (otraVez, otraVezError) = await surveys.CreateClientAsync(nuevoId, "u1");
        check(otraVez==null && otraVezError!=null && await db.Clients.CountAsync(x => x.Rfc=="NUE010101AB2")==1,
            "Factibilidades: no duplica el cliente si ya está ligado");
        var (orden, _) = await surveys.CreateOrderAsync(nuevoId, "u1", "https://control.test/Ventas/Factibilidad/" + nuevoId);
        await db.SaveChangesAsync();
        var guardada = await db.LevantamientoSurveys.AsNoTracking().SingleAsync(x => x.Id == nuevoId);
        check(orden!=null && orden.ClientId==alta.Id && orden.Type==ServiceOrderType.LevantamientoTecnico && guardada.ServiceOrderId==orden.Id
            && guardada.Status==LevantamientoSurveyStatus.InReview && orden.Description!.Contains("Av. Tres 1, MTY · 200 MB")
            && orden.Description.Contains("Ancho de banda: 100 Mbps") && !orden.Description.Contains("google.com/maps"),
            "Factibilidades: con cliente crea la orden de levantamiento con resumen y sitios");
        check((await surveys.CreateOrderAsync(nuevoId, "u1", "x")).Order==null
            && await surveys.LinkClientAsync(nuevoId, client.Id, "u1")!=null
            && await surveys.LinkClientAsync(hermano, inactive.Id, "u1")!=null
            && await surveys.LinkClientAsync(hermano, client.Id, "u1")==null,
            "Factibilidades: no repite orden ni cambia cliente con orden; solo liga a clientes activos");
        var mismoRfc = Json(await controller.Survey(Input("lev-9","Empresa Lev SA de CV",rfc:"ABC010101XY1"), default)).GetProperty("id").GetGuid();
        await db.LevantamientoSurveys.Where(x => x.Id == mismoRfc).ExecuteUpdateAsync(x => x.SetProperty(s => s.ClientId, (Guid?)null));
        db.ChangeTracker.Clear();
        var (existente, _) = await surveys.CreateClientAsync(mismoRfc, "u1");
        check(existente?.Id==client.Id && await db.Clients.CountAsync(x => x.Rfc=="ABC010101XY1")==1,
            "Factibilidades: crear cliente con un RFC ya registrado liga al existente en lugar de duplicar");
    }
}
