using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace HNControl.Web.Services;

public sealed class SupportIntegrationAuth(IConfiguration configuration) : IAsyncActionFilter
{
    public static bool Matches(string? configured, string? supplied) =>
        !string.IsNullOrWhiteSpace(configured) && configured.Length >= 32 && !string.IsNullOrWhiteSpace(supplied)
        && supplied.Length <= 512 && CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(configured)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var key = configuration["SupportIntegration:ApiKey"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32) {
            context.Result = new ObjectResult(new { message = "Integración Support no configurada." }) { StatusCode = 503 }; return;
        }
        if (!Matches(key, context.HttpContext.Request.Headers["X-HN-Support-Key"].ToString())) {
            context.Result = new UnauthorizedObjectResult(new { message = "Credencial de integración inválida." }); return;
        }
        await next();
    }
}
