using HNControl.Web.Models;

namespace HNControl.Web.Services;

public interface IQuoteRequestPdfRenderer
{
    Task<byte[]> RenderAsync(QuoteRequest request);
}
