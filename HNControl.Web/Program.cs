using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using HNControl.Web.Services.Clients;
using HNControl.Web.Services.Monitoring;
using HNControl.Web.Services.Mobile;
using HNControl.Web.Services.Tickets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using System.Text;
using Npgsql;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// --------------------
// DB (PostgreSQL)
// --------------------
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
{
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
});

// --------------------
// Identity + Roles
// --------------------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;

        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/Account/Login";
    opt.AccessDeniedPath = "/Account/Denied";
    opt.Cookie.Name = "HNControl.Auth";
});

builder.Services.AddAuthentication()
    .AddCookie("ClientPortal", opt =>
    {
        opt.LoginPath = "/Portal/Login";
        opt.AccessDeniedPath = "/Portal/Login";
        opt.Cookie.Name = "HNControl.ClientPortal";
        opt.ExpireTimeSpan = TimeSpan.FromDays(30);
        opt.SlidingExpiration = true;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, opt =>
    {
        var issuer = builder.Configuration["Jwt:Issuer"] ?? "HNControl.Mobile";
        var audience = builder.Configuration["Jwt:Audience"] ?? "HNControl.Mobile";
        var key = builder.Configuration["Jwt:Key"] ?? "DEV_ONLY_CHANGE_THIS_KEY_32_CHARS_MIN";

        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// --------------------
// Authorization policies
// --------------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireAssertion(ctx =>
    {
        if (AppRoles.IsGlobalAdmin(ctx.User)) return true;
        if (ctx.User?.Identity?.IsAuthenticated != true) return false;
        if (!ctx.User.IsInRole(AppRoles.InventoryManager) && !ctx.User.IsInRole(AppRoles.WarehouseLead)) return false;

        var path = (ctx.Resource as HttpContext)?.Request.Path ?? PathString.Empty;
        return path.StartsWithSegments("/Admin/Inventory", StringComparison.OrdinalIgnoreCase);
    }));
    options.AddPolicy("EmployeeOnly", policy => policy.RequireRole(AppRoles.Employee, AppRoles.Admin, AppRoles.SuperAdmin));
    options.AddPolicy("InventorySupervisor", policy => policy.RequireAssertion(ctx =>
    {
        if (AppRoles.IsGlobalAdmin(ctx.User)) return true;
        if (ctx.User?.Identity?.IsAuthenticated != true) return false;
        if (!ctx.User.IsInRole(AppRoles.InventoryManager) && !ctx.User.IsInRole(AppRoles.WarehouseLead)) return false;

        var path = (ctx.Resource as HttpContext)?.Request.Path ?? PathString.Empty;
        return path.StartsWithSegments("/Admin/Inventory", StringComparison.OrdinalIgnoreCase);
    }));
});
builder.Services.AddControllers();
builder.Services.AddScoped<MobileJwtTokenService>();

// --------------------
// Permisos por módulo (Employee)
// --------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IModuleAccessService, ModuleAccessService>();
builder.Services.AddScoped<IActionAccessService, ActionAccessService>();
builder.Services.AddScoped<ModulePermissionPageFilter>();

// --------------------
// Monitoreo
// --------------------
builder.Services.AddHttpClient("monitoring");
builder.Services.AddHttpClient("whatsapp", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IMonitorProbeService, MonitorProbeService>();
builder.Services.AddHostedService<MonitorWorker>();

// --------------------
// Monitoreo
// --------------------
builder.Services.AddHttpClient("monitoring");
builder.Services.AddScoped<IMonitorProbeService, MonitorProbeService>();
builder.Services.AddHostedService<MonitorWorker>();

// --------------------
// Razor Pages routing / auth conventions
// --------------------
builder.Services.AddRazorPages(options =>
{
    // Todo requiere login por default
    options.Conventions.AuthorizeFolder("/");

    // Account libre
    options.Conventions.AllowAnonymousToFolder("/Account");

    // Público (links token órdenes)
    options.Conventions.AllowAnonymousToFolder("/Public");
    options.Conventions.AllowAnonymousToFolder("/Portal");

    // Admin
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Admin/ServiceOrders", "AdminOnly");

    // Evaluación 360
    options.Conventions.AuthorizeFolder("/Admin/Eval360", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Eval360", "EmployeeOnly");


    // Clientes: admin + empleado (el control fino se hace por accion)
    options.Conventions.AuthorizeFolder("/Clients", "EmployeeOnly");

    // Proyectos: admin + empleado
    options.Conventions.AuthorizeFolder("/Projects", "EmployeeOnly");
    options.Conventions.AuthorizeFolder("/Sales", "EmployeeOnly");

    // Documentos: admin + empleado
    options.Conventions.AuthorizeFolder("/Knowledge", "EmployeeOnly");

    // Empleados: admin gestiona en /Admin/Employees, el empleado ve su ficha en /Employees/MyProfile
    // Importante: NO autorizar toda la carpeta /Employees como AdminOnly.
    // En Razor Pages las convenciones se acumulan y terminarías exigiendo Admin + Employee a la vez.
    options.Conventions.AuthorizePage("/Employees/MyProfile", "EmployeeOnly");
    options.Conventions.AuthorizePage("/Employees/OrgChart", "EmployeeOnly");

    // Viáticos: empleados y admin
    options.Conventions.AuthorizeFolder("/Viaticos", "EmployeeOnly");

    // Carriers (Internet): empleados ven + notas, admin administra
    options.Conventions.AuthorizeFolder("/Carriers", "EmployeeOnly");

    // Inventarios: empleado solicita entrada/salida, admin aprueba
    options.Conventions.AuthorizeFolder("/Inventory", "EmployeeOnly");

    // Monitoreo: empleado y admin (admin gestiona targets)
    options.Conventions.AuthorizeFolder("/Monitoring", "EmployeeOnly");
    options.Conventions.AuthorizeFolder("/Tickets", "EmployeeOnly");

    // Monitoreo: empleados (lectura) + admin
    options.Conventions.AuthorizeFolder("/Monitoring", "EmployeeOnly");
    // Vacaciones e incidencias
    options.Conventions.AuthorizeFolder("/Leaves", "EmployeeOnly");
    options.Conventions.AuthorizeFolder("/Admin/Leaves", "AdminOnly");

    // Exámenes
    options.Conventions.AuthorizeFolder("/Exams", "EmployeeOnly");
    options.Conventions.AuthorizeFolder("/Admin/Exams", "AdminOnly");


    // Seguridad / permisos (Admin)
    options.Conventions.AuthorizeFolder("/Admin/Security", "AdminOnly");

    // Alias de modulos principales (sin mover implementacion actual)
    options.Conventions.AddPageRoute("/Sales/Dashboard", "/Ventas");
    options.Conventions.AddPageRoute("/Sales/Workflow", "/Ventas/Workflow");
    options.Conventions.AddPageRoute("/Sales/Templates", "/Ventas/Plantillas");
    options.Conventions.AddPageRoute("/Sales/Prospects", "/Ventas/Prospectos");
    options.Conventions.AddPageRoute("/Sales/Calls", "/Ventas/Llamadas");
    options.Conventions.AddPageRoute("/Sales/Feasibility", "/Ventas/Factibilidad");
    options.Conventions.AddPageRoute("/Admin/Quotes/Requests", "/Ventas/Cotizaciones");
    options.Conventions.AddPageRoute("/Sales/My", "/Ventas/MisCotizaciones");
    options.Conventions.AddPageRoute("/Projects/Sales/Index", "/Ventas/Gestion");
    options.Conventions.AddPageRoute("/Projects/Billing/Index", "/Facturacion");
    options.Conventions.AddPageRoute("/Employees/OrgChart", "/Employees/Organigrama");
    options.Conventions.AddPageRoute("/Account/ChangePassword", "/Cuenta/CambiarContrasena");
})
.AddMvcOptions(o => o.Filters.AddService<ModulePermissionPageFilter>());

// --------------------
// Servicios (Storage / Secrets / Email / PDF)
// --------------------
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();

// DataProtection para cifrar accesos de proyectos (passwords).
// Las llaves se persisten en disco (volumen) y se fija el ApplicationName para que
// sobrevivan a reconstrucciones/recreaciones del contenedor. Sin esto, al perder el
// keyring TODOS los secretos guardados (WhatsApp, SMTP, Mercado Pago, CSD...) quedan
// indescifrables y se envian vacios.
var dpKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "dp-keys");
Directory.CreateDirectory(dpKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("HNControl")
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));
builder.Services.AddScoped<ISecretProtector, SecretProtector>();

// EMAIL: evitamos ambigüedad con Identity.UI IEmailSender
builder.Services.AddScoped<HNControl.Web.Services.IEmailSender, HNControl.Web.Services.SmtpEmailSender>();
builder.Services.AddScoped<IWhatsAppSender, MetaWhatsAppSender>();

// PDF renderer para órdenes de servicio
builder.Services.AddScoped<IServiceOrderPdfRenderer, ServiceOrderPdfRenderer>();
builder.Services.AddScoped<IQuoteRequestPdfRenderer, QuoteRequestPdfRenderer>();
builder.Services.AddScoped<IClientLegalPdfRenderer, ClientLegalPdfRenderer>();
builder.Services.AddScoped<IProjectDeliveryPdfRenderer, ProjectDeliveryPdfRenderer>();
builder.Services.AddScoped<ITemplateDocxService, TemplateDocxService>();
builder.Services.AddScoped<IOfficePdfConverter, OfficePdfConverter>();
builder.Services.AddScoped<IBillingInvoicePdfRenderer, BillingInvoicePdfRenderer>();
builder.Services.AddScoped<IBillingFiscalService, BillingFiscalService>();
builder.Services.AddScoped<IEventEmailTemplateService, EventEmailTemplateService>();
builder.Services.AddScoped<IPayrollReceiptService, PayrollReceiptService>();
builder.Services.AddScoped<ITicketFlowService, TicketFlowService>();
builder.Services.AddScoped<IClientPortalAccessService, ClientPortalAccessService>();
builder.Services.AddScoped<IMercadoPagoService, MercadoPagoService>();
builder.Services.AddScoped<IPasswordHasher<ClientPortalAccess>, PasswordHasher<ClientPortalAccess>>();
builder.Services.AddHostedService<PayrollReceiptDispatchWorker>();
builder.Services.AddHostedService<CommercialReminderWorker>();

// QuestPDF licencia community
QuestPDF.Settings.License = LicenseType.Community;

var mx = new CultureInfo("es-MX");
CultureInfo.DefaultThreadCurrentCulture = mx;
CultureInfo.DefaultThreadCurrentUICulture = mx;

builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    o.DefaultRequestCulture = new RequestCulture(mx);
    o.SupportedCultures = new[] { mx };
    o.SupportedUICultures = new[] { mx };
});

var app = builder.Build();

// --------------------
// Migraciones + seed
// --------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var db = services.GetRequiredService<ApplicationDbContext>();
    await EnsureLegacyBrokenMigrationMarkedAsAppliedAsync(db);
    await db.Database.MigrateAsync();
    await EnsureQuoteSchemaAsync(db);
    await EnsureBillingSchemaAsync(db);
    await EnsureSecuritySchemaAsync(db);
    await EnsureOrgChartSchemaAsync(db);
    await EnsureProjectSchemaAsync(db);
    await EnsureProjectActivitySchemaAsync(db);
    await EnsureClientProspectsSchemaAsync(db);
    await EnsureSalesSchemaAsync(db);
    await EnsureSalesTelephonySchemaAsync(db);
    await EnsureSalesFeasibilitySchemaAsync(db);
    await EnsureClientPortalSchemaAsync(db);
    await EnsureSystemConfigurationSchemaAsync(db);

    await SeedRolesAndAdminAsync(services, app.Configuration);
    await EnsureEmployeeNumbersAsync(db);
    await SeedServiceOrderTemplates.EnsureAsync(db);

    // Eval360: si aún no ejecutaste el script de tablas, no tronamos el arranque.
    try
    {
        await SeedEval360.EnsureAsync(db);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "SeedEval360: tablas aún no existen o no accesibles (se omite).");
    }

}

static async Task EnsureLegacyBrokenMigrationMarkedAsAppliedAsync(ApplicationDbContext db)
{
    const string brokenMigrationId = "20260508201704_AddSalesProspectNotes";

    try
    {
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!pending.Contains(brokenMigrationId))
        {
            return;
        }

        await db.Database.OpenConnectionAsync();
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = """
SELECT EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name IN ('QuoteRequests', 'quoterequests')
      AND column_name IN ('ContractTermMonths', 'contracttermmonths')
)
""";
        var existsObj = await cmd.ExecuteScalarAsync();
        var quoteHasContractTermMonths = existsObj is bool b && b;

        if (!quoteHasContractTermMonths)
        {
            return;
        }

        cmd.CommandText = """
SELECT "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId" DESC
LIMIT 1
""";
        var pvObj = await cmd.ExecuteScalarAsync();
        var productVersion = pvObj as string;
        if (string.IsNullOrWhiteSpace(productVersion))
        {
            productVersion = "8.0.0";
        }

        cmd.Parameters.Clear();
        var pMigrationId = cmd.CreateParameter();
        pMigrationId.ParameterName = "@migrationId";
        pMigrationId.Value = brokenMigrationId;
        cmd.Parameters.Add(pMigrationId);

        var pProductVersion = cmd.CreateParameter();
        pProductVersion.ParameterName = "@productVersion";
        pProductVersion.Value = productVersion;
        cmd.Parameters.Add(pProductVersion);

        cmd.CommandText = """
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES (@migrationId, @productVersion)
ON CONFLICT ("MigrationId") DO NOTHING
""";
        await cmd.ExecuteNonQueryAsync();
    }
    catch
    {
        // Si no puede leer/escribir __EFMigrationsHistory, dejamos que el flujo normal lo reporte.
    }
    finally
    {
        await db.Database.CloseConnectionAsync();
    }
}

app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "UNHANDLED | TraceId={TraceId} Path={Path}", ctx.TraceIdentifier, ctx.Request.Path);
        throw;
    }
});

// --------------------
// Pipeline
// --------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

using (var scope = app.Services.CreateScope())

app.Run();

// --------------------
// Seed roles + admin + perfil
// --------------------
static async Task SeedRolesAndAdminAsync(IServiceProvider services, IConfiguration config)
{
    var roleMgr = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userMgr = services.GetRequiredService<UserManager<ApplicationUser>>();
    var db = services.GetRequiredService<ApplicationDbContext>();

    // Roles
    foreach (var r in new[] { AppRoles.Admin, AppRoles.SuperAdmin, AppRoles.Employee, AppRoles.Seller, AppRoles.InventoryManager, AppRoles.WarehouseLead })
    {
        if (!await roleMgr.RoleExistsAsync(r))
            await roleMgr.CreateAsync(new IdentityRole(r));
    }

    // Admin seed (forzamos correo canonical para evitar recrear legacy admin@hn.local en despliegues).
    var configuredAdminEmail = (config["SeedAdmin:Email"] ?? "").Trim();
    var adminEmail = string.IsNullOrWhiteSpace(configuredAdminEmail)
        || configuredAdminEmail.Equals("admin@hn.local", StringComparison.OrdinalIgnoreCase)
            ? "soporte@hubnet-solutions.net"
            : configuredAdminEmail;
    var adminPass = config["SeedAdmin:Password"] ?? "Admin123*Cambialo";

    var adminUser = await userMgr.FindByEmailAsync(adminEmail);
    var legacyAdminUser = await userMgr.FindByEmailAsync("admin@hn.local");
    if (adminUser == null
        && legacyAdminUser != null
        && !adminEmail.Equals("admin@hn.local", StringComparison.OrdinalIgnoreCase))
    {
        legacyAdminUser.UserName = adminEmail;
        legacyAdminUser.Email = adminEmail;
        legacyAdminUser.EmailConfirmed = true;
        var migrated = await userMgr.UpdateAsync(legacyAdminUser);
        if (!migrated.Succeeded)
        {
            var msg = string.Join("; ", migrated.Errors.Select(e => e.Description));
            throw new Exception("No se pudo migrar admin legacy a correo soporte: " + msg);
        }
        adminUser = legacyAdminUser;
    }

    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var created = await userMgr.CreateAsync(adminUser, adminPass);
        if (!created.Succeeded)
        {
            var msg = string.Join("; ", created.Errors.Select(e => e.Description));
            throw new Exception("No se pudo crear admin: " + msg);
        }
    }
    else if (!string.Equals(adminUser.Email, adminEmail, StringComparison.OrdinalIgnoreCase)
             || !string.Equals(adminUser.UserName, adminEmail, StringComparison.OrdinalIgnoreCase))
    {
        adminUser.Email = adminEmail;
        adminUser.UserName = adminEmail;
        adminUser.EmailConfirmed = true;
        var normalized = await userMgr.UpdateAsync(adminUser);
        if (!normalized.Succeeded)
        {
            var msg = string.Join("; ", normalized.Errors.Select(e => e.Description));
            throw new Exception("No se pudo normalizar admin seed: " + msg);
        }
    }

    if (!await userMgr.IsInRoleAsync(adminUser, AppRoles.Admin))
        await userMgr.AddToRoleAsync(adminUser, AppRoles.Admin);

    // Perfil admin (1:1)
    var adminProfile = await db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == adminUser.Id);
    if (adminProfile == null)
    {
        db.EmployeeProfiles.Add(new EmployeeProfile
        {
            UserId = adminUser.Id,
            FullName = "Administrador HN",
            Email = adminEmail,
            Position = "Administrador",
            Phone = "",
            Nss = "",
            Curp = "",
            Rfc = "",
            PostalCode = "",
            Address = "",
            BankName = "",
            BankAccount = "",
            BankClabe = "",
            Gender = "N/A",
            EducationLevel = "",
            EmployeeNumber = "",
            SatContractTypeCode = "",
            SatWorkdayTypeCode = "",
            SatJobRiskCode = "",
            SalaryBase = 0m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
    else
    {
        // Compatibilidad con esquemas existentes que tengan columnas NOT NULL.
        adminProfile.FullName ??= "Administrador HN";
        adminProfile.Email ??= adminEmail;
        adminProfile.Position ??= "Administrador";
        adminProfile.Phone ??= "";
        adminProfile.Nss ??= "";
        adminProfile.Curp ??= "";
        adminProfile.Rfc ??= "";
        adminProfile.PostalCode ??= "";
        adminProfile.Address ??= "";
        adminProfile.BankName ??= "";
        adminProfile.BankAccount ??= "";
        adminProfile.BankClabe ??= "";
        adminProfile.Gender ??= "N/A";
        adminProfile.EducationLevel ??= "";
        adminProfile.EmployeeNumber ??= "";
        adminProfile.SatContractTypeCode ??= "";
        adminProfile.SatWorkdayTypeCode ??= "";
        adminProfile.SatJobRiskCode ??= "";
        adminProfile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    // Permisos de módulos (rol default + asignación automática)
    await SeedModulePermissions.EnsureAsync(db, userMgr);
}

static async Task EnsureQuoteSchemaAsync(ApplicationDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
ALTER TABLE IF EXISTS public."QuoteRequests"
    ADD COLUMN IF NOT EXISTS "GeneralTerms" character varying(4000);
ALTER TABLE IF EXISTS public."QuoteRequests"
    ADD COLUMN IF NOT EXISTS "ContractTermMonths" integer;

ALTER TABLE IF EXISTS public.quoterequests
    ADD COLUMN IF NOT EXISTS "GeneralTerms" character varying(4000);
ALTER TABLE IF EXISTS public.quoterequests
    ADD COLUMN IF NOT EXISTS "ContractTermMonths" integer;

ALTER TABLE IF EXISTS public."QuoteRequestLines"
    ADD COLUMN IF NOT EXISTS "Recurrence" character varying(30);
ALTER TABLE IF EXISTS public.quoterequestlines
    ADD COLUMN IF NOT EXISTS "Recurrence" character varying(30);
""");
}

static async Task EnsureBillingSchemaAsync(ApplicationDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
ALTER TABLE IF EXISTS public."BillingInvoicePlans"
    ADD COLUMN IF NOT EXISTS "ClientServiceContractId" uuid;
ALTER TABLE IF EXISTS public."BillingInvoicePlans"
    ADD COLUMN IF NOT EXISTS "InvoiceIssueDate" date;

ALTER TABLE IF EXISTS public.billinginvoiceplans
    ADD COLUMN IF NOT EXISTS "ClientServiceContractId" uuid;
ALTER TABLE IF EXISTS public.billinginvoiceplans
    ADD COLUMN IF NOT EXISTS "InvoiceIssueDate" date;

ALTER TABLE IF EXISTS public."BillingInvoiceRuns"
    ADD COLUMN IF NOT EXISTS "IsPaid" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE IF EXISTS public."BillingInvoiceRuns"
    ADD COLUMN IF NOT EXISTS "PaidAt" date;
ALTER TABLE IF EXISTS public."BillingInvoiceRuns"
    ADD COLUMN IF NOT EXISTS "PaymentFormCode" character varying(4) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."BillingInvoiceRuns"
    ADD COLUMN IF NOT EXISTS "PaymentMethodCode" character varying(4) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."BillingInvoiceRuns"
    ADD COLUMN IF NOT EXISTS "PaymentNotes" character varying(1200) NOT NULL DEFAULT '';

ALTER TABLE IF EXISTS public.billinginvoiceruns
    ADD COLUMN IF NOT EXISTS "IsPaid" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE IF EXISTS public.billinginvoiceruns
    ADD COLUMN IF NOT EXISTS "PaidAt" date;
ALTER TABLE IF EXISTS public.billinginvoiceruns
    ADD COLUMN IF NOT EXISTS "PaymentFormCode" character varying(4) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public.billinginvoiceruns
    ADD COLUMN IF NOT EXISTS "PaymentMethodCode" character varying(4) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public.billinginvoiceruns
    ADD COLUMN IF NOT EXISTS "PaymentNotes" character varying(1200) NOT NULL DEFAULT '';

CREATE TABLE IF NOT EXISTS public."BillingInvoiceLines" (
    "Id" uuid NOT NULL,
    "BillingInvoicePlanId" uuid NOT NULL,
    "Concept" character varying(220) NOT NULL DEFAULT '',
    "Category" character varying(80) NOT NULL DEFAULT '',
    "Quantity" integer NOT NULL DEFAULT 1,
    "UnitPrice" numeric(12,2) NOT NULL DEFAULT 0,
    "Subtotal" numeric(12,2) NOT NULL DEFAULT 0,
    "VatRate" numeric(7,5) NOT NULL DEFAULT 0.16,
    "VatAmount" numeric(12,2) NOT NULL DEFAULT 0,
    "Total" numeric(12,2) NOT NULL DEFAULT 0,
    "SortOrder" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_BillingInvoiceLines" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_BillingInvoiceLines_BillingInvoicePlans_BillingInvoicePlanId"
        FOREIGN KEY ("BillingInvoicePlanId") REFERENCES public."BillingInvoicePlans" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_BillingInvoiceLines_BillingInvoicePlanId_SortOrder"
    ON public."BillingInvoiceLines" ("BillingInvoicePlanId", "SortOrder");

UPDATE public."BillingInvoicePlans"
SET "InvoiceIssueDate" = COALESCE("InvoiceIssueDate", "NextRunDate", "StartDate", CURRENT_DATE)
WHERE "InvoiceIssueDate" IS NULL;
""");
    }
    catch (PostgresException ex) when (ex.SqlState == "42501")
    {
        Console.WriteLine($"[WARN] EnsureBillingSchemaAsync omitido por permisos (owner requerido): {ex.MessageText}");
    }
}

static async Task EnsureSecuritySchemaAsync(ApplicationDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS public."LoginTwoFactorChallenges" (
    "Id" uuid NOT NULL,
    "UserId" character varying(64) NOT NULL,
    "UserEmail" character varying(180) NOT NULL,
    "IpAddress" character varying(64) NOT NULL,
    "CodeHash" character varying(120) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    "ExpiresAt" timestamp with time zone NOT NULL,
    "UsedAt" timestamp with time zone NULL,
    "FailedAttempts" integer NOT NULL DEFAULT 0,
    CONSTRAINT "PK_LoginTwoFactorChallenges" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_LoginTwoFactorChallenges_UserId_IpAddress_CreatedAt"
    ON public."LoginTwoFactorChallenges" ("UserId", "IpAddress", "CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_LoginTwoFactorChallenges_ExpiresAt"
    ON public."LoginTwoFactorChallenges" ("ExpiresAt");
""");
    }
    catch (PostgresException ex) when (ex.SqlState == "42501")
    {
        Console.WriteLine($"[WARN] EnsureSecuritySchemaAsync omitido por permisos (owner requerido): {ex.MessageText}");
    }
}

static async Task EnsureOrgChartSchemaAsync(ApplicationDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS public."EmployeeOrgChartNodes" (
    "Id" uuid NOT NULL,
    "UserId" character varying(64) NOT NULL,
    "ReportsToUserId" character varying(64) NULL,
    "SortOrder" integer NOT NULL DEFAULT 0,
    "PositionX" integer NOT NULL DEFAULT 0,
    "PositionY" integer NOT NULL DEFAULT 0,
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    "UpdatedByUserId" character varying(64) NULL,
    CONSTRAINT "PK_EmployeeOrgChartNodes" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmployeeOrgChartNodes_UserId"
    ON public."EmployeeOrgChartNodes" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_EmployeeOrgChartNodes_ReportsToUserId_SortOrder"
    ON public."EmployeeOrgChartNodes" ("ReportsToUserId", "SortOrder");

ALTER TABLE IF EXISTS public."EmployeeOrgChartNodes"
    ADD COLUMN IF NOT EXISTS "PositionX" integer NOT NULL DEFAULT 0;
ALTER TABLE IF EXISTS public."EmployeeOrgChartNodes"
    ADD COLUMN IF NOT EXISTS "PositionY" integer NOT NULL DEFAULT 0;
""");
    }
    catch (PostgresException ex) when (ex.SqlState == "42501")
    {
        Console.WriteLine($"[WARN] EnsureOrgChartSchemaAsync omitido por permisos (owner requerido): {ex.MessageText}");
    }
}

static async Task EnsureProjectSchemaAsync(ApplicationDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
ALTER TABLE IF EXISTS public."Projects"
    ADD COLUMN IF NOT EXISTS "Objective" character varying(400) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."Projects"
    ADD COLUMN IF NOT EXISTS "Scope" character varying(1200) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."Projects"
    ADD COLUMN IF NOT EXISTS "ActivityDescription" character varying(4000) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."Projects"
    ADD COLUMN IF NOT EXISTS "AdditionalComments" character varying(2000) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."Projects"
    ADD COLUMN IF NOT EXISTS "AccessNotes" character varying(8000) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."Projects"
    ADD COLUMN IF NOT EXISTS "ClosedAt" timestamp with time zone;
ALTER TABLE IF EXISTS public."Projects"
    ADD COLUMN IF NOT EXISTS "ClosedByUserId" character varying(64);
ALTER TABLE IF EXISTS public."Projects"
    ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW();
ALTER TABLE IF EXISTS public."Projects"
    ADD COLUMN IF NOT EXISTS "Status" integer NOT NULL DEFAULT 1;
ALTER TABLE IF EXISTS public."Projects"
    ADD COLUMN IF NOT EXISTS "EstimatedEndDate" timestamp with time zone NOT NULL DEFAULT NOW();

UPDATE public."Projects"
SET "Objective" = COALESCE("Objective", ''),
    "Scope" = COALESCE("Scope", ''),
    "ActivityDescription" = COALESCE("ActivityDescription", ''),
    "AdditionalComments" = COALESCE("AdditionalComments", ''),
    "AccessNotes" = COALESCE("AccessNotes", ''),
    "UpdatedAt" = COALESCE("UpdatedAt", NOW()),
    "Status" = COALESCE("Status", 1),
    "EstimatedEndDate" = COALESCE("EstimatedEndDate", "StartDate", NOW());
""");
    }
    catch (PostgresException ex) when (ex.SqlState == "42501")
    {
        Console.WriteLine($"[WARN] EnsureProjectSchemaAsync omitido por permisos (owner requerido): {ex.MessageText}");
    }
}

static async Task EnsureProjectActivitySchemaAsync(ApplicationDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'ProjectActivities'
          AND column_name = 'assignedtouserid'
    ) THEN
        EXECUTE 'ALTER TABLE public."ProjectActivities" RENAME COLUMN assignedtouserid TO "AssignedToUserId"';
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'projectactivities'
          AND column_name = 'assignedtouserid'
    ) THEN
        EXECUTE 'ALTER TABLE public.projectactivities RENAME COLUMN assignedtouserid TO "AssignedToUserId"';
    END IF;
END $$;

ALTER TABLE IF EXISTS public."ProjectActivities"
    ADD COLUMN IF NOT EXISTS "AssignedToUserId" character varying(64);
ALTER TABLE IF EXISTS public."ProjectActivities"
    ADD COLUMN IF NOT EXISTS "DurationUnit" character varying(16) NOT NULL DEFAULT 'hours';
ALTER TABLE IF EXISTS public."ProjectActivities"
    ADD COLUMN IF NOT EXISTS "DurationValue" integer NOT NULL DEFAULT 1;
ALTER TABLE IF EXISTS public."ProjectActivities"
    ADD COLUMN IF NOT EXISTS "StartAtUtc" timestamp with time zone;
ALTER TABLE IF EXISTS public."ProjectActivities"
    ADD COLUMN IF NOT EXISTS "EndAtUtc" timestamp with time zone;
ALTER TABLE IF EXISTS public."ProjectActivities"
    ADD COLUMN IF NOT EXISTS "ColorHex" character varying(16);
ALTER TABLE IF EXISTS public."ProjectActivities"
    ADD COLUMN IF NOT EXISTS "IsCompleted" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE IF EXISTS public."ProjectActivities"
    ADD COLUMN IF NOT EXISTS "CompletedAtUtc" timestamp with time zone;

ALTER TABLE IF EXISTS public.projectactivities
    ADD COLUMN IF NOT EXISTS "AssignedToUserId" character varying(64);
ALTER TABLE IF EXISTS public.projectactivities
    ADD COLUMN IF NOT EXISTS "DurationUnit" character varying(16) NOT NULL DEFAULT 'hours';
ALTER TABLE IF EXISTS public.projectactivities
    ADD COLUMN IF NOT EXISTS "DurationValue" integer NOT NULL DEFAULT 1;
ALTER TABLE IF EXISTS public.projectactivities
    ADD COLUMN IF NOT EXISTS "StartAtUtc" timestamp with time zone;
ALTER TABLE IF EXISTS public.projectactivities
    ADD COLUMN IF NOT EXISTS "EndAtUtc" timestamp with time zone;
ALTER TABLE IF EXISTS public.projectactivities
    ADD COLUMN IF NOT EXISTS "ColorHex" character varying(16);
ALTER TABLE IF EXISTS public.projectactivities
    ADD COLUMN IF NOT EXISTS "IsCompleted" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE IF EXISTS public.projectactivities
    ADD COLUMN IF NOT EXISTS "CompletedAtUtc" timestamp with time zone;

UPDATE public."ProjectActivities"
SET "DurationUnit" = COALESCE(NULLIF("DurationUnit", ''), 'hours'),
    "DurationValue" = CASE
        WHEN "DurationValue" IS NULL OR "DurationValue" < 1
            THEN GREATEST(1, COALESCE("PlannedDays", 1))
        ELSE "DurationValue"
    END
WHERE "DurationUnit" IS NULL OR "DurationUnit" = '' OR "DurationValue" IS NULL OR "DurationValue" < 1;
""");
    }
    catch (PostgresException ex) when (ex.SqlState == "42501")
    {
        Console.WriteLine($"[WARN] EnsureProjectActivitySchemaAsync omitido por permisos (owner requerido): {ex.MessageText}");
    }
}

static async Task EnsureClientProspectsSchemaAsync(ApplicationDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
ALTER TABLE IF EXISTS public."Clients"
    ADD COLUMN IF NOT EXISTS "CreatedByUserId" character varying(64);
ALTER TABLE IF EXISTS public."Clients"
    ADD COLUMN IF NOT EXISTS "State" character varying(80);
ALTER TABLE IF EXISTS public."Clients"
    ADD COLUMN IF NOT EXISTS "Municipality" character varying(120);
ALTER TABLE IF EXISTS public."Clients"
    ADD COLUMN IF NOT EXISTS "OwnerUserId" character varying(64);
ALTER TABLE IF EXISTS public."Clients"
    ADD COLUMN IF NOT EXISTS "Rfc" character varying(13);
ALTER TABLE IF EXISTS public.clients
    ADD COLUMN IF NOT EXISTS "CreatedByUserId" character varying(64);
ALTER TABLE IF EXISTS public.clients
    ADD COLUMN IF NOT EXISTS "State" character varying(80);
ALTER TABLE IF EXISTS public.clients
    ADD COLUMN IF NOT EXISTS "Municipality" character varying(120);
ALTER TABLE IF EXISTS public.clients
    ADD COLUMN IF NOT EXISTS "OwnerUserId" character varying(64);
ALTER TABLE IF EXISTS public.clients
    ADD COLUMN IF NOT EXISTS "Rfc" character varying(13);

CREATE INDEX IF NOT EXISTS "IX_Clients_IsTemporaryLead_CreatedByUserId_CreatedAt"
    ON public."Clients" ("IsTemporaryLead", "CreatedByUserId", "CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_Clients_IsTemporaryLead_OwnerUserId_CreatedAt"
    ON public."Clients" ("IsTemporaryLead", "OwnerUserId", "CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_Clients_Rfc"
    ON public."Clients" ("Rfc");

UPDATE public."Clients"
SET "OwnerUserId" = "CreatedByUserId"
WHERE COALESCE("OwnerUserId", '') = '' AND COALESCE("CreatedByUserId", '') <> '';
""");
    }
    catch (PostgresException ex) when (ex.SqlState == "42501")
    {
        Console.WriteLine($"[WARN] EnsureClientProspectsSchemaAsync omitido por permisos (owner requerido): {ex.MessageText}");
    }
}

static async Task EnsureSalesSchemaAsync(ApplicationDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
ALTER TABLE IF EXISTS public."SalesAuditLogs"
    ALTER COLUMN "Details" TYPE character varying(2000);
ALTER TABLE IF EXISTS public.salesauditlogs
    ALTER COLUMN "Details" TYPE character varying(2000);

CREATE TABLE IF NOT EXISTS public."SalesProspectNotes" (
    "Id" uuid NOT NULL,
    "ClientId" uuid NOT NULL,
    "UserId" character varying(64) NULL,
    "UserName" character varying(160) NOT NULL DEFAULT '',
    "Note" character varying(2000) NOT NULL DEFAULT '',
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_SalesProspectNotes" PRIMARY KEY ("Id")
);

ALTER TABLE IF EXISTS public."SalesProspectNotes"
    ADD COLUMN IF NOT EXISTS "ClientId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE IF EXISTS public."SalesProspectNotes"
    ADD COLUMN IF NOT EXISTS "UserId" character varying(64);
ALTER TABLE IF EXISTS public."SalesProspectNotes"
    ADD COLUMN IF NOT EXISTS "UserName" character varying(160) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SalesProspectNotes"
    ADD COLUMN IF NOT EXISTS "Note" character varying(2000) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SalesProspectNotes"
    ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW();

CREATE INDEX IF NOT EXISTS "IX_SalesProspectNotes_ClientId_CreatedAt"
    ON public."SalesProspectNotes" ("ClientId", "CreatedAt");
""");
    }
    catch (PostgresException ex) when (ex.SqlState == "42501")
    {
        Console.WriteLine($"[WARN] EnsureSalesSchemaAsync omitido por permisos (owner requerido): {ex.MessageText}");
    }
}

static async Task EnsureSalesTelephonySchemaAsync(ApplicationDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS public."SalesSipAccounts" (
    "Id" uuid NOT NULL,
    "UserId" character varying(64) NOT NULL,
    "Host" character varying(220) NOT NULL DEFAULT '',
    "WsUrl" character varying(300) NOT NULL DEFAULT '',
    "SipDomain" character varying(180) NOT NULL DEFAULT '',
    "SipUser" character varying(180) NOT NULL DEFAULT '',
    "AuthUser" character varying(180) NOT NULL DEFAULT '',
    "SipPasswordProtected" character varying(2000) NOT NULL DEFAULT '',
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_SalesSipAccounts" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SalesSipAccounts_UserId"
    ON public."SalesSipAccounts" ("UserId");

CREATE TABLE IF NOT EXISTS public."SalesCallLogs" (
    "Id" uuid NOT NULL,
    "UserId" character varying(64) NOT NULL,
    "SalesOpportunityId" uuid NULL,
    "DialedNumber" character varying(60) NOT NULL DEFAULT '',
    "Result" integer NOT NULL DEFAULT 1,
    "DurationSeconds" integer NOT NULL DEFAULT 0,
    "Notes" character varying(2000) NOT NULL DEFAULT '',
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_SalesCallLogs" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_SalesCallLogs_UserId_CreatedAt"
    ON public."SalesCallLogs" ("UserId", "CreatedAt");

CREATE INDEX IF NOT EXISTS "IX_SalesCallLogs_SalesOpportunityId_CreatedAt"
    ON public."SalesCallLogs" ("SalesOpportunityId", "CreatedAt");

ALTER TABLE IF EXISTS public."SalesCallLogs"
    ADD COLUMN IF NOT EXISTS "UserId" character varying(64) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SalesCallLogs"
    ADD COLUMN IF NOT EXISTS "EmployeeUserId" character varying(64);
ALTER TABLE IF EXISTS public."SalesCallLogs"
    ADD COLUMN IF NOT EXISTS "SalesOpportunityId" uuid;
ALTER TABLE IF EXISTS public."SalesCallLogs"
    ADD COLUMN IF NOT EXISTS "DialedNumber" character varying(60) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SalesCallLogs"
    ADD COLUMN IF NOT EXISTS "Result" integer NOT NULL DEFAULT 1;
ALTER TABLE IF EXISTS public."SalesCallLogs"
    ADD COLUMN IF NOT EXISTS "DurationSeconds" integer NOT NULL DEFAULT 0;
ALTER TABLE IF EXISTS public."SalesCallLogs"
    ADD COLUMN IF NOT EXISTS "Notes" character varying(2000) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SalesCallLogs"
    ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW();

ALTER TABLE IF EXISTS public."SalesSipAccounts"
    ADD COLUMN IF NOT EXISTS "UserId" character varying(64) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SalesSipAccounts"
    ADD COLUMN IF NOT EXISTS "EmployeeUserId" character varying(64);
ALTER TABLE IF EXISTS public."SalesSipAccounts"
    ADD COLUMN IF NOT EXISTS "Host" character varying(220) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SalesSipAccounts"
    ADD COLUMN IF NOT EXISTS "WsUrl" character varying(300) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SalesSipAccounts"
    ADD COLUMN IF NOT EXISTS "SipDomain" character varying(180) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SalesSipAccounts"
    ADD COLUMN IF NOT EXISTS "SipUser" character varying(180) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SalesSipAccounts"
    ADD COLUMN IF NOT EXISTS "AuthUser" character varying(180) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SalesSipAccounts"
    ADD COLUMN IF NOT EXISTS "SipPasswordProtected" character varying(2000) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SalesSipAccounts"
    ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT TRUE;
ALTER TABLE IF EXISTS public."SalesSipAccounts"
    ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW();

UPDATE public."SalesSipAccounts"
SET "EmployeeUserId" = "UserId"
WHERE COALESCE("EmployeeUserId", '') = '' AND COALESCE("UserId", '') <> '';

UPDATE public."SalesCallLogs"
SET "EmployeeUserId" = "UserId"
WHERE COALESCE("EmployeeUserId", '') = '' AND COALESCE("UserId", '') <> '';
""");
    }
    catch (PostgresException ex) when (ex.SqlState == "42501")
    {
        Console.WriteLine($"[WARN] EnsureSalesTelephonySchemaAsync omitido por permisos (owner requerido): {ex.MessageText}");
    }
}

static async Task EnsureSalesFeasibilitySchemaAsync(ApplicationDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS public."ServiceFeasibilities" (
    "Id" uuid NOT NULL,
    "ClientId" uuid NOT NULL,
    "ProjectId" uuid NULL,
    "Title" character varying(200) NOT NULL DEFAULT '',
    "SiteAddress" character varying(400) NOT NULL DEFAULT '',
    "Coordinates" character varying(64) NULL,
    "SiteContactName" character varying(160) NOT NULL DEFAULT '',
    "SiteContactPhone" character varying(60) NOT NULL DEFAULT '',
    "Notes" character varying(2000) NOT NULL DEFAULT '',
    "Status" integer NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    "AcceptedAt" timestamp with time zone NULL,
    "CreatedByUserId" character varying(64) NULL,
    "ConvertedServiceOrderId" uuid NULL,
    CONSTRAINT "PK_ServiceFeasibilities" PRIMARY KEY ("Id")
);

ALTER TABLE IF EXISTS public."ServiceFeasibilities"
    ADD COLUMN IF NOT EXISTS "ProjectId" uuid NULL;
ALTER TABLE IF EXISTS public."ServiceFeasibilities"
    ADD COLUMN IF NOT EXISTS "Coordinates" character varying(64) NULL;
ALTER TABLE IF EXISTS public."ServiceFeasibilities"
    ADD COLUMN IF NOT EXISTS "AcceptedAt" timestamp with time zone NULL;
ALTER TABLE IF EXISTS public."ServiceFeasibilities"
    ADD COLUMN IF NOT EXISTS "CreatedByUserId" character varying(64) NULL;
ALTER TABLE IF EXISTS public."ServiceFeasibilities"
    ADD COLUMN IF NOT EXISTS "ConvertedServiceOrderId" uuid NULL;

CREATE INDEX IF NOT EXISTS "IX_ServiceFeasibilities_ClientId_Status_CreatedAt"
    ON public."ServiceFeasibilities" ("ClientId", "Status", "CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_ServiceFeasibilities_ConvertedServiceOrderId"
    ON public."ServiceFeasibilities" ("ConvertedServiceOrderId");
""");
    }
    catch (PostgresException ex) when (ex.SqlState == "42501")
    {
        Console.WriteLine($"[WARN] EnsureSalesFeasibilitySchemaAsync omitido por permisos (owner requerido): {ex.MessageText}");
    }
}

static async Task EnsureClientPortalSchemaAsync(ApplicationDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS public."ClientPortalAccesses" (
    "Id" uuid NOT NULL,
    "ClientId" uuid NOT NULL,
    "Username" character varying(40) NOT NULL DEFAULT '',
    "PasswordHash" character varying(512) NOT NULL DEFAULT '',
    "PasswordProtected" character varying(4000) NOT NULL DEFAULT '',
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "LastLoginAt" timestamp with time zone NULL,
    "UpdatedByUserId" character varying(64) NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_ClientPortalAccesses" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ClientPortalAccesses_ClientId"
    ON public."ClientPortalAccesses" ("ClientId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ClientPortalAccesses_Username"
    ON public."ClientPortalAccesses" ("Username");

CREATE TABLE IF NOT EXISTS public."ClientCardDomiciliations" (
    "Id" uuid NOT NULL,
    "ClientId" uuid NOT NULL,
    "MercadoPagoPreferenceId" character varying(120) NOT NULL DEFAULT '',
    "MercadoPagoExternalReference" character varying(120) NOT NULL DEFAULT '',
    "InitPointUrl" character varying(500) NOT NULL DEFAULT '',
    "ReferenceAmount" numeric(12,2) NOT NULL DEFAULT 0,
    "Status" integer NOT NULL DEFAULT 2,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_ClientCardDomiciliations" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_ClientCardDomiciliations_ClientId_CreatedAt"
    ON public."ClientCardDomiciliations" ("ClientId", "CreatedAt");
""");
    }
    catch (PostgresException ex) when (ex.SqlState == "42501")
    {
        Console.WriteLine($"[WARN] EnsureClientPortalSchemaAsync omitido por permisos (owner requerido): {ex.MessageText}");
    }
}

static async Task EnsureSystemConfigurationSchemaAsync(ApplicationDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "MercadoPagoAccessTokenProtected" character varying(2200) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "MercadoPagoPublicKey" character varying(220) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "MercadoPagoWebhookSecretProtected" character varying(2200) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "PublicBaseUrl" character varying(220) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppEnabled" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppGatewayUrl" character varying(300) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppApiKeyProtected" character varying(2200) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppInternalPhonesCsv" character varying(1000) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppNotifyTickets" boolean NOT NULL DEFAULT TRUE;
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppNotifyCustomers" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppOtpTemplate" character varying(2000) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppPayrollReceiptTemplate" character varying(2000) NOT NULL DEFAULT '';

ALTER TABLE IF EXISTS public.systemconfigurations
    ADD COLUMN IF NOT EXISTS "MercadoPagoAccessTokenProtected" character varying(2200) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public.systemconfigurations
    ADD COLUMN IF NOT EXISTS "MercadoPagoPublicKey" character varying(220) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public.systemconfigurations
    ADD COLUMN IF NOT EXISTS "MercadoPagoWebhookSecretProtected" character varying(2200) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public.systemconfigurations
    ADD COLUMN IF NOT EXISTS "PublicBaseUrl" character varying(220) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public.systemconfigurations
    ADD COLUMN IF NOT EXISTS "WhatsAppEnabled" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE IF EXISTS public.systemconfigurations
    ADD COLUMN IF NOT EXISTS "WhatsAppGatewayUrl" character varying(300) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public.systemconfigurations
    ADD COLUMN IF NOT EXISTS "WhatsAppApiKeyProtected" character varying(2200) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public.systemconfigurations
    ADD COLUMN IF NOT EXISTS "WhatsAppInternalPhonesCsv" character varying(1000) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public.systemconfigurations
    ADD COLUMN IF NOT EXISTS "WhatsAppNotifyTickets" boolean NOT NULL DEFAULT TRUE;
ALTER TABLE IF EXISTS public.systemconfigurations
    ADD COLUMN IF NOT EXISTS "WhatsAppNotifyCustomers" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE IF EXISTS public.systemconfigurations
    ADD COLUMN IF NOT EXISTS "WhatsAppOtpTemplate" character varying(2000) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public.systemconfigurations
    ADD COLUMN IF NOT EXISTS "WhatsAppPayrollReceiptTemplate" character varying(2000) NOT NULL DEFAULT '';

-- Meta WhatsApp Cloud API
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppWabaId" character varying(64) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppGraphApiVersion" character varying(12) NOT NULL DEFAULT 'v21.0';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppTemplateLanguage" character varying(12) NOT NULL DEFAULT 'es_MX';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppWebhookVerifyToken" character varying(120) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppOtpTemplateName" character varying(200) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppPayrollTemplateName" character varying(200) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppTicketTemplateName" character varying(200) NOT NULL DEFAULT '';
""");
    }
    catch (PostgresException ex) when (ex.SqlState == "42501")
    {
        Console.WriteLine($"[WARN] EnsureSystemConfigurationSchemaAsync omitido por permisos (owner requerido): {ex.MessageText}");
    }
}

static async Task EnsureEmployeeNumbersAsync(ApplicationDbContext db)
{
    const string Prefix = "HN-NOM-5";

    var employees = await db.EmployeeProfiles
        .OrderBy(x => x.CreatedAt)
        .ThenBy(x => x.UserId)
        .ToListAsync();

    if (employees.Count == 0) return;

    var used = new HashSet<int>();
    foreach (var e in employees)
    {
        if (TryParseEmployeeSequence(e.EmployeeNumber, out var seq) && seq > 0)
            used.Add(seq);
    }

    var next = used.Count == 0 ? 1 : used.Max() + 1;
    var changed = false;

    foreach (var e in employees)
    {
        if (TryParseEmployeeSequence(e.EmployeeNumber, out var seq) && seq > 0)
        {
            var normalized = $"{Prefix}{seq:000}";
            if (!string.Equals(e.EmployeeNumber, normalized, StringComparison.Ordinal))
            {
                e.EmployeeNumber = normalized;
                e.UpdatedAt = DateTime.UtcNow;
                changed = true;
            }
            continue;
        }

        while (used.Contains(next)) next++;
        e.EmployeeNumber = $"{Prefix}{next:000}";
        e.UpdatedAt = DateTime.UtcNow;
        used.Add(next);
        next++;
        changed = true;
    }

    if (changed)
        await db.SaveChangesAsync();

    static bool TryParseEmployeeSequence(string? value, out int sequence)
    {
        sequence = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var v = value.Trim().ToUpperInvariant();

        if (v.StartsWith("HN-NOM-5", StringComparison.Ordinal))
        {
            var suffix = v["HN-NOM-5".Length..];
            if (int.TryParse(suffix, out var parsed) && parsed > 0)
            {
                sequence = parsed;
                return true;
            }
        }

        if (v.StartsWith("ID-", StringComparison.Ordinal) || v.StartsWith("HN-", StringComparison.Ordinal))
        {
            var candidate = v[3..];
            if (int.TryParse(candidate, out var parsed) && parsed > 0)
            {
                sequence = parsed;
                return true;
            }
        }

        var digits = Regex.Replace(v, @"\D", "");
        if (digits.Length > 0 && int.TryParse(digits, out var fallback) && fallback > 0)
        {
            sequence = fallback;
            return true;
        }

        return false;
    }
}
