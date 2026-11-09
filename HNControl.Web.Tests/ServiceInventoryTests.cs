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
        var noAccount = new ServiceInventoryItemInput { Name = "Fibra oficina", CarrierKey = "telmex", ClientId = client.Id, MonthlyCost = 850, HasNetworkDetails = true,
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
        check(!(await inventory.SaveItemAsync(mismatch, null, admin)).Success, "Inventario requiere inicio y plazo para calcular el vencimiento");
        mismatch.ContractStartsOn = null; mismatch.ContractEndsOn = null; mismatch.FixedIp = "IP inválida"; mismatch.HasNetworkDetails = true;
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
        check(await Page(new ServicePage(db, inventory), AppRoles.Employee).OnGetAccountsAsync() is ForbidResult,
            "Actualizar cuentas del formulario requiere administrador");
        var unrelatedInactive = new ServiceInventoryAccount { CarrierKey = "starlink", Label = "No ofrecer", Email = "inactiva@example.test", IsActive = false, PasswordProtected = "No exponer" };
        db.ServiceInventoryAccounts.Add(unrelatedInactive); await db.SaveChangesAsync();
        var refreshed = await Page(new ServicePage(db, inventory) { Id = item.Id }).OnGetAccountsAsync() as JsonResult;
        var refreshJson = JsonSerializer.Serialize(refreshed?.Value);
        using (var refreshDoc = JsonDocument.Parse(refreshJson))
        {
            var ids = refreshDoc.RootElement.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToHashSet();
            check(ids.Contains(account.Id) && ids.Contains(other.RecordId) && !ids.Contains(unrelatedInactive.Id)
                && !refreshJson.Contains("PasswordProtected") && !refreshJson.Contains(originalCipher) && !refreshJson.Contains(secret),
                "Refresco autorizado devuelve cuentas activas y cuenta actual sin incluir secretos ni otras inactivas");
        }
        var mobile = new HNControl.Web.Controllers.Mobile.ServiceInventoryController(db);
        var mobileHttp = new DefaultHttpContext { User = Principal(AppRoles.Employee) };
        mobile.ControllerContext = new ControllerContext { HttpContext = mobileHttp };
        var mobileResult = (OkObjectResult)await mobile.List();
        var mobileJson = JsonSerializer.Serialize(mobileResult.Value);
        check(!mobileJson.Contains(secret) && !mobileJson.Contains("PasswordProtected") && !mobileJson.Contains(originalCipher), "Consulta móvil no expone contraseñas ni secretos cifrados");
        using (var accountRows = JsonDocument.Parse(mobileJson))
        {
            var itemJson = accountRows.RootElement.GetProperty("Items").EnumerateArray().Single(x => x.GetProperty("Id").GetGuid() == item.Id);
            check(itemJson.GetProperty("ProviderAccountNumber").GetString() == account.AccountNumber
                && itemJson.GetProperty("AccountEmail").GetString() == account.Email
                && !itemJson.GetProperty("CanCopyPassword").GetBoolean(), "Ficha móvil incluye cuenta y correo sin habilitar contraseñas a empleados");
        }
        using (var filtered = JsonDocument.Parse(JsonSerializer.Serialize(((OkObjectResult)await mobile.List(carrier: "no-existe")).Value)))
            check(filtered.RootElement.GetProperty("Total").GetInt32() == 0, "Filtro móvil por proveedor se aplica en servidor");
        check(await mobile.CopyPassword(item.Id, inventory) is ForbidResult, "Empleado no puede copiar contraseña de la cuenta desde la API");
        mobileHttp.User = Principal(AppRoles.Admin);
        var secretResult = (OkObjectResult)await mobile.CopyPassword(item.Id, inventory);
        using (var secretJson = JsonDocument.Parse(JsonSerializer.Serialize(secretResult.Value)))
            check(secretJson.RootElement.GetProperty("password").GetString() == secret, "Administrador puede copiar contraseña de la cuenta vinculada");
        check(await mobile.CopyPassword(Guid.NewGuid(), inventory) is NotFoundObjectResult, "Copia móvil valida el servicio y su cuenta vinculada");
        mobileHttp.User = Principal(AppRoles.Seller);
        check(await mobile.List() is ForbidResult, "Consulta móvil rechaza roles sin autorización operativa");
        await RunProviderRulesAsync(check);
    }

    private static async Task RunProviderRulesAsync(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var inventory = new ServiceInventoryService(db, new EphemeralDataProtectionProvider());
        var admin = Principal(AppRoles.Admin);
        ServiceInventoryItemInput New(string carrier = "telmex") => new() { Name = "Reglas proveedor", CarrierKey = carrier, OtherCarrierName = carrier == "otro" ? "Proveedor personalizado" : null };

        foreach (var plan in new[] { "Residencial", "Itinerante", "Empresarial" })
        {
            var starlink = New("starlink"); starlink.PlanType = plan; starlink.DeliveryType = "Fibra óptica"; starlink.SupportPhone = new string('9', 500);
            var result = await inventory.SaveItemAsync(starlink, null, admin);
            var row = await db.ServiceInventoryItems.FindAsync(starlink.RecordId);
            check(result.Success && row?.PlanType == plan && row.DeliveryType == "Satelital" && row.SupportPhone == "Online",
                $"Starlink acepta {plan} y fija Satelital/Online aunque el POST intente cambiarlos");
        }
        var invalidStarlink = New("starlink"); invalidStarlink.PlanType = "Dedicado";
        check(!(await inventory.SaveItemAsync(invalidStarlink, null, admin)).Success, "Starlink rechaza tipos de plan ajenos a su catálogo");
        foreach (var carrier in new[] { "telmex", "totalplay", "izzi", "megacable", "otro" })
        {
            foreach (var plan in new[] { "Dedicado", "Best effort" })
            {
                var input = New(carrier); input.PlanType = plan; input.DeliveryType = "Microonda";
                check((await inventory.SaveItemAsync(input, null, admin)).Success, $"{carrier} admite plan {plan} con tecnología independiente");
            }
            var inputInvalid = New(carrier); inputInvalid.PlanType = "Residencial";
            check(!(await inventory.SaveItemAsync(inputInvalid, null, admin)).Success, $"{carrier} no admite planes nuevos de Starlink");
            inputInvalid.PlanType = "Dedicado"; inputInvalid.DeliveryType = "Satelital";
            check(!(await inventory.SaveItemAsync(inputInvalid, null, admin)).Success, $"{carrier} rechaza Satelital reservado para Starlink");
        }
        foreach (var technology in new[] { "Fibra óptica", "Microonda", "Otro" })
        {
            var input = New(); input.DeliveryType = technology;
            check((await inventory.SaveItemAsync(input, null, admin)).Success, $"Conexión permite tecnología {technology}");
        }
        var noLegacyCreate = New(); noLegacyCreate.DeliveryType = "Dedicado";
        check(!(await inventory.SaveItemAsync(noLegacyCreate, null, admin)).Success, "Altas nuevas no usan clasificación comercial como tecnología");

        foreach (var (start, months, expected) in new[] {
            (new DateOnly(2024, 1, 31), 1, new DateOnly(2024, 2, 29)),
            (new DateOnly(2025, 1, 31), 1, new DateOnly(2025, 2, 28)),
            (new DateOnly(2024, 2, 29), 12, new DateOnly(2025, 2, 28)),
            (new DateOnly(2026, 8, 31), 1, new DateOnly(2026, 9, 30)),
            (new DateOnly(2026, 11, 8), 240, new DateOnly(2046, 11, 8)) })
        {
            var input = New(); input.ContractStartsOn = start; input.ContractMonths = months; input.ContractEndsOn = new DateOnly(2000, 1, 1);
            var result = await inventory.SaveItemAsync(input, null, admin);
            check(result.Success && (await db.ServiceInventoryItems.FindAsync(input.RecordId))?.ContractEndsOn == expected,
                $"Servidor calcula {start:yyyy-MM-dd} + {months} meses como {expected:yyyy-MM-dd}, ignorando fecha fin enviada");
        }
        foreach (var months in new[] { 0, -1, 241 })
        {
            var input = New(); input.ContractStartsOn = new DateOnly(2026, 11, 1); input.ContractMonths = months;
            check(!(await inventory.SaveItemAsync(input, null, admin)).Success, $"Plazo {months} se rechaza sin guardar");
        }
        var overflow = New(); overflow.ContractStartsOn = new DateOnly(9999, 12, 31); overflow.ContractMonths = 1;
        check(!(await inventory.SaveItemAsync(overflow, null, admin)).Success, "Fecha fuera de rango retorna validación y no una excepción de calendario");
        var incomplete = New(); incomplete.ContractMonths = 12;
        check(!(await inventory.SaveItemAsync(incomplete, null, admin)).Success, "Un plazo nuevo requiere fecha de inicio");
        incomplete.ContractMonths = null; incomplete.ContractStartsOn = new DateOnly(2026, 11, 1);
        check(!(await inventory.SaveItemAsync(incomplete, null, admin)).Success, "Una fecha de inicio nueva requiere meses");
        var manualEnd = New(); manualEnd.ContractEndsOn = new DateOnly(2027, 1, 1);
        check((await inventory.SaveItemAsync(manualEnd, null, admin)).Success && (await db.ServiceInventoryItems.FindAsync(manualEnd.RecordId))?.ContractEndsOn == null,
            "Una fecha fin posteada sin inicio/plazo no crea un vencimiento manual");

        var hiddenNetwork = New(); hiddenNetwork.FixedIp = "invalid"; hiddenNetwork.Dns = new string('x', 2000); hiddenNetwork.Lan = new string('x', 2000);
        check((await inventory.SaveItemAsync(hiddenNetwork, null, admin)).Success && (await db.ServiceInventoryItems.FindAsync(hiddenNetwork.RecordId))?.FixedIp == "",
            "Alta con red desactivada ignora valores ocultos incluso inválidos o demasiado largos");
        var visibleNetwork = New(); visibleNetwork.HasNetworkDetails = true; visibleNetwork.FixedIp = "invalid";
        check(!(await inventory.SaveItemAsync(visibleNetwork, null, admin)).Success, "Red activada exige IP válida en servidor");
        visibleNetwork.FixedIp = "203.0.113.10"; visibleNetwork.Dns = new string('x', 201);
        check(!(await inventory.SaveItemAsync(visibleNetwork, null, admin)).Success, "Red activada conserva límites de longitud y validación DNS");
        visibleNetwork.Dns = "1.1.1.1, 8.8.8.8"; visibleNetwork.SubnetMask = "255.255.255.248"; visibleNetwork.Gateway = "203.0.113.9";
        visibleNetwork.Lan = "192.168.10.0/24"; visibleNetwork.Ddns = "equipo.example.test";
        check((await inventory.SaveItemAsync(visibleNetwork, null, admin)).Success, "Datos de red opcionales completos se guardan al habilitarlos");
        var networkRow = (await db.ServiceInventoryItems.FindAsync(visibleNetwork.RecordId))!;
        var networkPage = Page(new ServicePage(db, inventory) { Id = networkRow.Id }); await networkPage.OnGetAsync(null);
        check(networkPage.Input.HasNetworkDetails && networkPage.HasStoredNetworkDetails, "Editar detecta y abre los datos de red existentes");
        visibleNetwork.Version = networkRow.Version; visibleNetwork.HasNetworkDetails = false;
        visibleNetwork.FixedIp = "overposted"; visibleNetwork.Dns = new string('x', 2000); visibleNetwork.Lan = "overposted";
        check((await inventory.SaveItemAsync(visibleNetwork, networkRow.Id, admin)).Success && networkRow.FixedIp == "203.0.113.10"
            && networkRow.Dns == "1.1.1.1, 8.8.8.8" && networkRow.Lan == "192.168.10.0/24" && networkRow.Ddns == "equipo.example.test",
            "Cerrar sección red conserva datos persistidos e ignora intentos de cambiarlos mediante campos ocultos");
        var failedNetworkPage = Page(new ServicePage(db, inventory) { Id = networkRow.Id }); await failedNetworkPage.OnGetAsync(null);
        failedNetworkPage.Input.HasNetworkDetails = false; failedNetworkPage.Input.FixedIp = null; failedNetworkPage.Input.Dns = null;
        failedNetworkPage.ModelState.AddModelError("Input.Name", "Corrige el nombre");
        failedNetworkPage.ModelState.SetModelValue("Input.FixedIp", "valor oculto manipulado", "valor oculto manipulado");
        check(await failedNetworkPage.OnPostAsync() is PageResult && failedNetworkPage.Input.FixedIp == "203.0.113.10"
            && failedNetworkPage.Input.Dns == "1.1.1.1, 8.8.8.8" && !failedNetworkPage.ModelState.ContainsKey("Input.FixedIp"),
            "Un error de otro campo repuebla la red cerrada desde BD y limpia valores ocultos del ModelState");
        failedNetworkPage.ModelState.Clear(); failedNetworkPage.Input.HasNetworkDetails = true;
        check(await failedNetworkPage.OnPostAsync() is RedirectToPageResult && networkRow.FixedIp == "203.0.113.10" && networkRow.Dns == "1.1.1.1, 8.8.8.8",
            "Reabrir red después de corregir un error conserva los datos guardados");

        var legacy = new ServiceInventoryItem { CarrierKey = "telmex", Name = "Registro 2.5.12", PlanType = "Residencial", DeliveryType = "Dedicado",
            ContractEndsOn = new DateOnly(2028, 2, 10), FixedIp = "203.0.113.20" };
        var starlinkLegacy = new ServiceInventoryItem { CarrierKey = "starlink", Name = "Starlink previo", PlanType = "Residencial", DeliveryType = "Best effort", SupportPhone = "5555555555" };
        var startOnlyLegacy = new ServiceInventoryItem { CarrierKey = "telmex", Name = "Plazo histórico incompleto", ContractStartsOn = new DateOnly(2026, 1, 1), ContractEndsOn = new DateOnly(2027, 1, 15) };
        db.AddRange(legacy, starlinkLegacy, startOnlyLegacy); await db.SaveChangesAsync();
        var legacyPage = Page(new ServicePage(db, inventory) { Id = legacy.Id }); await legacyPage.OnGetAsync(null);
        check(legacyPage.LegacyPlanType == "Residencial" && legacyPage.LegacyDeliveryType == "Dedicado" && legacyPage.SuggestedPlanType == "Dedicado"
            && legacyPage.HasLegacyContractEnd && legacyPage.StoredContractEnd == legacy.ContractEndsOn && legacyPage.Input.HasNetworkDetails,
            "Formulario identifica clasificaciones y vencimiento históricos sin inferir una tecnología");
        legacyPage.Input.Name = "Nombre editado"; legacyPage.Input.ContractEndsOn = new DateOnly(2035, 1, 1); legacyPage.Input.HasNetworkDetails = false;
        check((await inventory.SaveItemAsync(legacyPage.Input, legacy.Id, admin)).Success && legacy.PlanType == "Residencial" && legacy.DeliveryType == "Dedicado"
            && legacy.ContractEndsOn == new DateOnly(2028, 2, 10) && legacy.FixedIp == "203.0.113.20",
            "Editar otros datos conserva plan/conexión/fin histórico y red guardada");
        legacyPage.Input.Version = legacy.Version; legacyPage.Input.PlanType = "Itinerante";
        check(!(await inventory.SaveItemAsync(legacyPage.Input, legacy.Id, admin)).Success, "Excepción histórica no permite inyectar otra clasificación inválida");
        legacyPage.Input.PlanType = legacy.PlanType; legacyPage.Input.CarrierKey = "izzi";
        check(!(await inventory.SaveItemAsync(legacyPage.Input, legacy.Id, admin)).Success, "Cambiar de proveedor no arrastra valores históricos ajenos al catálogo");
        legacyPage.Input.CarrierKey = "telmex"; legacyPage.Input.PlanType = "Dedicado"; legacyPage.Input.DeliveryType = "Microonda";
        check((await inventory.SaveItemAsync(legacyPage.Input, legacy.Id, admin)).Success && legacy.PlanType == "Dedicado" && legacy.DeliveryType == "Microonda",
            "Una selección explícita reclasifica el plan antiguo y guarda la tecnología elegida");
        var startOnlyPage = Page(new ServicePage(db, inventory) { Id = startOnlyLegacy.Id }); await startOnlyPage.OnGetAsync(null);
        check((await inventory.SaveItemAsync(startOnlyPage.Input, startOnlyLegacy.Id, admin)).Success && startOnlyLegacy.ContractEndsOn == new DateOnly(2027, 1, 15),
            "Legado con inicio y fin pero sin plazo conserva su información al editar");
        startOnlyPage.Input.Version = startOnlyLegacy.Version; startOnlyPage.Input.ContractMonths = 12;
        check((await inventory.SaveItemAsync(startOnlyPage.Input, startOnlyLegacy.Id, admin)).Success && startOnlyLegacy.ContractEndsOn == new DateOnly(2027, 1, 1),
            "Completar el plazo de un legado calcula vencimiento desde la pareja explícita");
        var starlinkPage = Page(new ServicePage(db, inventory) { Id = starlinkLegacy.Id }); await starlinkPage.OnGetAsync(null);
        check(starlinkPage.Input.SupportPhone == "Online" && starlinkPage.Input.DeliveryType == "Satelital" && starlinkPage.LegacyDeliveryType == "Best effort",
            "Starlink previo muestra soporte Online y tecnología Satelital con referencia histórica");
        starlinkPage.Input.SupportPhone = new string('9', 500); starlinkPage.Input.DeliveryType = "valor manipulado";
        starlinkPage.ModelState.AddModelError("Input.SupportPhone", "Error sobre valor que el servidor reemplaza");
        starlinkPage.ModelState.AddModelError("Input.DeliveryType", "Error sobre valor que el servidor reemplaza");
        check(await starlinkPage.OnPostAsync() is RedirectToPageResult && starlinkLegacy.SupportPhone == "Online" && starlinkLegacy.DeliveryType == "Satelital",
            "Handler web también impone campos Starlink e ignora sobreescrituras de solo lectura");

        var catalogsValid = true;
        foreach (var carrier in new[] { "starlink", "telmex" })
        foreach (var planName in ServiceInventoryDisplay.GetPlanNames(carrier))
        {
            var input = New(carrier); input.PlanName = planName; input.KitNumber = carrier == "starlink" ? "KIT-CATALOGO" : null;
            var saved = await inventory.SaveItemAsync(input, null, admin);
            var row = await db.ServiceInventoryItems.FindAsync(input.RecordId);
            catalogsValid &= saved.Success && row != null && row.PlanName == planName && (carrier != "starlink" || row.KitNumber == "KIT-CATALOGO");
        }
        check(catalogsValid, "Catálogos de nombres Starlink e Internet se guardan y Starlink conserva su kit");
        var rejectedPlanNames = true;
        foreach (var (carrier, planName) in new[] { ("starlink", "Internet 100"), ("telmex", "Itinerante Ilimitado"), ("starlink", "Plan libre nuevo"), ("telmex", "Plan libre nuevo") })
        {
            var input = New(carrier); input.PlanName = planName;
            rejectedPlanNames &= !(await inventory.SaveItemAsync(input, null, admin)).Success;
        }
        check(rejectedPlanNames, "Nombres de plan nuevos rechazan valores libres y catálogos de otro proveedor");
        var business = New(); business.PlanType = "Empresarial"; business.PlanName = "Internet 1000";
        check((await inventory.SaveItemAsync(business, null, admin)).Success, "Empresarial también es tipo de plan válido para proveedores terrestres");
        var emptyPlan = New(); emptyPlan.KitNumber = new string('X', 1000);
        check((await inventory.SaveItemAsync(emptyPlan, null, admin)).Success && (await db.ServiceInventoryItems.FindAsync(emptyPlan.RecordId))?.KitNumber == "",
            "Plan vacío es opcional y un kit posteado para otro proveedor se ignora incluso si excede el límite");
        var priorPlan = new ServiceInventoryItem { CarrierKey = "telmex", Name = "Plan histórico", PlanName = "Paquete empresarial anterior", PlanType = "Empresarial", DeliveryType = "Fibra óptica", KitNumber = "KIT-HISTORICO" };
        db.ServiceInventoryItems.Add(priorPlan); await db.SaveChangesAsync();
        var priorPlanPage = Page(new ServicePage(db, inventory) { Id = priorPlan.Id }); await priorPlanPage.OnGetAsync(null);
        check(priorPlanPage.LegacyPlanName == priorPlan.PlanName && priorPlanPage.LegacyPlanType == null && priorPlanPage.Input.KitNumber == null,
            "Editar ofrece el nombre de plan histórico y no expone kits de otros proveedores");
        priorPlanPage.Input.KitNumber = "KIT-MANIPULADO";
        priorPlanPage.ModelState.AddModelError("Input.KitNumber", "Campo oculto que no aplica");
        check(await priorPlanPage.OnPostAsync() is RedirectToPageResult && priorPlan.PlanName == "Paquete empresarial anterior" && priorPlan.KitNumber == "KIT-HISTORICO",
            "Guardar mantiene plan y kit históricos y no acepta cambios ocultos del kit");
        priorPlanPage.Input.Version = priorPlan.Version; priorPlanPage.Input.PlanName = "Otro nombre libre";
        check(!(await inventory.SaveItemAsync(priorPlanPage.Input, priorPlan.Id, admin)).Success, "Excepción de plan histórico solo acepta exactamente el nombre previamente guardado");
        priorPlanPage.Input.PlanName = priorPlan.PlanName; priorPlanPage.Input.CarrierKey = "totalplay";
        check(!(await inventory.SaveItemAsync(priorPlanPage.Input, priorPlan.Id, admin)).Success, "Cambiar proveedor requiere escoger su catálogo y no traslada planes históricos libres");
        priorPlanPage.Input.CarrierKey = "telmex"; priorPlanPage.Input.PlanName = "Internet 500";
        check((await inventory.SaveItemAsync(priorPlanPage.Input, priorPlan.Id, admin)).Success && priorPlan.PlanName == "Internet 500" && priorPlan.KitNumber == "KIT-HISTORICO",
            "Elegir un nombre del catálogo actualiza el plan sin borrar información histórica del kit");
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
