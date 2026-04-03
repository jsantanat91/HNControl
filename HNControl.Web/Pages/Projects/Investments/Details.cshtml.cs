using System.Security.Cryptography;
using System.Text;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HNControl.Web.Pages.Projects.Investments;

[Authorize(Policy = "EmployeeOnly")]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _cfg;

    public DetailsModel(ApplicationDbContext db, IEmailSender emailSender, IConfiguration cfg)
    {
        _db = db;
        _emailSender = emailSender;
        _cfg = cfg;
    }

    public InvestmentInvestor? Investor { get; set; }

    public record PaymentVm(Guid Id, int Number, DateTime DueDate, decimal Principal, decimal Profit, decimal Total, bool IsPaid, DateTime? PaidAt, string PaymentReference);
    public record PlanVm(Guid Id, string Name, decimal PrincipalAmount, decimal ProfitPercent, int PaymentCount, InvestmentPeriodicity Periodicity, decimal PaidAmount, decimal PendingAmount, List<PaymentVm> Payments);
    public List<PlanVm> Plans { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Investor = await _db.InvestmentInvestors
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (Investor == null) return NotFound();

        Plans = await _db.InvestmentPlans
            .AsNoTracking()
            .Where(x => x.InvestorId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(p => new PlanVm(
                p.Id,
                p.Name,
                p.PrincipalAmount,
                p.ProfitPercent,
                p.PaymentCount,
                p.Periodicity,
                p.Payments.Where(x => x.IsPaid).Sum(x => x.TotalAmount),
                p.Payments.Where(x => !x.IsPaid).Sum(x => x.TotalAmount),
                p.Payments.OrderBy(x => x.PeriodNumber).Select(x => new PaymentVm(
                    x.Id,
                    x.PeriodNumber,
                    x.DueDate,
                    x.PrincipalPortion,
                    x.ProfitPortion,
                    x.TotalAmount,
                    x.IsPaid,
                    x.PaidAt,
                    x.PaymentReference
                )).ToList()
            ))
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostMarkPaidAsync(Guid paymentId, string? paymentReference)
    {
        var payment = await _db.InvestmentPayments
            .Include(x => x.Plan!)
            .ThenInclude(x => x.Investor)
            .FirstOrDefaultAsync(x => x.Id == paymentId);
        if (payment == null) return NotFound();

        var investor = payment.Plan?.Investor;
        if (investor == null) return NotFound();

        if (!payment.IsPaid)
        {
            payment.IsPaid = true;
            payment.PaidAt = DateTime.UtcNow;
            payment.PaymentReference = (paymentReference ?? "").Trim();
            payment.Plan!.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        string? emailError = null;
        try
        {
            var pdf = await BuildStatementPdfAsync(payment.PlanId);
            await _emailSender.SendAsync(
                investor.Email,
                $"Estado de cuenta actualizado Â· {payment.Plan!.Name}",
                BuildStatementEmailBody(investor.FullName, payment.Plan.Name, payment.TotalAmount),
                pdf,
                $"estado_cuenta_inversion_{payment.Plan.Name.Replace(' ', '_')}_{DateTime.Now:yyyyMMdd}.pdf",
                "application/pdf");
            payment.StatementSentAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            emailError = ex.Message;
        }

        var isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        if (isAjax)
        {
            return new JsonResult(new
            {
                ok = true,
                paidAt = payment.PaidAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                emailOk = emailError == null,
                emailError
            });
        }

        Flash = emailError == null
            ? "Pago registrado y estado de cuenta enviado por correo."
            : $"Pago registrado, pero el correo fallÃ³: {emailError}";
        return RedirectToPage(new { id = investor.Id });
    }

    public async Task<IActionResult> OnGetStatementPdfAsync(Guid id, Guid planId)
    {
        var investorId = await _db.InvestmentPlans
            .AsNoTracking()
            .Where(x => x.Id == planId)
            .Select(x => x.InvestorId)
            .FirstOrDefaultAsync();
        if (investorId == Guid.Empty || investorId != id) return NotFound();

        var pdf = await BuildStatementPdfAsync(planId);
        return File(pdf, "application/pdf", $"estado_cuenta_inversion_{DateTime.Now:yyyyMMdd}.pdf");
    }

    private async Task<byte[]> BuildStatementPdfAsync(Guid planId)
    {
        var plan = await _db.InvestmentPlans
            .AsNoTracking()
            .Include(x => x.Investor)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == planId);
        if (plan == null || plan.Investor == null)
            throw new InvalidOperationException("Plan no encontrado.");

        var paid = plan.Payments.Where(x => x.IsPaid).Sum(x => x.TotalAmount);
        var pending = plan.Payments.Where(x => !x.IsPaid).Sum(x => x.TotalAmount);
        var hash = BuildHash(plan, paid, pending);
        var company = (_cfg["Branding:CompanyName"] ?? "HN Solutions").Trim();

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(h =>
                {
                    h.Item().Text(company).FontSize(15).SemiBold();
                    h.Item().Text("Estado de cuenta de inversiÃ³n").FontSize(12).FontColor(Colors.Grey.Darken2);
                    h.Item().Text($"{plan.Investor.FullName} Â· {plan.Investor.Email}");
                });

                page.Content().PaddingTop(10).Column(c =>
                {
                    c.Spacing(8);
                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(x =>
                    {
                        x.Item().Text($"Plan: {plan.Name}").SemiBold();
                        x.Item().Text($"Capital: {plan.PrincipalAmount:C}");
                        x.Item().Text($"Ganancia pactada: {(plan.ProfitPercent * 100m):0.##}%");
                        x.Item().Text($"Pagado acumulado: {paid:C}");
                        x.Item().Text($"Saldo pendiente: {pending:C}");
                    });

                    c.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(50);
                            cd.ConstantColumn(75);
                            cd.RelativeColumn();
                            cd.ConstantColumn(90);
                            cd.ConstantColumn(90);
                            cd.ConstantColumn(90);
                            cd.ConstantColumn(70);
                        });

                        Header(t, "#");
                        Header(t, "Vence");
                        Header(t, "Referencia");
                        Header(t, "Capital");
                        Header(t, "Ganancia");
                        Header(t, "Total");
                        Header(t, "Estado");

                        foreach (var p in plan.Payments.OrderBy(x => x.PeriodNumber))
                        {
                            Body(t, p.PeriodNumber.ToString());
                            Body(t, p.DueDate.ToString("yyyy-MM-dd"));
                            Body(t, string.IsNullOrWhiteSpace(p.PaymentReference) ? "-" : p.PaymentReference);
                            Body(t, p.PrincipalPortion.ToString("C"));
                            Body(t, p.ProfitPortion.ToString("C"));
                            Body(t, p.TotalAmount.ToString("C"));
                            Body(t, p.IsPaid ? "Pagado" : "Pendiente", p.IsPaid ? Colors.Green.Darken2 : Colors.Orange.Darken2);
                        }
                    });
                });

                page.Footer().Column(f =>
                {
                    f.Item().AlignCenter().Text($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    f.Item().AlignCenter().Text($"Firma digital: {hash}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();
    }

    private static IContainer Head(IContainer c) => c.Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(4);
    private static IContainer Cell(IContainer c) => c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(4);
    private static void Header(TableDescriptor t, string text) => t.Cell().Element(Head).Text(text).SemiBold();
    private static void Body(TableDescriptor t, string text, string? color = null)
    {
        var cell = t.Cell().Element(Cell).Text(text);
        if (color != null) cell.FontColor(color);
    }

    private static string BuildStatementEmailBody(string investorName, string planName, decimal amount)
        => $@"<p>Hola {System.Net.WebUtility.HtmlEncode(investorName)},</p>
             <p>Registramos un pago en tu plan <b>{System.Net.WebUtility.HtmlEncode(planName)}</b>.</p>
             <p>Importe aplicado: <b>{amount:C2}</b></p>
             <p>Adjuntamos tu estado de cuenta actualizado en PDF.</p>
             <p>Saludos,<br/>HN Control</p>";

    private static string BuildHash(InvestmentPlan plan, decimal paid, decimal pending)
    {
        var payload = $"{plan.Id}|{plan.InvestorId}|{plan.Name}|{plan.PrincipalAmount:0.00}|{plan.ProfitPercent:0.00000}|{paid:0.00}|{pending:0.00}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes)[..16];
    }
}

