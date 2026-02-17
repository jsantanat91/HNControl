using HNControl.Web.Models;

namespace HNControl.Web.Services;

public class ServiceOrderPdfRenderer : IServiceOrderPdfRenderer
{
    public Task<byte[]> RenderAsync(ServiceOrder order)
    {
        // Stub: por ahora devolvemos “PDF vacío” para que compile.
        // En la siguiente iteración lo generamos con QuestPDF con logo, tablas y firmas.
        return Task.FromResult(Array.Empty<byte>());
    }
}
