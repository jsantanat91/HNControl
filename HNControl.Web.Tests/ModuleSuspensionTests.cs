using System.Security.Claims;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using HNControl.Web.Services.Monitoring;
using HNControl.Web.TagHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

internal static class ModuleSuspensionTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var features = new ModuleAvailability(config);
        var called = 0;
        var middleware = new ModuleSuspensionMiddleware(_ => { called++; return Task.CompletedTask; });
        var blocked = new[] { "/Knowledge", "/Knowledge/Edit/1", "/Admin/Knowledge/Index", "/Monitoring/Index", "/Admin/Monitoring/Targets/Create",
            "/Exams/Take/1", "/Admin/Exams/Grade", "/Ventas/Llamadas", "/Sales/Calls",
            "/api/mobile/modules/exams/1/submit", "/api/mobile/modules/knowledge", "/api/mobile/modules/monitoring/1" };
        foreach (var method in new[] { "GET", "POST" })
        {
            foreach (var path in blocked)
            {
                var context = new DefaultHttpContext();
                context.Request.Method = method;
                context.Request.Path = path;
                context.Response.Body = new MemoryStream();
                await middleware.InvokeAsync(context, features);
                check(context.Response.StatusCode == 404 && called == 0, $"Suspensión bloquea {method} {path} antes del handler");
            }
        }
        var alias = new DefaultHttpContext();
        alias.Request.Path = "/alias-nuevo";
        alias.Response.Body = new MemoryStream();
        alias.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new PageActionDescriptor { ViewEnginePath = "/Sales/Calls" }), "alias"));
        await middleware.InvokeAsync(alias, features);
        check(alias.Response.StatusCode == 404 && called == 0, "Alias adicional de llamadas no evita la suspensión");
        foreach (var path in new[] { "/Admin/Carriers/Services", "/Clients/Documents/1", "/Projects/Delivery/Sign", "/Ventas/Factibilidad", "/Admin/Leaves", "/Sales/Prospects", "/Admin/System/Health" })
            check(features.IsPathEnabled(path), $"Se conserva acceso al módulo {path}");

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, AppRoles.Admin) }, "test"));
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("Data Source=:memory:").Options);
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var modules = new ModuleAccessService(db, http, features);
        var allowed = await modules.GetAllowedModulesAsync(user);
        check(!allowed.Contains(AppModules.Knowledge) && !allowed.Contains(AppModules.Exams) && !allowed.Contains(AppModules.Monitoring)
            && allowed.Contains(AppModules.Carriers), "Catálogo de módulos excluye suspendidos incluso para admin");
        check(!await modules.HasAccessAsync(user, AppModules.Exams), "Bypass administrativo no reactiva exámenes");
        var actions = new ActionAccessService(db, http, modules, features);
        check(!await actions.HasActionAsync(user, AppActions.SalesCallsUse) && !await actions.HasActionAsync(user, AppActions.SalesCallsView),
            "Permisos antiguos de llamadas no evitan la suspensión");

        var probe = new MonitorProbeService(NullLogger<MonitorProbeService>.Instance, new NeverHttp(), features);
        var stopped = false;
        try { await probe.ProbeAsync(new MonitorTarget { Fqdn = "must-not-connect.invalid" }); }
        catch (InvalidOperationException) { stopped = true; }
        check(stopped, "La sonda suspendida rechaza ejecución antes de intentar conexiones");
        using var provider = new ServiceCollection().AddSingleton(features).BuildServiceProvider();
        using var worker = new MonitorWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<MonitorWorker>.Instance, config);
        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;
        check(worker.ExecuteTask.IsCompletedSuccessfully, "Worker suspendido termina sin resolver DB ni ejecutar sondeos");

        TagHelperOutput Render(string href, string? module = null, bool relocated = false)
        {
            var attrs = new TagHelperAttributeList { { "href", href } };
            if (module != null) attrs.Add("hn-module", module);
            if (relocated) attrs.Add("hn-menu-relocated", "");
            var output = new TagHelperOutput("a", attrs, (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetContent("Link")));
            new ModuleVisibilityTagHelper(features).Process(new TagHelperContext(attrs, new Dictionary<object, object>(), "test"), output);
            return output;
        }
        check(Render("/Sales/Calls").TagName == null && Render("/Exams").TagName == null && Render("/Carriers").TagName == "a",
            "Menús y accesos secundarios ocultan módulos suspendidos conservando carriers");
        check(Render("/Admin/Leaves", relocated: true).TagName == null && Render("/Admin/Leaves").TagName == "a",
            "Vacaciones se oculta solo del menú principal, conservando el acceso interno");
        config["Features:DocumentsEnabled"] = "true";
        config["Features:ExamsEnabled"] = "true";
        config["Features:MonitoringEnabled"] = "true";
        config["Features:SalesCallsEnabled"] = "true";
        check(blocked.All(features.IsPathEnabled), "Los cuatro módulos pueden reactivarse por configuración sin recuperar código borrado");
    }

    private sealed class NeverHttp : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new Exception("No debe crear clientes HTTP durante suspensión.");
    }
}
