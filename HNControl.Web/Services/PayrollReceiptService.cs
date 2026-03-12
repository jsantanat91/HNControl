using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HNControl.Web.Services;

public class PayrollReceiptService : IPayrollReceiptService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _cfg;

    public PayrollReceiptService(ApplicationDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    public async Task<PayrollReceiptData?> BuildAsync(string userId, DateTime periodStart, DateTime periodEnd, DateTime payrollDate)
    {
        var profile = await _db.EmployeeProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile == null) return null;

        var pStart = periodStart.Date;
        var pEnd = periodEnd.Date;
        var pDate = payrollDate.Date;
        var pStartUtc = TimeUtil.UtcDate(pStart);
        var pEndUtc = TimeUtil.UtcDate(pEnd);

        var review = await _db.PerformanceReviews
            .AsNoTracking()
            .Where(r => r.UserId == userId
                        && r.PeriodStart >= pStartUtc && r.PeriodStart < pStartUtc.AddDays(1)
                        && r.PeriodEnd >= pEndUtc && r.PeriodEnd < pEndUtc.AddDays(1))
            .OrderByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync();

        if (review == null)
        {
            review = await _db.PerformanceReviews
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.PeriodStart)
                .ThenByDescending(r => r.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        var vp = review?.VariablePercent ?? 0m;
        if (vp < 0m) vp = 0m;
        if (vp > 1m) vp = 1m;

        var baseQ = Math.Round(profile.SalaryBase / 2m, 2);
        var fixed80 = Math.Round(baseQ * 0.80m, 2);
        var max20 = Math.Round(baseQ * 0.20m, 2);
        var variableAmount = Math.Round(max20 * vp, 2);
        var grossEstimated = Math.Round(fixed80 + variableAmount, 2);

        var (deductions, bonuses, lines) = await CalcPayrollAdjustmentsAsync(userId, baseQ, grossEstimated, pEnd);
        var net = Math.Max(0m, Math.Round(grossEstimated - deductions + bonuses, 2));

        var imss = BuildImssLines();

        return new PayrollReceiptData
        {
            UserId = userId,
            FullName = profile.FullName,
            Email = profile.Email,
            Position = profile.Position,
            Nss = profile.Nss,
            PeriodStart = pStart,
            PeriodEnd = pEnd,
            PayrollDate = pDate,
            SalaryBaseMonthly = profile.SalaryBase,
            BaseQuincenal = baseQ,
            Fixed80 = fixed80,
            Max20 = max20,
            VariablePercent = vp,
            VariableAmount = variableAmount,
            GrossEstimated = grossEstimated,
            Deductions = deductions,
            Bonuses = bonuses,
            NetEstimated = net,
            AppliedAdjustments = lines,
            ImssLines = imss
        };
    }

    public byte[] RenderPdf(PayrollReceiptData data)
    {
        var company = (_cfg["Branding:CompanyName"] ?? "HN Solutions").Trim();

        var doc = Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(24);
                p.DefaultTextStyle(x => x.FontSize(10));

                p.Header().Column(col =>
                {
                    col.Item().Text(company).FontSize(15).SemiBold();
                    col.Item().Text("Recibo de nomina quincenal").FontSize(12).FontColor(Colors.Grey.Darken2);
                    col.Item().Text($"Periodo: {data.PeriodStart:yyyy-MM-dd} a {data.PeriodEnd:yyyy-MM-dd}");
                    col.Item().Text($"Pago: {data.PayrollDate:yyyy-MM-dd}");
                });

                p.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(x =>
                        {
                            x.Item().Text("Empleado").SemiBold();
                            x.Item().Text(data.FullName);
                            x.Item().Text($"Puesto: {ValueOrDash(data.Position)}").FontColor(Colors.Grey.Darken2);
                            x.Item().Text($"Correo: {ValueOrDash(data.Email)}").FontColor(Colors.Grey.Darken2);
                            x.Item().Text($"NSS: {ValueOrDash(data.Nss)}").FontColor(Colors.Grey.Darken2);
                        });

                        r.ConstantItem(250).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(x =>
                        {
                            x.Item().Text("Resumen quincena").SemiBold();
                            x.Item().Text($"Sueldo mensual neto base: {Money(data.SalaryBaseMonthly)}");
                            x.Item().Text($"Base quincenal: {Money(data.BaseQuincenal)}");
                            x.Item().Text($"Variable 80/20: {(data.VariablePercent * 100m):0.#}%");
                            x.Item().Text($"Neto estimado: {Money(data.NetEstimated)}").SemiBold();
                        });
                    });

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(x =>
                    {
                        x.Item().Text("Calculo de nomina").SemiBold();
                        x.Item().PaddingTop(5).Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(2);
                                cd.ConstantColumn(110);
                            });

                            t.Cell().Element(CellHead).Text("Concepto");
                            t.Cell().Element(CellHead).AlignRight().Text("Importe");

                            Row(t, "80% fijo", data.Fixed80);
                            Row(t, "20% variable max", data.Max20);
                            Row(t, $"Variable aplicado ({(data.VariablePercent * 100m):0.#}%)", data.VariableAmount);
                            Row(t, "Total quincenal (sin ajustes)", data.GrossEstimated);
                            Row(t, "Deducciones", -data.Deductions);
                            Row(t, "Bonos", data.Bonuses);
                            Row(t, "Neto estimado", data.NetEstimated, true);
                        });
                    });

                    if (data.AppliedAdjustments.Count > 0)
                    {
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(x =>
                        {
                            x.Item().Text("Ajustes aplicados").SemiBold();
                            x.Item().PaddingTop(5).Table(t =>
                            {
                                t.ColumnsDefinition(cd =>
                                {
                                    cd.RelativeColumn(2);
                                    cd.ConstantColumn(130);
                                    cd.ConstantColumn(110);
                                });
                                t.Cell().Element(CellHead).Text("Concepto");
                                t.Cell().Element(CellHead).Text("Tipo");
                                t.Cell().Element(CellHead).AlignRight().Text("Importe");
                                foreach (var a in data.AppliedAdjustments)
                                {
                                    t.Cell().Element(CellBody).Text(a.Concept);
                                    t.Cell().Element(CellBody).Text(a.Kind);
                                    t.Cell().Element(CellBody).AlignRight().Text(Money(a.Amount));
                                }
                            });
                        });
                    }

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(x =>
                    {
                        x.Item().Text("Aportaciones IMSS (informativo)").SemiBold();
                        x.Item().Text("No se descuentan del neto; se muestran para comprobante interno.").FontColor(Colors.Grey.Darken2);
                        x.Item().PaddingTop(5).Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(2);
                                cd.ConstantColumn(120);
                                cd.ConstantColumn(120);
                            });
                            t.Cell().Element(CellHead).Text("Concepto");
                            t.Cell().Element(CellHead).AlignRight().Text("Mensual");
                            t.Cell().Element(CellHead).AlignRight().Text("Quincena");
                            foreach (var l in data.ImssLines)
                            {
                                t.Cell().Element(CellBody).Text(l.Concept);
                                t.Cell().Element(CellBody).AlignRight().Text(Money(l.MonthlyAmount));
                                t.Cell().Element(CellBody).AlignRight().Text(Money(l.PeriodAmount));
                            }
                            t.Cell().Element(CellHead).Text("Total");
                            t.Cell().Element(CellHead).AlignRight().Text(Money(data.ImssMonthlyTotal));
                            t.Cell().Element(CellHead).AlignRight().Text(Money(data.ImssPeriodTotal));
                        });
                    });
                });

                p.Footer().AlignCenter().Text($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

        return doc.GeneratePdf();
    }

    private async Task<(decimal deductions, decimal bonuses, List<PayrollAdjustmentLine> lines)> CalcPayrollAdjustmentsAsync(
        string userId, decimal baseQuincenal, decimal estimatedQuincenal, DateTime periodDate)
    {
        var currentHalf = periodDate.Day <= 15
            ? EmployeeDeductionApplyOnHalf.First
            : EmployeeDeductionApplyOnHalf.Second;

        var active = await _db.EmployeeDeductions
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .Where(d => d.StartDate <= periodDate && (d.EndDate == null || d.EndDate >= periodDate))
            .Where(d => d.Frequency == EmployeeDeductionFrequency.Biweekly
                        || (d.Frequency == EmployeeDeductionFrequency.Monthly
                            && (d.ApplyOnHalf == null || d.ApplyOnHalf == currentHalf)))
            .ToListAsync();

        decimal deductions = 0m;
        decimal bonuses = 0m;
        var lines = new List<PayrollAdjustmentLine>();

        foreach (var d in active)
        {
            var amount = d.Mode switch
            {
                EmployeeDeductionMode.FixedAmount => d.Amount,
                EmployeeDeductionMode.PercentOfBase => baseQuincenal * d.Rate,
                EmployeeDeductionMode.PercentOfEstimatedPay => estimatedQuincenal * d.Rate,
                _ => d.Amount
            };

            amount = Math.Round(Math.Max(0m, amount), 2);
            if (d.RemainingAmount.HasValue)
            {
                if (d.RemainingAmount.Value <= 0m) continue;
                if (amount > d.RemainingAmount.Value) amount = d.RemainingAmount.Value;
            }

            if (d.Direction == EmployeeDeductionDirection.Bonus)
            {
                bonuses += amount;
                lines.Add(new PayrollAdjustmentLine(d.Concept, "Bono", amount));
            }
            else
            {
                deductions += amount;
                lines.Add(new PayrollAdjustmentLine(d.Concept, "Deduccion", amount));
            }
        }

        return (Math.Round(deductions, 2), Math.Round(bonuses, 2), lines.OrderBy(x => x.Kind).ThenBy(x => x.Concept).ToList());
    }

    private static List<PayrollImssLine> BuildImssLines()
    {
        var monthly = new List<PayrollImssLine>
        {
            new("IMSS Cesantia y vejez", 700m, 350m),
            new("IMSS Invalidez y vida", 500m, 250m),
            new("IMSS Enfermedades y maternidad", 400m, 200m),
            new("IMSS Riesgo de trabajo", 400m, 200m)
        };
        return monthly;
    }

    private static void Row(TableDescriptor t, string concept, decimal value, bool strong = false)
    {
        var v = Money(value);
        if (strong)
        {
            t.Cell().Element(CellHead).Text(concept);
            t.Cell().Element(CellHead).AlignRight().Text(v);
            return;
        }
        t.Cell().Element(CellBody).Text(concept);
        t.Cell().Element(CellBody).AlignRight().Text(v);
    }

    private static IContainer CellHead(IContainer c) => c.Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5);
    private static IContainer CellBody(IContainer c) => c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
    private static string Money(decimal? v) => (v ?? 0m).ToString("C2");
    private static string ValueOrDash(string? v) => string.IsNullOrWhiteSpace(v) ? "-" : v.Trim();
}
