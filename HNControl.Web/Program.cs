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


    // Clientes: solo admin
    options.Conventions.AuthorizeFolder("/Clients", "AdminOnly");

    // Proyectos: admin + empleado
    options.Conventions.AuthorizeFolder("/Projects", "EmployeeOnly");
    options.Conventions.AuthorizeFolder("/Sales", "EmployeeOnly");

    // Documentos: admin + empleado
    options.Conventions.AuthorizeFolder("/Knowledge", "EmployeeOnly");

    // Empleados: admin gestiona en /Admin/Employees, el empleado ve su ficha en /Employees/MyProfile
    // Importante: NO autorizar toda la carpeta /Employees como AdminOnly.
    // En Razor Pages las convenciones se acumulan y terminarías exigiendo Admin + Employee a la vez.
    options.Conventions.AuthorizePage("/Employees/MyProfile", "EmployeeOnly");

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
builder.Services.AddScoped<IBillingInvoicePdfRenderer, BillingInvoicePdfRenderer>();
builder.Services.AddScoped<IPayrollReceiptService, PayrollReceiptService>();
builder.Services.AddScoped<ITicketFlowService, TicketFlowService>();
builder.Services.AddHostedService<PayrollReceiptDispatchWorker>();

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

    // Admin seed
    var adminEmail = config["SeedAdmin:Email"] ?? "admin@hn.local";
    var adminPass = config["SeedAdmin:Password"] ?? "Admin123*Cambialo";

    var adminUser = await userMgr.FindByEmailAsync(adminEmail);
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

    if (!await userMgr.IsInRoleAsync(adminUser, AppRoles.Admin))
        await userMgr.AddToRoleAsync(adminUser, AppRoles.Admin);

    // Perfil admin (1:1)
    var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == adminUser.Id);
    if (profile == null)
    {
        db.EmployeeProfiles.Add(new EmployeeProfile
        {
            UserId = adminUser.Id,
            FullName = "Administrador HN",
            Email = adminEmail,
            Position = "Administrador",
            Phone = "",
            Nss = "",
            Gender = "N/A",
            SalaryBase = 0m,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    // Permisos de módulos (rol default + asignación automática)
    await SeedModulePermissions.EnsureAsync(db, userMgr);
}
