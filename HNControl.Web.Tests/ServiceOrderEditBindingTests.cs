using System.Globalization;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using OrderEdit = HNControl.Web.Pages.Admin.ServiceOrders.EditModel;

internal static class ServiceOrderEditBindingTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore().AddDataAnnotations();
        using var provider = services.BuildServiceProvider();
        var client = new Client { Name = "Cliente de edición", IsActive = true };
        var order = new ServiceOrder
        {
            Client = client, Title = "Título original", Type = ServiceOrderType.Preventivo,
            StartedAt = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc),
            EstimatedEndDate = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc)
        };
        db.ServiceOrders.Add(order);
        await db.SaveChangesAsync();

        // Exercise MVC binding and validation, including its empty-prefix fallback.
        // Setting Input directly would miss the error caused by the other form.
        async Task<OrderEdit> Bind(Dictionary<string, StringValues> fields)
        {
            var http = new DefaultHttpContext { RequestServices = provider };
            http.Request.Method = "POST";
            http.Request.Form = new FormCollection(fields);
            var page = new OrderEdit(db)
            {
                PageContext = new PageContext
                {
                    HttpContext = http, RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
                    ActionDescriptor = new CompiledPageActionDescriptor()
                }
            };
            page.PageContext.RouteData.Values["id"] = order.Id.ToString();
            var values = new CompositeValueProvider
            {
                new FormValueProvider(BindingSource.Form, http.Request.Form, CultureInfo.InvariantCulture),
                new RouteValueProvider(BindingSource.Path, page.RouteData.Values)
            };
            var metadataProvider = provider.GetRequiredService<IModelMetadataProvider>();
            var factory = provider.GetRequiredService<IModelBinderFactory>();
            var parameterBinder = provider.GetRequiredService<ParameterBinder>();
            foreach (var property in typeof(OrderEdit).GetProperties().Where(p => p.IsDefined(typeof(BindPropertyAttribute), true)))
            {
                var metadata = metadataProvider.GetMetadataForProperty(typeof(OrderEdit), property.Name);
                var binding = BindingInfo.GetBindingInfo(property.GetCustomAttributes(true), metadata);
                var descriptor = new ParameterDescriptor { Name = property.Name, ParameterType = property.PropertyType, BindingInfo = binding };
                var binder = factory.CreateBinder(new ModelBinderFactoryContext { Metadata = metadata, BindingInfo = binding, CacheToken = property });
                var result = await parameterBinder.BindModelAsync(page.PageContext, binder, values, descriptor, metadata, value: null, container: page);
                if (result.IsModelSet) property.SetValue(page, result.Model);
            }
            return page;
        }

        Dictionary<string, StringValues> MainForm(string title = "Mantenimiento Preventivo CCTV - Sucursal Mexico") => new()
        {
            ["Input.Id"] = order.Id.ToString(), ["Input.ClientId"] = client.Id.ToString(),
            ["Input.Title"] = title, ["Input.Type"] = "Preventivo", ["Input.Status"] = "Created",
            ["Input.Description"] = "Mantenimiento CCTV", ["Input.StartDate"] = "2026-11-04",
            ["Input.ExpectedEndDate"] = "2026-11-05"
        };
        Dictionary<string, StringValues> ActivityForm(string title, string type = "Preventivo") => new()
        {
            ["NewWorkItem.Title"] = title, ["NewWorkItem.Type"] = type,
            ["NewWorkItem.Description"] = "Revisión de cámaras"
        };

        var edit = await Bind(MainForm());
        var saved = await edit.OnPostAsync();
        await db.Entry(order).ReloadAsync();
        check(saved is RedirectToPageResult && order.Title == "Mantenimiento Preventivo CCTV - Sucursal Mexico"
            && !await db.ServiceOrderWorkItems.AnyAsync(),
            "Guardar orden con título no exige un título de actividad ni agrega actividades");

        var empty = await Bind(MainForm(""));
        check(await empty.OnPostAsync() is PageResult && empty.ModelState["Input.Title"]?.Errors.Count > 0,
            "El título de la orden sigue siendo obligatorio en el formulario principal");

        var invalidDateFields = MainForm("No guardar");
        invalidDateFields["Input.StartDate"] = "fecha incorrecta";
        var invalidDate = await Bind(invalidDateFields);
        check(await invalidDate.OnPostAsync() is PageResult
            && invalidDate.ModelState["Input.StartDate"]?.AttemptedValue == "fecha incorrecta"
            && invalidDate.ModelState["Input.StartDate"]?.Errors.Count > 0,
            "Guardar orden conserva los errores de conversión de fecha");
        await db.Entry(order).ReloadAsync();
        check(order.Title == "Mantenimiento Preventivo CCTV - Sucursal Mexico" && order.StartedAt?.Day == 4,
            "Los formularios inválidos no modifican la orden persistida");

        var emptyActivity = await Bind(ActivityForm(""));
        check(await emptyActivity.OnPostAddWorkItemAsync(order.Id) is PageResult
            && emptyActivity.ModelState["NewWorkItem.Title"]?.Errors.Count > 0
            && !await db.ServiceOrderWorkItems.AnyAsync(),
            "Agregar actividad sigue exigiendo su propio título");

        var invalidActivity = await Bind(ActivityForm("No agregar", "tipo incorrecto"));
        check(await invalidActivity.OnPostAddWorkItemAsync(order.Id) is PageResult
            && invalidActivity.ModelState["NewWorkItem.Type"]?.Errors.Count > 0
            && !await db.ServiceOrderWorkItems.AnyAsync(),
            "Agregar actividad conserva los errores de conversión del tipo y no guarda un valor por defecto");

        var activity = await Bind(ActivityForm("Limpieza de cámaras"));
        check(await activity.OnPostAddWorkItemAsync(order.Id) is RedirectToPageResult
            && await db.ServiceOrderWorkItems.AnyAsync(w => w.OrderId == order.Id && w.Title == "Limpieza de cámaras")
            && await db.ServiceOrders.AnyAsync(o => o.Id == order.Id && o.Type == ServiceOrderType.Global),
            "Agregar actividad válida funciona sin enviar los campos del formulario principal");

        var globalEdit = await Bind(MainForm("Orden global actualizada"));
        check(await globalEdit.OnPostAsync() is RedirectToPageResult
            && await db.ServiceOrders.AnyAsync(o => o.Id == order.Id && o.Title == "Orden global actualizada" && o.Type == ServiceOrderType.Global)
            && await db.ServiceOrderWorkItems.CountAsync(w => w.OrderId == order.Id) == 2,
            "Editar una orden global conserva sus actividades y su tipo global");
    }
}
