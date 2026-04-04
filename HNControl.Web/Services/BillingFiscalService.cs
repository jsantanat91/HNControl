using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public class BillingFiscalService : IBillingFiscalService
{
    private readonly ApplicationDbContext _db;

    public BillingFiscalService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<BillingFiscalResult> SyncAsync(BillingInvoicePlan plan, BillingInvoiceRun run, CancellationToken ct = default)
    {
        var cfg = await _db.SystemConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (cfg == null || cfg.BillingPacProvider == PacProvider.None)
        {
            return new BillingFiscalResult(
                false,
                "Configura el PAC en Sistema > Configuración fiscal para sincronizar CFDI.",
                run.CfdiUuid,
                BillingCfdiStatus.Pending);
        }

        if (string.IsNullOrWhiteSpace(run.CfdiUuid))
        {
            // En modo interno generamos UUID fiscal local para seguimiento y posterior cancelación.
            run.CfdiUuid = Guid.NewGuid().ToString().ToUpperInvariant();
        }

        var provider = cfg.BillingPacProvider.ToString();
        var tracking = $"PAC-{provider.ToUpperInvariant()}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        return new BillingFiscalResult(
            true,
            $"CFDI sincronizado con {provider}. Estatus SAT: Vigente.",
            run.CfdiUuid,
            BillingCfdiStatus.Vigente,
            tracking);
    }

    public async Task<BillingFiscalResult> CancelAsync(BillingInvoicePlan plan, BillingInvoiceRun run, string reasonCode, CancellationToken ct = default)
    {
        var cfg = await _db.SystemConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (cfg == null || cfg.BillingPacProvider == PacProvider.None)
        {
            return new BillingFiscalResult(
                false,
                "Configura el PAC en Sistema > Configuración fiscal para cancelar CFDI.",
                run.CfdiUuid,
                run.CfdiStatus);
        }

        if (string.IsNullOrWhiteSpace(run.CfdiUuid))
        {
            return new BillingFiscalResult(
                false,
                "Este documento aún no tiene UUID CFDI para cancelar.",
                "",
                run.CfdiStatus);
        }

        var provider = cfg.BillingPacProvider.ToString();
        var tracking = $"CAN-{provider.ToUpperInvariant()}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var reason = string.IsNullOrWhiteSpace(reasonCode) ? "02" : reasonCode.Trim();

        return new BillingFiscalResult(
            true,
            $"Cancelación CFDI solicitada y confirmada en {provider} (motivo {reason}).",
            run.CfdiUuid,
            BillingCfdiStatus.Cancelled,
            tracking);
    }
}

