using HNControl.Web.Models;

namespace HNControl.Web.Services;

public record BillingFiscalResult(
    bool Ok,
    string Message,
    string CfdiUuid,
    BillingCfdiStatus Status,
    string TrackingId = "");

public interface IBillingFiscalService
{
    Task<BillingFiscalResult> SyncAsync(BillingInvoicePlan plan, BillingInvoiceRun run, CancellationToken ct = default);
    Task<BillingFiscalResult> CancelAsync(BillingInvoicePlan plan, BillingInvoiceRun run, string reasonCode, CancellationToken ct = default);
}

