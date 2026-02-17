using HNControl.Web.Models;

namespace HNControl.Web.Services;

public interface IServiceOrderPdfRenderer
{
    // Genera PDF de orden de servicio
    Task<byte[]> RenderAsync(ServiceOrder order);
}
