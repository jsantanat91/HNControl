using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Services;

public sealed class ModuleSuspensionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ModuleAvailability features)
    {
        // Use page metadata as well as the URL, so aliases and POST handlers cannot bypass suspension.
        var page = context.GetEndpoint()?.Metadata.GetMetadata<PageActionDescriptor>()?.ViewEnginePath;
        if (!features.IsPathEnabled(context.Request.Path.Value) || !features.IsPathEnabled(page))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsync("Este módulo está suspendido.", context.RequestAborted);
            return;
        }
        await next(context);
    }
}
