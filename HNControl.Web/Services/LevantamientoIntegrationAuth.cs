using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace HNControl.Web.Services;

// Dedicated key: the public questionnaire host never holds the Support key, which can create orders.
public sealed class LevantamientoIntegrationAuth(IConfiguration configuration) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var key = configuration["LevantamientoIntegration:ApiKey"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32) {
            context.Result = new ObjectResult(new { message = "Integración de levantamiento no configurada." }) { StatusCode = 503 }; return;
        }
        if (!SupportIntegrationAuth.Matches(key, context.HttpContext.Request.Headers["X-HN-Levantamiento-Key"].ToString())) {
            context.Result = new UnauthorizedObjectResult(new { message = "Credencial de integración inválida." }); return;
        }
        await next();
    }
}
