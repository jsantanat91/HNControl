using System.IO.Compression;
using System.Security.Claims;
using System.Xml.Linq;
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

internal static class ContractRenewalTests
{
    private sealed class Actions(params string[] allowed) : IActionAccessService
    {
        public Task<bool> HasActionAsync(ClaimsPrincipal user, string actionKey) => Task.FromResult(allowed.Contains(actionKey));
        public Task<HashSet<string>> GetAllowedActionsAsync(ClaimsPrincipal user) => Task.FromResult(allowed.ToHashSet());
    }

    public static async Task RunAsync(Action<bool, string> check)
    {
        var fixedDate = new DateTime(2027, 11, 30);
        var example = new ClientServiceContract { Notes = "[META] Plazo=12", ContractEndDate = fixedDate.AddMonths(3) };
        var term = ContractRenewals.Term(example)!;
        check(term.End == new DateTime(2028, 2, 29) && term.IsDue(fixedDate)
            && !term.IsDue(fixedDate.AddDays(-2)) && term.IsDue(term.End) && !term.IsDue(term.End.AddDays(1)),
            "La ventana incluye hoy y tres meses calendario, incluso febrero bisiesto");
        example.ContractEndDate = null;
        example.ContractStartDate = new DateTime(2027, 3, 1);
        check(ContractRenewals.Term(example)?.End == term.End, "Se calcula el último día de vigencia a partir del inicio y el plazo");
        example.ContractStartDate = null;
        check(ContractRenewals.Term(example) == null, "No se inventa un vencimiento si faltan las fechas");

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var own = new Client { Name = "Cliente A", ClientCode = "HN-0100", OwnerUserId = "asesor", IsActive = true };
        var other = new Client { Name = "Cliente B", ClientCode = "HN-0101", OwnerUserId = "otro", IsActive = true };
        var inactive = new Client { Name = "Baja", ClientCode = "HN-0102", IsActive = false };
        var today = AppTime.Today;
        ClientServiceContract Add(Client client, string notes, DateTime end) => db.Add(new ClientServiceContract
        {
            Client = client, Label = "Servicio", Notes = notes, ContractEndDate = end, ContractStartDate = end.AddYears(-1)
        }).Entity;
        var eligible = Add(own, "Datos anteriores\n[META] Plazo=12", today.AddMonths(2));
        var legacy = Add(own, "Observación previa", today);
        // Anchor the legacy term at consecutive first-of-month dates to avoid leap-day ambiguity.
        legacy.ContractEndDate = new DateTime(today.Year, today.Month, 1).AddMonths(1);
        legacy.ContractStartDate = legacy.ContractEndDate.Value.AddYears(-1);
        var foreign = Add(other, "[META] Plazo=12", today.AddDays(5));
        Add(inactive, "[META] Plazo=12", today);
        Add(own, "[META] Recurrencia=Eventual\n[META] Plazo=12", today);
        Add(own, "[META] Recurrencia=Unica\n[META] Plazo=12", today);
        Add(own, "[META] Plazo=Indefinido", today);
        Add(own, "[META] Plazo=12", today.AddDays(-1));
        await db.SaveChangesAsync();

        IndexModel PageFor(params string[] permissions)
        {
            var ctx = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "asesor") }, "test")) };
            return new IndexModel(db, new Actions(permissions))
            {
                PageContext = new PageContext { HttpContext = ctx },
                TempData = new TempDataDictionary(ctx, new ClientCommunicationTests.MemoryTempData())
            };
        }
        var editor = PageFor(AppActions.ClientsViewOwn, AppActions.ClientsEdit);
        editor.Name = "Sin coincidencias";
        await editor.OnGetAsync();
        check(editor.Rows.Count == 0 && editor.Expiring.Select(c => c.Id).ToHashSet().SetEquals([eligible.Id, legacy.Id]),
            "Aviso global del asesor independiente de búsqueda y paginación, solo para sus contratos elegibles");
        var all = await ContractRenewals.UpcomingAsync(db.ClientServiceContracts, today);
        check(all.Count == 3 && all.Any(c => c.Id == foreign.Id), "El panel general incluye clientes activos de varios responsables");
        check(await PageFor(AppActions.ClientsViewOwn).OnPostRenewAsync(own.Id, eligible.Id, eligible.UpdatedAt.Ticks, true, false) is ForbidResult,
            "Un usuario de consulta no puede registrar renovaciones");
        await editor.OnPostRenewAsync(other.Id, foreign.Id, foreign.UpdatedAt.Ticks, true, false);
        check(await db.ClientServiceContracts.AsNoTracking().Where(c => c.Id == foreign.Id).Select(c => c.ContractEndDate).SingleAsync() == foreign.ContractEndDate,
            "El servidor impide renovar el contrato de otro asesor");
        check(await editor.OnPostRenewAsync(own.Id, eligible.Id, eligible.UpdatedAt.Ticks, false, true) is BadRequestResult,
            "Se requiere confirmación del acuerdo");
        var expected = ContractRenewals.Term(eligible)!.NextEnd;
        await editor.OnPostRenewAsync(own.Id, eligible.Id, eligible.UpdatedAt.Ticks, true, true);
        await editor.OnPostRenewAsync(own.Id, eligible.Id, eligible.UpdatedAt.Ticks, true, true);
        var saved = await db.ClientServiceContracts.AsNoTracking().SingleAsync(c => c.Id == eligible.Id);
        check(saved.ContractEndDate == expected && saved.ContractStartDate == eligible.ContractStartDate && saved.Notes == eligible.Notes
            && (string?)editor.TempData["ClientDetailsInfoType"] == "warning", "Renovar conserva el inicio y un reintento no duplica el periodo");
        check(await ContractRenewals.RenewAsync(db.ClientServiceContracts, legacy.Id, legacy.UpdatedAt.Ticks, today)
            && ContractRenewals.Term(await db.ClientServiceContracts.AsNoTracking().SingleAsync(c => c.Id == legacy.Id))?.Months == 12,
            "Los contratos antiguos conservan el plazo real tras la primera renovación");
    }

    public static void CheckTemplate(string root, TemplateDocxService service, Action<bool, string> check)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XDocument Body(byte[] bytes)
        {
            using var archive = new ZipArchive(new MemoryStream(bytes));
            using var stream = archive.GetEntry("word/document.xml")!.Open();
            return XDocument.Load(stream);
        }
        string Text(XElement p) => string.Concat(p.Descendants(w + "t").Select(t => t.Value));
        var old = Body(File.ReadAllBytes(Path.Combine(root, "assets", "legal-templates", "Contrato_Blanco.docx")));
        var clauses = old.Descendants(w + "tbl").Last().Descendants(w + "p").Select(Text)
            .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        var client = new Client { Name = "Cliente de prueba" };
        var doc = new ClientLegalDocument
        {
            SigningSnapshotJson = ClientSigningSnapshot.Capture(client, null),
            CreatedAt = new DateTime(2026, 10, 2, 1, 0, 0, DateTimeKind.Utc)
        };
        var current = Body(service.BuildClientLegalDocx(doc, client, null));
        check(current.Descendants(w + "t").Any(t => t.Value == "Fecha de contrato: 01/10/2026"),
            "La fecha de la carátula es la de emisión en México, incluso si se firma otro día");
        var main = current.Descendants(w + "body").Single().Elements(w + "p").Select(Text).ToList();
        var approved = clauses.Select(s => s.Replace("el día 16 calendarios de cada mes", "el día 1 de cada mes")
            .Replace("al corte y “EL USUARIO” se obliga a pagar dichas facturas a más tardar el día 1 (uno) del mes correspondiente.",
                "al corte. La primera factura se pagará contra entrega del Servicio. A partir de la segunda factura, “EL USUARIO” se obliga a pagar a más tardar el día 15 (quince) del mismo mes del corte."));
        check(main.SkipWhile(s => s != clauses[0]).SequenceEqual(approved), "El contrato conserva las 26 cláusulas con el corte aprobado en la sexta");
        check(current.Descendants(w + "t").Any(t => t.Value == "01 de cada mes")
            && main.Any(s => s.Contains("será el día 1 de cada mes.")), "Carátula y cláusula sexta fijan el mismo corte mensual");
        check(main.Any(s => s.Contains("La primera factura se pagará contra entrega del Servicio.") &&
            s.Contains("el día 15 (quince) del mismo mes del corte.")), "El pago posterior a la entrega vence el día 15 del mes del corte");
        doc.SigningSnapshotJson = """{"Client":{"Name":"Cliente anterior"},"Contract":null}""";
        var legacy = Body(service.BuildClientLegalDocx(doc, client, null));
        check(legacy.Descendants(w + "tbl").Last().Descendants(w + "p").Select(Text).Where(s => !string.IsNullOrWhiteSpace(s)).SequenceEqual(clauses),
            "Una firma pendiente anterior mantiene la plantilla con la que se emitió");
    }
}
