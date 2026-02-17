using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

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

// --------------------
// Authorization policies
// --------------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(AppRoles.Admin));
    options.AddPolicy("EmployeeOnly", policy => policy.RequireRole(AppRoles.Employee, AppRoles.Admin));
});

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

    // Clientes: solo admin
    options.Conventions.AuthorizeFolder("/Clients", "AdminOnly");

    // Proyectos: admin + empleado
    options.Conventions.AuthorizeFolder("/Projects", "EmployeeOnly");

    // Documentos: admin + empleado
    options.Conventions.AuthorizeFolder("/Knowledge", "EmployeeOnly");

    // Empleados: admin gestiona, empleado ve su ficha
    options.Conventions.AuthorizeFolder("/Employees", "AdminOnly");
    options.Conventions.AuthorizePage("/Employees/MyProfile", "EmployeeOnly");

    // Viáticos: empleados y admin
    options.Conventions.AuthorizeFolder("/Viaticos", "EmployeeOnly");
});

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

// QuestPDF licencia community
QuestPDF.Settings.License = LicenseType.Community;

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
}

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

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
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
    foreach (var r in new[] { AppRoles.Admin, AppRoles.Employee })
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
}
