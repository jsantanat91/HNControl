using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

internal static class PayrollPostgresTests
{
    // Run with --payroll-postgres and HNCONTROL_TEST_POSTGRES pointing to a local test database.
    // Each run creates and drops only its own randomly named schema. Email is always simulated.
    public static async Task RunAsync(Action<bool, string> check)
    {
        var connectionString = Environment.GetEnvironmentVariable("HNCONTROL_TEST_POSTGRES")
            ?? throw new InvalidOperationException("Configure HNCONTROL_TEST_POSTGRES for the PostgreSQL regression test.");
        var schema = "payroll_test_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", connection)) await create.ExecuteNonQueryAsync();
        try
        {
            var scoped = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema, Pooling = false };
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scoped.ConnectionString).Options);
            await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
            var employee = new EmployeeProfile { UserId = "payroll-test", FullName = "Empleado de prueba", Email = "nomina@example.test", SalaryBase = 30000, IsActive = true };
            var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddDays(14);
            db.Add(employee);
            db.Add(new EmployeeDeduction { UserId = employee.UserId, Concept = "Bono de prueba", Direction = EmployeeDeductionDirection.Bonus,
                Amount = 150, StartDate = start, EndDate = end, TermCount = 1 });
            db.Add(new EmployeeDeduction { UserId = employee.UserId, Concept = "Deducción de prueba", Direction = EmployeeDeductionDirection.Deduct,
                Amount = 500, StartDate = start, EndDate = end, TermCount = 1 });
            await db.SaveChangesAsync();
            var cfg = new ConfigurationBuilder().Build();
            var realReceipt = new PayrollReceiptService(db, cfg);
            var receipt = new TestReceipt(realReceipt);
            var email = new ClientCommunicationTests.RecordingEmail();
            var ledger = new PayrollDeductionLedgerService(db, NullLogger<PayrollDeductionLedgerService>.Instance);
            HNControl.Web.Pages.Admin.DashboardModel Panel() => new(db, receipt, email, null!, null!, ledger, new ModuleAvailability(cfg));

            var panel = Panel();
            panel.PayrollYear = 2026; panel.PayrollMonth = 11; panel.PayrollHalf = 1;
            await panel.OnGetAsync();
            var row = panel.PayrollRows.Single();
            check(row.Deductions == 500 && row.Bonuses == 150 && row.NetEstimated == 11650,
                "PostgreSQL: panel resta 500 de deducciones y suma 150 de bonos al 80% fijo de 12000");
            var export = (FileContentResult)await new HNControl.Web.Pages.Performance.ExportPayrollModel(db).OnGetAsync(start, end);
            using (var workbook = new XLWorkbook(new MemoryStream(export.FileContents)))
            {
                var sheet = workbook.Worksheet(1);
                check(sheet.Cell(2, 13).GetValue<decimal>() == 500 && sheet.Cell(2, 14).GetValue<decimal>() == 150
                    && sheet.Cell(2, 15).GetValue<decimal>() == 11650, "PostgreSQL: Excel coincide con deducciones, bonos y neto del panel");
            }
            await panel.OnPostMarkPaidAsync(employee.UserId, 2026, 11, 1);
            check(panel.FlashError == null && email.Messages.Count == 1,
                "PostgreSQL: marcar pagado genera y envía el recibo sin error de fecha. " + panel.FlashError);
            check(email.Messages[0].To == employee.Email && email.Messages[0].Attachment?.Length > 0,
                "PostgreSQL: el correo simulado incluye destinatario y PDF");
            var dispatch = await db.PayrollReceiptDispatches.AsNoTracking().SingleAsync();
            check(dispatch.IsSent && dispatch.PeriodStart.Date == start.Date && dispatch.PeriodEnd.Date == end.Date && dispatch.LastError == null,
                "PostgreSQL: pago confirmado conserva las fechas de la quincena");
            var applied = await db.EmployeeDeductionApplications.AsNoTracking().SingleAsync(x => x.Direction == EmployeeDeductionDirection.Bonus);
            check(applied.Amount == 150 && applied.PeriodStart == start && applied.PeriodEnd == end && applied.PayrollDate.Kind == DateTimeKind.Utc,
                "PostgreSQL: bono aplicado y fechas del ledger persistidas en UTC");
            check(await db.EmployeeDeductionApplications.AnyAsync(x => x.Direction == EmployeeDeductionDirection.Deduct && x.Amount == 500),
                "PostgreSQL: la deducción también se registra al confirmar el pago");
            await Panel().OnPostMarkPaidAsync(employee.UserId, 2026, 11, 1);
            check(email.Messages.Count == 1 && await db.PayrollReceiptDispatches.CountAsync() == 1 && await db.EmployeeDeductionApplications.CountAsync() == 2,
                "PostgreSQL: repetir marcar pagado no duplica correo ni bono");
            foreach (var kind in new[] { DateTimeKind.Unspecified, DateTimeKind.Local, DateTimeKind.Utc })
            {
                var data = await realReceipt.BuildAsync(employee.UserId, DateTime.SpecifyKind(start, kind), DateTime.SpecifyKind(end, kind), DateTime.SpecifyKind(end, kind));
                check(data?.Bonuses == 150 && data.Deductions == 500 && data.NetEstimated == 11650 && data.PeriodStart.Date == start.Date,
                    $"PostgreSQL: recibo con fechas {kind} conserva fechas, deducciones, bonos y neto después del pago");
                await ledger.RecordApplicationsAsync(employee.UserId, DateTime.SpecifyKind(start, kind), DateTime.SpecifyKind(end, kind), DateTime.SpecifyKind(end, kind));
            }
            check(await db.EmployeeDeductionApplications.CountAsync() == 2, "PostgreSQL: normalizar fechas no duplica aplicaciones del ledger");
            await panel.OnGetAsync();
            check(panel.PayrollRows.Single().NetEstimated == 11650 && panel.PayrollRows.Single().IsPaid,
                "PostgreSQL: el panel mantiene los ajustes aplicados al cerrar el pago");

            email.Fail = true;
            panel = Panel();
            await panel.OnPostMarkPaidAsync(employee.UserId, 2026, 11, 2);
            var failed = await db.PayrollReceiptDispatches.AsNoTracking().SingleAsync(x => x.PeriodStart.Day == 16);
            check(panel.FlashError != null && !failed.IsSent && failed.LastError == "Simulated SMTP failure",
                "PostgreSQL: fallo SMTP real se registra y conserva el pago pendiente");
            email.Fail = false;
            await Panel().OnPostMarkPaidAsync(employee.UserId, 2026, 11, 2);
            check(email.Messages.Count == 2 && await db.PayrollReceiptDispatches.CountAsync(x => x.IsSent) == 2,
                "PostgreSQL: reintento de SMTP confirma el pendiente sin duplicar la quincena anterior");
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class TestReceipt(PayrollReceiptService service) : IPayrollReceiptService
    {
        public Task<PayrollReceiptData?> BuildAsync(string userId, DateTime start, DateTime end, DateTime paid) => service.BuildAsync(userId, start, end, paid);
        public byte[] RenderPdf(PayrollReceiptData data) => "%PDF-1.7 payroll workflow test"u8.ToArray();
    }
}
