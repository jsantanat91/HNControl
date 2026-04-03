using HNControl.Web.Models;

namespace HNControl.Web.Services;

public interface IClientLegalPdfRenderer
{
    Task<byte[]> RenderAsync(ClientLegalDocument document);
}
