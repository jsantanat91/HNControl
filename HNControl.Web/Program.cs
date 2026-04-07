using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using HNControl.Web.Services.Monitoring;
using HNControl.Web.Services.Mobile;
using HNControl.Web.Services.Tickets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using System.Text;
using Npgsql;

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
    options.Conventions.AddPageRoute("/Admin/Quotes/Requests", "/Ventas/Cotizaciones");
    options.Conventions.AddPageRoute("/Projects/Sales/Index", "/Ventas/Gestion");
    options.Conventions.AddPageRoute("/Projects/Billing/Index", "/Facturacion");
    options.Conventions.AddPageRoute("/Employees/OrgChart", "/Employees/Organigrama");
})
.AddMvcOptions(o => o.Filters.AddService<ModulePermissionPageFilter>());

// --------------------
// Servicios (Storage / Secrets / Email / PDF)
// --------------------
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();

// DataProtection para cifrar accesos de proyectos (passwords)
builder.Services.AddDataProtection();
builder.Services.AddScoped<ISecretProtector, SecretProtector>();

// EMAIL: evitamos ambigüedad con Identity.UI IEmailSender
builder.Services.AddScoped<HNControl.Web.Services.IEmailSender, HNControl.Web.Services.SmtpEmailSender>();

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
    await db.Database.MigrateAsync();
    await EnsureQuoteSchemaAsync(db);
    await EnsureBillingSchemaAsync(db);
    await EnsureSecuritySchemaAsync(db);
    await EnsureOrgChartSchemaAsync(db);
    await EnsureProjectActivitySchemaAsync(db);
    await EnsureClientProspectsSchemaAsync(db);
    await EnsureSalesSchemaAsync(db);

    await SeedRolesAndAdminAsync(services, app.Configuration);
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
ALTER TABLE IF EXISTS public.clients
    ADD COLUMN IF NOT EXISTS "CreatedByUserId" character varying(64);
ALTER TABLE IF EXISTS public.clients
    ADD COLUMN IF NOT EXISTS "State" character varying(80);
ALTER TABLE IF EXISTS public.clients
    ADD COLUMN IF NOT EXISTS "Municipality" character varying(120);

CREATE INDEX IF NOT EXISTS "IX_Clients_IsTemporaryLead_CreatedByUserId_CreatedAt"
    ON public."Clients" ("IsTemporaryLead", "CreatedByUserId", "CreatedAt");
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
""");
    }
    catch (PostgresException ex) when (ex.SqlState == "42501")
    {
        Console.WriteLine($"[WARN] EnsureSalesSchemaAsync omitido por permisos (owner requerido): {ex.MessageText}");
    }
}
