using System.Security.Claims;
using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using HNControl.Web.Pages.Public;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

internal static class OperationsFlowTests
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var tech = new EmployeeProfile { UserId="tech", FullName="Técnico", ServiceOrderCommissionEnabled=true, ServiceOrderCommissionAmount=150 };
        var assigned = new EmployeeProfile { UserId="assigned", ServiceOrderCommissionEnabled=true, ServiceOrderCommissionAmount=90 };
        var client = new Client { Name="Cliente privado", Email="privado@example.test", PublicQuoteToken="private-test-token" };
        db.AddRange(tech,assigned,client); await db.SaveChangesAsync();
        var now = new DateTime(2026,11,16,4,0,0,DateTimeKind.Utc); // still Nov 15 in Mexico
        var period = ServiceOrderCommissionService.Period(now);
        check(period.start.Day==1 && period.end.Day==15,"Comisión: quincena con fecha de México, no UTC");
        var order = new ServiceOrder { PublicToken=Guid.NewGuid().ToString("N"), ClientId=client.Id, ClaimedByUserId=tech.UserId, AssignedUserId=assigned.UserId, Title="Instalación", Status=ServiceOrderStatus.InReview };
        db.Add(order); await db.SaveChangesAsync();
        await ServiceOrderCommissionService.FinalizeAsync(db,order,now);
        var bonus = await db.EmployeeDeductions.SingleAsync();
        check(bonus.UserId==tech.UserId && bonus.Amount==150 && bonus.SourceServiceOrderId==order.Id,"Comisión: bono al técnico que tomó la orden");
        check(bonus.StartDate.Day==1 && bonus.EndDate!.Value.Day==15 && bonus.TermCount==1,"Comisión: limitada a la quincena activa");
        await ServiceOrderCommissionService.FinalizeAsync(db,order,now.AddHours(1));
        check(await db.EmployeeDeductions.CountAsync()==1,"Comisión: aprobar dos veces no duplica");
        tech.ServiceOrderCommissionAmount=999; await db.SaveChangesAsync();
        order.Status=ServiceOrderStatus.InReview; await db.SaveChangesAsync();
        await ServiceOrderCommissionService.FinalizeAsync(db,order,now.AddDays(1));
        check(await db.EmployeeDeductions.CountAsync()==1 && bonus.Amount==150,"Comisión: reabrir y cambiar tarifa conserva el bono original");
        var receipt = await new PayrollReceiptService(db,new ConfigurationBuilder().Build()).BuildAsync(tech.UserId,period.start,period.end,period.end);
        check(receipt!.Bonuses==150 && receipt.NetEstimated==150,"Comisión: aparece y suma en el recibo de nómina");
        await new PayrollDeductionLedgerService(db,Microsoft.Extensions.Logging.Abstractions.NullLogger<PayrollDeductionLedgerService>.Instance).RecordApplicationsAsync(tech.UserId,period.start,period.end,period.end);
        var paidReceipt = await new PayrollReceiptService(db,new ConfigurationBuilder().Build()).BuildAsync(tech.UserId,period.start,period.end,period.end);
        check(paidReceipt!.Bonuses==150,"Comisión: el recibo conserva el bono después de confirmar el pago");
        var next = await new PayrollReceiptService(db,new ConfigurationBuilder().Build()).BuildAsync(tech.UserId,new DateTime(2026,11,16),new DateTime(2026,11,30),new DateTime(2026,11,30));
        check(next!.Bonuses==0,"Comisión: no se repite en siguiente quincena");
        tech.ServiceOrderCommissionEnabled=false; await db.SaveChangesAsync();
        var disabled = new ServiceOrder { PublicToken=Guid.NewGuid().ToString("N"), ClientId=client.Id, ClaimedByUserId=tech.UserId }; db.Add(disabled); await db.SaveChangesAsync();
        await ServiceOrderCommissionService.FinalizeAsync(db,disabled,now);
        check(await db.EmployeeDeductions.CountAsync()==1,"Comisión: desactivada no genera bono");
        var fallback=new ServiceOrder { PublicToken=Guid.NewGuid().ToString("N"), ClientId=client.Id, AssignedUserId=assigned.UserId }; db.Add(fallback); await db.SaveChangesAsync();
        await ServiceOrderCommissionService.FinalizeAsync(db,fallback,now);
        check(await db.EmployeeDeductions.AnyAsync(x=>x.UserId==assigned.UserId && x.Amount==90),"Comisión: usa asignado si no hay técnico que la tomó");
        db.PayrollReceiptDispatches.Add(new PayrollReceiptDispatch { UserId=assigned.UserId,PeriodStart=period.start,PeriodEnd=period.end,PayrollDate=period.end,IsSent=true }); await db.SaveChangesAsync();
        var afterPay=new ServiceOrder { PublicToken=Guid.NewGuid().ToString("N"),ClientId=client.Id,AssignedUserId=assigned.UserId };db.Add(afterPay);await db.SaveChangesAsync();
        await ServiceOrderCommissionService.FinalizeAsync(db,afterPay,now);
        check((await db.EmployeeDeductions.SingleAsync(x=>x.SourceServiceOrderId==afterPay.Id)).StartDate.Day==16,"Comisión: respeta quincenas pagadas y usa la siguiente pendiente");
        var category=new QuoteCatalogItem { Name="Internet", Segment=QuoteSegment.Business, NodeType=QuoteNodeType.Category };
        var item=new QuoteCatalogItem { Name="Internet 20 MB", Segment=QuoteSegment.Business, NodeType=QuoteNodeType.Service, ParentId=category.Id, UnitPrice=1000, IsManualPrice=false, UnitPriceIncludesVat=false };
        db.AddRange(category,item); await db.SaveChangesAsync();
        var mail=new ClientCommunicationTests.RecordingEmail();
        QuoteModel Page() => new(db,new Pdf(),new ClientCommunicationTests.MemoryFiles(),mail,new ConfigurationBuilder().Build(),new Actions())
        { PageContext=new PageContext { HttpContext=new DefaultHttpContext { User=new ClaimsPrincipal(new ClaimsIdentity()) } } };
        var page=Page();check(await page.OnGetAsync(null,null) is PageResult && !page.IsInternal,"Cotizador: disponible sin iniciar sesión");
        check(page.ProspectOptions.Count==0 && page.ClientContactsPayload.Count==0,"Cotizador público: nunca expone lista de clientes o contactos");
        check(await page.OnPostPreviewAsync() is ForbidResult,"Cotizador público: no accede a herramienta interna de preview");
        page.Input=new QuoteModel.QuoteInput { Segment=QuoteSegment.Business, CustomerName="Nuevo contacto", CustomerEmail="nuevo@example.test", CustomerPhone="5526014566", CustomerLocation="CDMX", SelectedClientId=client.Id, GlobalDiscountType="percent", GlobalDiscountValue=100, GeneralTerms="No pagar", ManualLinesJson="[{\"ServiceName\":\"Fraude\",\"UnitPrice\":-999}]", LinesJson=JsonSerializer.Serialize(new[]{new {categoryId=category.Id,serviceId=item.Id,quantity=2}}) };
        check(await page.OnPostAsync() is RedirectToPageResult,"Cotizador público: guarda y confirma solicitud válida");
        var request=await db.QuoteRequests.Include(x=>x.Lines).SingleAsync();
        check(request.EstimatedTotal==2320 && request.Lines.Count==1 && request.GeneralTerms==null,"Cotizador público: ignora descuentos, precios manuales y condiciones manipuladas");
        check(request.ClientId==null && await db.Clients.CountAsync()==1 && client.Email=="privado@example.test","Cotizador público: no asocia IDs arbitrarios ni altera clientes existentes");
        check(mail.Messages.Count==1 && mail.Messages[0].To=="nuevo@example.test","Cotizador público: confirmación al correo capturado (SMTP simulado)");
        var bad=Page();bad.Input=page.Input;bad.Input.LinesJson=JsonSerializer.Serialize(new[]{new {categoryId=category.Id,serviceId=item.Id,quantity=-1}});
        check(await bad.OnPostAsync() is PageResult && await db.QuoteRequests.CountAsync()==1,"Cotizador: cantidad inválida no guarda ni envía");
        var edit=Page();edit.Input.EditQuoteId=request.Id;check(await edit.OnPostAsync() is ForbidResult,"Cotizador público: no permite editar cotizaciones por ID");
    }
    private sealed class Pdf : IQuoteRequestPdfRenderer { public Task<byte[]> RenderAsync(QuoteRequest r)=>Task.FromResult("%PDF-test"u8.ToArray()); }
    private sealed class Actions : IActionAccessService { public Task<bool> HasActionAsync(ClaimsPrincipal u,string a)=>Task.FromResult(false); public Task<HashSet<string>> GetAllowedActionsAsync(ClaimsPrincipal u)=>Task.FromResult(new HashSet<string>()); }
}
