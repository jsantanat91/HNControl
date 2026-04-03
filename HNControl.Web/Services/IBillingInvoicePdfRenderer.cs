using HNControl.Web.Models;

namespace HNControl.Web.Services;

public interface IBillingInvoicePdfRenderer
{
    Task<byte[]> RenderAsync(BillingInvoicePlan plan, BillingInvoiceRun run);
}
