using System.Security.Claims;
using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Migrations;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using InventoryPage = HNControl.Web.Pages.Admin.ServiceInventory.IndexModel;
using AccountPage = HNControl.Web.Pages.Admin.ServiceInventory.AccountModel;
using ServicePage = HNControl.Web.Pages.Admin.ServiceInventory.ServiceModel;

internal static class ServiceInventoryTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var provider = new EphemeralDataProtectionProvider();
        var inventory = new ServiceInventoryService(db, provider);
        var admin = Principal(AppRoles.Admin);
        var employee = Principal(AppRoles.Employee);
        var client = new Client { Name = "Cliente existente", ClientCode = "HN-TEST" };
        var lead = new Client { Name = "Prospecto", IsTemporaryLead = true };
        var oldCarrier = new InternetCarrier { Name = "Carrier histórico" };
        var oldService = new ClientCarrierService { Carrier = oldCarrier, Client = client, ServiceLabel = "Conservar" };
        db.AddRange(oldService, lead);
        await db.SaveChangesAsync();

        var input = new ServiceInventoryAccountInput { CarrierKey = "starlink", Label = "Cuenta operaciones", Email = "operaciones@example.test", AccountNumber = "ACC-001" };
        const string secret = "Clave-privada-<>-2026";
        var result = await inventory.SaveAccountAsync(input, null, secret, admin);
        check(result.Success, "Inventario crea cuenta nueva independiente del carrier histórico");
        var account = await db.ServiceInventoryAccounts.SingleAsync();
        check(account.PasswordProtected != secret && !account.PasswordProtected.Contains(secret), "Inventario cifra la contraseña al persistirla");
        check(await inventory.CopyPasswordAsync(account.Id, admin) == secret, "Inventario recupera contraseña mediante protector vinculado al ID");
        check(!JsonSerializer.Serialize(account).Contains("PasswordProtected") && !JsonSerializer.Serialize(account).Contains(secret), "La serialización de cuenta excluye su secreto cifrado");
        var denied = false;
        try { await inventory.CopyPasswordAsync(account.Id, employee); } catch (UnauthorizedAccessException) { denied = true; }
        check(denied, "Servicio de copia deniega empleados aunque conozcan el ID de la cuenta");
        denied = false;
        try { await inventory.SaveAccountAsync(input, null, secret, employee); } catch (UnauthorizedAccessException) { denied = true; }
        check(denied, "Servicio de cuentas deniega escrituras de empleados");
        check(!(await inventory.SaveAccountAsync(input, null, secret, admin)).Success && await db.ServiceInventoryAccounts.CountAsync() == 1,
            "Repetir el POST con el mismo identificador no crea cuentas duplicadas");

        var edit = new ServiceInventoryAccountInput { RecordId = account.Id, Version = account.Version, CarrierKey = account.CarrierKey, Label = "Cuenta actualizada", Email = account.Email };
        var originalCipher = account.PasswordProtected;
        var originalVersion = account.Version;
        check((await inventory.SaveAccountAsync(edit, account.Id, "Clave-inyectada-en-POST", admin)).Success
            && account.PasswordProtected == originalCipher && await inventory.CopyPasswordAsync(account.Id, admin) == secret,
            "Editar cuenta conserva la clave original aunque el POST intente sustituirla");
        edit.Version = originalVersion;
        edit.Label = "No sobrescribir";
        check(!(await inventory.SaveAccountAsync(edit, account.Id, null, admin)).Success && account.Label == "Cuenta actualizada",
            "Una edición obsoleta de cuenta no sobrescribe cambios recientes");

        var itemInput = new ServiceInventoryItemInput { CarrierKey = "starlink", Name = "Mini eventos", AccountId = account.Id, ClientId = client.Id,
            DeviceModel = "Mini", SerialNumber = "SER-01", KitNumber = "KIT-01", PlanType = "Itinerante", DeliveryType = "Satelital", MonthlyCost = 1500,
            SpeedMbps = 100, ContractStartsOn = new DateOnly(2026, 11, 1), ContractEndsOn = new DateOnly(2027, 11, 1), ContractMonths = 12 };
        check((await inventory.SaveItemAsync(itemInput, null, admin)).Success, "Inventario relaciona equipo Starlink con cuenta y cliente existentes");
        var second = new ServiceInventoryItemInput { Name = "Segunda antena", CarrierKey = "starlink", AccountId = account.Id };
        check((await inventory.SaveItemAsync(second, null, admin)).Success && await db.ServiceInventoryItems.CountAsync(x => x.AccountId == account.Id) == 2,
            "Una cuenta admite varias antenas independientes");
        var noAccount = new ServiceInventoryItemInput { Name = "Fibra oficina", CarrierKey = "telmex", ClientId = client.Id, MonthlyCost = 850,
            FixedIp = "203.0.113.10", SubnetMask = "255.255.255.248", Gateway = "203.0.113.9", Dns = "1.1.1.1, 8.8.8.8" };
        check((await inventory.SaveItemAsync(noAccount, null, admin)).Success, "Servicio de fibra se guarda sin cuenta ni credenciales");
        check(!(await inventory.SaveItemAsync(noAccount, null, admin)).Success && await db.ServiceInventoryItems.CountAsync() == 3,
            "Repetir el POST de alta de servicio no duplica conexiones");
        var mismatch = new ServiceInventoryItemInput { Name = "Rechazar relación", CarrierKey = "telmex", AccountId = account.Id };
        check(!(await inventory.SaveItemAsync(mismatch, null, admin)).Success, "No permite vincular servicio con cuenta de otro carrier");
        mismatch.AccountId = null; mismatch.ClientId = Guid.NewGuid();
        check(!(await inventory.SaveItemAsync(mismatch, null, admin)).Success, "No permite asignar inventario a un cliente inexistente");
        mismatch.ClientId = lead.Id;
        check(!(await inventory.SaveItemAsync(mismatch, null, admin)).Success, "Un prospecto no aparece como cliente asignable");
        mismatch.ClientId = null; mismatch.MonthlyCost = -1;
        check(!(await inventory.SaveItemAsync(mismatch, null, admin)).Success, "Inventario rechaza costos negativos en servidor");
        mismatch.MonthlyCost = 15.555m;
        check(!(await inventory.SaveItemAsync(mismatch, null, admin)).Success, "Inventario evita redondear silenciosamente costos con más de dos decimales");
        mismatch.MonthlyCost = null; mismatch.ContractStartsOn = new DateOnly(2026, 11, 10); mismatch.ContractEndsOn = new DateOnly(2026, 11, 1);
        check(!(await inventory.SaveItemAsync(mismatch, null, admin)).Success, "Inventario rechaza plazos con término anterior al inicio");
        mismatch.ContractStartsOn = null; mismatch.ContractEndsOn = null; mismatch.FixedIp = "IP inválida";
        check(!(await inventory.SaveItemAsync(mismatch, null, admin)).Success, "Inventario valida direcciones IP en servidor");

        var other = new ServiceInventoryAccountInput { CarrierKey = "otro", OtherCarrierName = "Proveedor Uno", Label = "Otra cuenta", Email = "otro@example.test" };
        await inventory.SaveAccountAsync(other, null, "OtraClave", admin);
        var wrongOther = new ServiceInventoryItemInput { CarrierKey = "otro", OtherCarrierName = "Proveedor Dos", Name = "No mezclar proveedores", AccountId = other.RecordId };
        check(!(await inventory.SaveItemAsync(wrongOther, null, admin)).Success, "Dos carriers de tipo Otro no comparten cuentas por accidente");
        var otherEntity = await db.ServiceInventoryAccounts.FindAsync(other.RecordId);
        otherEntity!.PasswordProtected = account.PasswordProtected;
        await db.SaveChangesAsync();
        check(await inventory.CopyPasswordAsync(other.RecordId, admin) == null, "Un secreto copiado a otra cuenta no puede descifrarse con ID distinto");

        edit.Version = account.Version; edit.Label = account.Label; edit.IsActive = false;
        check((await inventory.SaveAccountAsync(edit, account.Id, null, admin)).Success && await db.ServiceInventoryItems.CountAsync(x => x.AccountId == account.Id && x.IsActive) == 2,
            "Desactivar cuenta conserva servicios y estados existentes");
        var archivedLink = new ServiceInventoryItemInput { Name = "No asignar cuenta inactiva", CarrierKey = "starlink", AccountId = account.Id };
        check(!(await inventory.SaveItemAsync(archivedLink, null, admin)).Success, "No asocia servicios nuevos a cuentas inactivas");
        var item = await db.ServiceInventoryItems.FindAsync(itemInput.RecordId);
        itemInput.Version = item!.Version; itemInput.IsActive = false;
        check((await inventory.SaveItemAsync(itemInput, item.Id, admin)).Success && item.ClientId == client.Id && item.KitNumber == "KIT-01",
            "Desactivar servicio conserva cliente, kit y cuenta histórica");
        itemInput.Version = item.Version; itemInput.IsActive = true;
        check((await inventory.SaveItemAsync(itemInput, item.Id, admin)).Success, "Un servicio inactivo se reactiva sin perder datos");

        foreach (var role in new string?[] { null, AppRoles.Employee, AppRoles.InventoryManager })
        {
            var page = Page(new InventoryPage(db, inventory), role);
            check(await page.OnGetAsync() is ForbidResult && await page.OnPostCopyPasswordAsync(account.Id) is ForbidResult
                && page.Response.Headers.CacheControl.ToString().Contains("no-store"), "Inventario restringe listado y copia a administradores");
            check(await Page(new AccountPage(db, inventory), role).OnPostAsync() is ForbidResult
                && await Page(new ServicePage(db, inventory), role).OnPostAsync() is ForbidResult, "Inventario protege escrituras de ambas pantallas");
        }
        var list = Page(new InventoryPage(db, inventory) { Tab = "cuentas", Status = "todos" });
        await list.OnGetAsync();
        check(!JsonSerializer.Serialize(list.Accounts).Contains(secret) && !JsonSerializer.Serialize(list.Accounts).Contains(originalCipher),
            "El DTO del listado de cuentas no contiene contraseña ni ciphertext");
        var accountPage = Page(new AccountPage(db, inventory) { Id = account.Id });
        await accountPage.OnGetAsync();
        check(accountPage.Password == null && !JsonSerializer.Serialize(accountPage.Input).Contains(originalCipher), "Formulario de edición no precarga secretos");
        var copyPage = Page(new InventoryPage(db, inventory));
        var copied = await copyPage.OnPostCopyPasswordAsync(account.Id) as JsonResult;
        check(copied != null && JsonSerializer.Serialize(copied.Value).Contains("password") && copyPage.Response.Headers.CacheControl.ToString().Contains("no-store"),
            "Copia autorizada devuelve secreto solo por POST con respuesta no cacheable");
        var itemsPage = Page(new InventoryPage(db, inventory) { Carrier = "starlink", Assignment = "disponibles" });
        await itemsPage.OnGetAsync();
        check(itemsPage.Items.Count == 1 && itemsPage.Items[0].Id == second.RecordId, "Filtros combinados conservan carrier y asignación real");
        check(itemsPage.MonthlyCost == 2350 && itemsPage.UnpricedCount == 1, "Indicadores suman costos activos y distinguen importes no registrados");
        check(await db.InternetCarriers.CountAsync() == 1 && await db.ClientCarrierServices.CountAsync() == 1
            && (await db.ClientCarrierServices.SingleAsync()).ServiceLabel == "Conservar", "Todo el flujo mantiene intacto el módulo Carriers histórico");
        var operations = new AddServiceInventory().UpOperations;
        check(operations.OfType<CreateTableOperation>().Select(x => x.Name).Order().SequenceEqual(new[] { "ServiceInventoryAccounts", "ServiceInventoryItems" })
            && operations.All(x => x is CreateTableOperation or CreateIndexOperation), "Migración agrega únicamente tablas e índices del nuevo inventario");
        await using var postgresModel = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=inventory_model_only;Username=unused;Password=unused").Options);
        var snapshot = postgresModel.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var initialized = postgresModel.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
        var currentModel = postgresModel.GetService<IDesignTimeModel>().Model;
        var pending = postgresModel.GetService<IMigrationsModelDiffer>().GetDifferences(initialized.GetRelationalModel(), currentModel.GetRelationalModel());
        check(!pending.Any(op => ((op.GetType().GetProperty("Table")?.GetValue(op) ?? op.GetType().GetProperty("Name")?.GetValue(op)) as string)?.StartsWith("ServiceInventory", StringComparison.Ordinal) == true),
            "Snapshot PostgreSQL refleja todas las columnas, índices y relaciones de Inventario Servicios");
    }

    private static ClaimsPrincipal Principal(string? role) => new(new ClaimsIdentity(role == null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.Role, role) }, role == null ? null : "test"));
    private static T Page<T>(T page, string? role = AppRoles.Admin) where T : PageModel
    {
        var context = new DefaultHttpContext { User = Principal(role) };
        page.PageContext = new PageContext { HttpContext = context };
        page.TempData = new TempDataDictionary(context, new ClientCommunicationTests.MemoryTempData());
        return page;
    }
}
