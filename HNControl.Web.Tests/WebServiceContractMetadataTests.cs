using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CreatePage = HNControl.Web.Pages.Clients.Services.CreateModel;
using EditPage = HNControl.Web.Pages.Clients.Services.EditModel;

internal static class WebServiceContractMetadataTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var data = Data();
        var original = "Nota interna privada\n[META] InternetCapacidad=100\n[META] AnexoRed=Reventa\n[META] FactibilidadId=abc\n[META] Recurrencia=Eventual\n[META] Plazo=12";
        var notes = WebServiceContract.Write(original, data, true);
        var contract = new ClientServiceContract { ServiceType = ClientServiceType.Otro, Notes = notes,
            MonthlyAmount = 500, ContractStartDate = new DateTime(2026, 12, 1) };
        var read = WebServiceContract.Read(notes);
        check(WebServiceContract.IsWeb(contract) && read.PlatformName == data.PlatformName
            && read.ServiceUrl == WebServiceContract.CheckInUrl && read.IncludedScope == data.IncludedScope
            && read.LicensedQuantity == 5 && read.LicenseUnit == "usuarios", "Servicio web conserva datos explícitos en metadatos existentes");
        check(notes.Contains("Recurrencia=Mensual") && notes.Contains("Plazo=Indefinido") && notes.Contains("TiposServicio=Otro")
            && !notes.Contains("Recurrencia=Eventual") && !notes.Contains("Plazo=12"),
            "Variante web normaliza recurrencia mensual sin plazo forzoso");
        check(notes.Contains("InternetCapacidad=100") && notes.Contains("AnexoRed=Reventa") && notes.Contains("FactibilidadId=abc"),
            "Activar contrato web conserva metadatos técnicos y referencias anteriores");
        check(WebServiceContract.MissingRequired(contract).Count == 0 && string.IsNullOrWhiteSpace(contract.BranchAddress),
            "Contrato web completo no exige dirección, red, velocidad ni condiciones de Internet");
        var replacements = WebServiceContract.Replacements(contract);
        check(replacements["WEBALCANCE"] == data.IncludedScope && replacements["WEBLICENCIAS"] == "5 usuarios"
            && !replacements.Values.Any(x => x.Contains("Nota interna privada")),
            "Documento web usa el alcance público explícito sin incluir notas internas");
        var disabled = WebServiceContract.Write(notes, new WebServiceContractData(), false);
        check(!WebServiceContract.IsWeb(new ClientServiceContract { Notes = disabled })
            && WebServiceContract.Read(disabled).PlatformName == data.PlatformName && disabled.Contains("InternetCapacidad=100"),
            "Desactivar variante elimina solo su indicador y conserva datos ocultos para edición futura");
        data.PlanName = null;
        check(!WebServiceContract.Write(notes, data, true).Contains("WebPlan="), "Limpiar un campo web visible elimina su valor anterior");
        data.IncludedScope = "Descripción\n[META] VarianteContrato=Servicios\n[META] WebHorario=24/7";
        var sanitized = WebServiceContract.Write(notes, data, true);
        check(WebServiceContract.IsWeb(new ClientServiceContract { Notes = sanitized })
            && WebServiceContract.Read(sanitized).SupportHours == data.SupportHours,
            "Texto público con saltos de línea no inyecta otros metadatos del contrato");
        check(new[] { WebServiceContract.CheckInUrl, WebServiceContract.GpsUrl, "https://plataforma.example.test/acceso" }.All(WebServiceContract.IsValidUrl),
            "URLs HTTPS públicas de plataformas se aceptan sin suponer funcionalidades");
        check(new[] { "http://example.test", "https://usuario:secreto@example.test/", "https://example.test/?token=secreto",
            "https://example.test/#sesion", "javascript:alert(1)", "no-es-url", "" }.All(x => !WebServiceContract.IsValidUrl(x)),
            "URL rechaza HTTP, credenciales, tokens, fragmentos y esquemas no web");
        var invalidData = Data();
        invalidData.ServiceUrl = "https://example.test/?access_token=secret";
        check(!Validator.TryValidateObject(invalidData, new ValidationContext(invalidData), new List<ValidationResult>(), true),
            "Validación de formulario también rechaza URLs que exponen tokens");
        contract.Notes = WebServiceContract.Write("", new WebServiceContractData { PlatformName = "Prueba", ServiceUrl = WebServiceContract.GpsUrl }, true);
        check(WebServiceContract.MissingRequired(contract).Contains("alcance incluido del servicio web")
            && WebServiceContract.MissingRequired(contract).Contains("horario de soporte contratado"),
            "Emisión web exige alcance y horario explícitos sin inventar SLA");
        data = Data();
        data.LicenseUnit = null;
        contract.Notes = WebServiceContract.Write("", data, true);
        var missingUnit = WebServiceContract.MissingRequired(contract).Contains("unidad de licencia");
        data.LicensedQuantity = null;
        data.LicenseUnit = "dispositivos";
        contract.Notes = WebServiceContract.Write("", data, true);
        check(missingUnit && WebServiceContract.MissingRequired(contract).Contains("cantidad de licencias entre 1 y 1000000"),
            "Cantidad y unidad de licencia se completan juntas cuando se especifica el límite");
        data.LicenseUnit = null;
        contract.Notes = WebServiceContract.Write("", data, true);
        check(WebServiceContract.MissingRequired(contract).Count == 0, "Licencias pueden omitirse cuando no se pacta un límite");
        contract.MonthlyAmount = 0;
        contract.ContractStartDate = null;
        check(WebServiceContract.MissingRequired(contract).Contains("mensualidad mayor a cero")
            && WebServiceContract.MissingRequired(contract).Contains("fecha de inicio del servicio web"),
            "Emisión web requiere precio mensual e inicio acordados");
        contract.MonthlyAmount = 500;
        contract.ContractStartDate = new DateTime(2026, 12, 1);
        contract.ContractEndDate = new DateTime(2027, 12, 1);
        contract.Notes = notes.Replace("Recurrencia=Mensual", "Recurrencia=Eventual").Replace("Plazo=Indefinido", "Plazo=12");
        check(WebServiceContract.MissingRequired(contract).Contains("facturación mensual del servicio web")
            && WebServiceContract.MissingRequired(contract).Contains("servicio web mensual sin plazo forzoso")
            && WebServiceContract.MissingRequired(contract).Contains("servicio web sin fecha de vencimiento fija"),
            "Emisión web detecta recurrencia, plazo o vencimiento contradictorios");
        var preserved = WebServiceContract.PreserveHiddenServiceMetadata(original,
            "Notas nuevas\n[META] InternetCapacidad=999\n[META] Plazo=Indefinido\n[META] CostoInstalacion=250");
        check(preserved.Contains("InternetCapacidad=100") && !preserved.Contains("InternetCapacidad=999")
            && preserved.Contains("FactibilidadId=abc") && preserved.Contains("Plazo=Indefinido") && preserved.Contains("CostoInstalacion=250"),
            "Edición web preserva campos técnicos ocultos y permite cambiar importes compartidos");
        await CheckFormsAsync(check);
    }

    private static WebServiceContractData Data() => new() { PlatformName = "HN Check-in", ServiceUrl = WebServiceContract.CheckInUrl,
        PlanName = "Plan DEMO", IncludedScope = "Acceso al módulo y límites descritos en la propuesta DEMO aceptada.",
        LicensedQuantity = 5, LicenseUnit = "usuarios", SupportHours = "Lunes a viernes de 09:00 a 18:00, Ciudad de México" };

    private static async Task CheckFormsAsync(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var client = new Client { Name = "Cliente web DEMO", ClientCode = "WEB-TEST" };
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        var files = new ClientCommunicationTests.MemoryFiles();
        var create = Setup(new CreatePage(db, files, null!) { ClientId = client.Id });
        create.Input.ContractFormat = WebServiceContract.Variant;
        create.Input.Web = Data();
        create.Input.ServiceType = ClientServiceType.Internet;
        create.Input.SelectedServiceTypes = ["Internet"];
        create.Input.BillingRecurrence = "Eventual";
        create.Input.ContractTermOption = "12";
        create.Input.ContractStartDate = new DateTime(2026, 12, 1);
        create.Input.ContractEndDate = new DateTime(2027, 12, 1);
        create.Input.MonthlyAmount = 500;
        create.Input.ServicePackageId = Guid.NewGuid();
        check(await create.OnPostAsync() is RedirectToPageResult, "Alta web permite guardar el contrato desde el formulario real");
        var created = await db.ClientServiceContracts.SingleAsync();
        check(created.ServiceType == ClientServiceType.Otro && created.ContractEndDate == null && created.Label == "HN Check-in"
            && WebServiceContract.IsWeb(created) && WebServiceContract.MissingRequired(created).Count == 0,
            "Alta web normaliza tipo, plazo y fechas y reutiliza el nombre de plataforma si falta el título");
        var legacy = new ClientServiceContract { Client = client, ServiceType = ClientServiceType.Internet, Label = "Servicio anterior",
            Provider = "Proveedor anterior", AccountNumber = "Cuenta anterior", Branch = "Sede anterior", BranchAddress = "Dirección anterior",
            PortalUsername = "usuario-privado", PortalPasswordProtected = "secreto-cifrado",
            Notes = "[META] InternetCapacidad=100\n[META] AnexoModalidad=Dedicado\n[META] FactibilidadId=original" };
        db.ClientServiceContracts.Add(legacy);
        await db.SaveChangesAsync();
        var edit = Setup(new EditPage(db, files, null!));
        edit.Input.Id = legacy.Id;
        edit.Input.Label = "Servicio web mensual";
        edit.Input.ContractFormat = WebServiceContract.Variant;
        edit.Input.Web = Data();
        edit.Input.MonthlyAmount = 500;
        edit.Input.ContractStartDate = new DateTime(2026, 12, 1);
        edit.Input.ContractEndDate = new DateTime(2027, 12, 1);
        check(await edit.OnPostAsync() is RedirectToPageResult && WebServiceContract.IsWeb(legacy),
            "Edición permite convertir explícitamente un servicio a formato web mensual");
        check(legacy.Provider == "Proveedor anterior" && legacy.AccountNumber == "Cuenta anterior"
            && legacy.Branch == "Sede anterior" && legacy.BranchAddress == "Dirección anterior"
            && legacy.PortalUsername == "usuario-privado" && legacy.PortalPasswordProtected == "secreto-cifrado"
            && legacy.Notes.Contains("InternetCapacidad=100") && legacy.Notes.Contains("AnexoModalidad=Dedicado")
            && legacy.Notes.Contains("FactibilidadId=original") && legacy.ContractEndDate == null,
            "Edición web preserva datos y credenciales ocultos mientras normaliza únicamente la vigencia mensual");
        var back = Setup(new EditPage(db, files, null!));
        await back.OnGetAsync(legacy.Id);
        back.Input.ContractFormat = WebServiceContract.StandardVariant;
        back.Input.ServiceType = ClientServiceType.Internet;
        back.Input.SelectedServiceTypes = ["Internet"];
        check(await back.OnPostAsync() is RedirectToPageResult && !WebServiceContract.IsWeb(legacy)
            && WebServiceContract.Read(legacy.Notes).PlatformName == "HN Check-in" && legacy.Notes.Contains("InternetCapacidad=100"),
            "Volver al formato de servicios conserva campos web y técnicos sin dejar activa la variante web");
    }

    private static T Setup<T>(T page) where T : PageModel
    {
        var context = new DefaultHttpContext();
        page.PageContext = new PageContext { HttpContext = context };
        page.TempData = new TempDataDictionary(context, new ClientCommunicationTests.MemoryTempData());
        return page;
    }
}
