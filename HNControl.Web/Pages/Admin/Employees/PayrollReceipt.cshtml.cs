using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HNControl.Web.Pages.Admin.Employees;

[Authorize(Roles = AppRoles.Admin)]
public class PayrollReceiptModel : PageModel
{
    private readonly IPayrollReceiptService _receipt;

    public PayrollReceiptModel(IPayrollReceiptService receipt)
    {
        _receipt = receipt;
    }

    public async Task<IActionResult> OnGetAsync(string userId, DateTime? start, DateTime? end)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("Falta userId.");

        var (periodStart, periodEnd, payrollDate) = ResolvePeriod(start, end);
        var data = await _receipt.BuildAsync(userId, periodStart, periodEnd, payrollDate);
        if (data == null) return NotFound();

        var pdf = _receipt.RenderPdf(data);
        var file = $"recibo_nomina_{data.FullName.Replace(" ", "_")}_{periodStart:yyyyMMdd}_{periodEnd:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", file);
    }

    private static (DateTime start, DateTime end, DateTime payrollDate) ResolvePeriod(DateTime? start, DateTime? end)
    {
        if (start.HasValue && end.HasValue)
            return (start.Value.Date, end.Value.Date, end.Value.Date);

        var today = DateTime.Now.Date;
        if (today.Day <= 15)
            return (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 15), new DateTime(today.Year, today.Month, 15));

        var second = Math.Min(30, DateTime.DaysInMonth(today.Year, today.Month));
        return (new DateTime(today.Year, today.Month, 16), new DateTime(today.Year, today.Month, second), new DateTime(today.Year, today.Month, second));
    }
}

