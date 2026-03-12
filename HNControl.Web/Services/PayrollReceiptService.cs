using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

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

        var imss = BuildImssLines(baseQ);

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
        var logoBytes = LoadLogoBytes();
        var periodLabel = BuildPeriodLabel(data.PayrollDate);
        var paidAmount = data.NetEstimated;
        var grossWithImss = Math.Round(data.GrossEstimated + data.ImssPeriodTotal, 2);
        var signatureHash = BuildReceiptHash(data, periodLabel);

        var doc = Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(24);
                p.DefaultTextStyle(x => x.FontSize(10));

                p.Header().Row(r =>
                {
                    r.RelativeItem().Column(col =>
                    {
                        if (logoBytes is { Length: > 0 })
                            col.Item().Height(52).Width(160).Image(logoBytes).FitArea();
                        else
                            col.Item().Text(company).FontSize(15).SemiBold();

                        col.Item().Text("Recibo de nomina quincenal").FontSize(12).FontColor(Colors.Grey.Darken2);
                        col.Item().Text($"Periodo: {periodLabel}");
                        col.Item().Text($"Pago: {data.PayrollDate:dd-MM-yyyy}");
                    });
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
                            x.Item().Text($"Sueldo neto base: {Money(data.SalaryBaseMonthly)}");
                            x.Item().Text($"Base quincenal: {Money(data.BaseQuincenal)}");
                            x.Item().Text($"Variable: {(data.VariablePercent * 100m):0.#}%");
                            x.Item().Text($"Sueldo bruto: {Money(grossWithImss)}");
                            x.Item().PaddingTop(3).Text($"Pago: {Money(paidAmount)}").FontSize(14).SemiBold();
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
                            Row(t, $"Variable ({(data.VariablePercent * 100m):0.#}%)", data.VariableAmount);
                            Row(t, "Subtotal quincena", data.GrossEstimated);
                            Row(t, "IMSS empleado (quincena)", -data.ImssEmployeePeriodTotal, false, Colors.Red.Darken1);
                            Row(t, "Deducciones aplicadas", -data.Deductions, false, Colors.Red.Darken1);
                            Row(t, "Bonos extra", data.Bonuses, false, Colors.Green.Darken2);
                            Row(t, "Pago", paidAmount, true);
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
                                    var isBonus = string.Equals(a.Kind, "Bono", StringComparison.OrdinalIgnoreCase);
                                    t.Cell().Element(CellBody).Text(a.Concept);
                                    t.Cell().Element(CellBody).Text(a.Kind).FontColor(isBonus ? Colors.Green.Darken2 : Colors.Red.Darken1);
                                    t.Cell().Element(CellBody).AlignRight().Text($"{(isBonus ? "+" : "-")}{Money(a.Amount)}")
                                        .FontColor(isBonus ? Colors.Green.Darken2 : Colors.Red.Darken1);
                                }
                            });
                        });
                    }

                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(x =>
                    {
                        x.Item().Text("Aportaciones IMSS (quincena)").SemiBold();
                        x.Item().PaddingTop(5).Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(2);
                                cd.ConstantColumn(80);
                                cd.ConstantColumn(120);
                                cd.ConstantColumn(80);
                                cd.ConstantColumn(120);
                            });
                            t.Cell().Element(CellHead).Text("Concepto");
                            t.Cell().Element(CellHead).AlignRight().Text("% Patrón");
                            t.Cell().Element(CellHead).AlignRight().Text("Patrón");
                            t.Cell().Element(CellHead).AlignRight().Text("% Empleado");
                            t.Cell().Element(CellHead).AlignRight().Text("Empleado");
                            foreach (var l in data.ImssLines)
                            {
                                t.Cell().Element(CellBody).Text(l.Concept);
                                t.Cell().Element(CellBody).AlignRight().Text($"{l.EmployerRate:0.###}%");
                                t.Cell().Element(CellBody).AlignRight().Text($"-{Money(l.EmployerPeriodAmount)}").FontColor(Colors.Red.Darken1);
                                t.Cell().Element(CellBody).AlignRight().Text($"{l.EmployeeRate:0.###}%");
                                t.Cell().Element(CellBody).AlignRight().Text($"-{Money(l.EmployeePeriodAmount)}").FontColor(Colors.Red.Darken1);
                            }
                            t.Cell().Element(CellHead).Text("Total");
                            t.Cell().Element(CellHead).AlignRight().Text("-");
                            t.Cell().Element(CellHead).AlignRight().Text($"-{Money(data.ImssEmployerPeriodTotal)}").FontColor(Colors.Red.Darken1);
                            t.Cell().Element(CellHead).AlignRight().Text("-");
                            t.Cell().Element(CellHead).AlignRight().Text($"-{Money(data.ImssEmployeePeriodTotal)}").FontColor(Colors.Red.Darken1);
                        });
                    });
                });

                p.Footer().Column(col =>
                {
                    col.Item().AlignCenter().Text($"Generado por HN Control - Nomina ({periodLabel})").FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().AlignCenter().Text($"Firma digital: {signatureHash}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
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

    private static List<PayrollImssLine> BuildImssLines(decimal baseQuincenal)
    {
        // Tasas aproximadas de referencia para simulación de recibo.
        // Se aplican sobre base quincenal para mostrar desglose patrón/empleado.
        var rates = new List<(string Concept, decimal EmployerRate, decimal EmployeeRate)>
        {
            ("Cesantía y vejez", 3.150m, 1.125m),
            ("Invalidez y vida", 1.750m, 0.625m),
            ("Enfermedades y maternidad", 1.050m, 0.400m),
            ("Riesgo de trabajo", 0.543m, 0.000m)
        };

        return rates.Select(x => new PayrollImssLine(
            x.Concept,
            x.EmployerRate,
            x.EmployeeRate,
            Math.Round(baseQuincenal * (x.EmployerRate / 100m), 2),
            Math.Round(baseQuincenal * (x.EmployeeRate / 100m), 2)))
            .ToList();
    }

    private static void Row(TableDescriptor t, string concept, decimal value, bool strong = false, string? color = null)
    {
        var v = value < 0m ? $"-{Money(Math.Abs(value))}" : value > 0m && color == Colors.Green.Darken2 ? $"+{Money(value)}" : Money(value);
        if (strong)
        {
            t.Cell().Element(CellHead).Text(concept);
            t.Cell().Element(CellHead).AlignRight().Text(v).FontSize(12).SemiBold();
            return;
        }
        if (color is not null)
        {
            t.Cell().Element(CellBody).Text(concept).FontColor(color);
            t.Cell().Element(CellBody).AlignRight().Text(v).FontColor(color);
        }
        else
        {
            t.Cell().Element(CellBody).Text(concept);
            t.Cell().Element(CellBody).AlignRight().Text(v);
        }
    }

    private static IContainer CellHead(IContainer c) => c.Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5);
    private static IContainer CellBody(IContainer c) => c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
    private static string Money(decimal? v) => (v ?? 0m).ToString("C2");
    private static string ValueOrDash(string? v) => string.IsNullOrWhiteSpace(v) ? "-" : v.Trim();

    private static string BuildPeriodLabel(DateTime payrollDate)
    {
        var q = payrollDate.Day <= 15 ? 1 : 2;
        var monthLabel = payrollDate.ToString("MMMM yyyy", new CultureInfo("es-MX"));
        return $"{char.ToUpper(monthLabel[0])}{monthLabel[1..]} (Quincena {q})";
    }

    private byte[]? LoadLogoBytes()
    {
        var configured = (_cfg["Branding:LogoPath"] ?? "").Trim();
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured))
            candidates.Add(configured);
        candidates.Add("assets/logo.png");
        candidates.Add("wwwroot/assets/logo.png");
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "assets", "logo.png"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "wwwroot", "assets", "logo.png"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "assets", "logo.png"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "logo.png"));

        foreach (var path in candidates.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            try
            {
                if (File.Exists(path))
                    return File.ReadAllBytes(path);
            }
            catch { }
        }
        return null;
    }

    private static string BuildReceiptHash(PayrollReceiptData data, string periodLabel)
    {
        var payload = string.Join("|",
            data.UserId,
            data.FullName,
            periodLabel,
            data.PayrollDate.ToString("yyyyMMdd"),
            data.BaseQuincenal.ToString("0.00", CultureInfo.InvariantCulture),
            data.VariablePercent.ToString("0.0000", CultureInfo.InvariantCulture),
            data.Deductions.ToString("0.00", CultureInfo.InvariantCulture),
            data.Bonuses.ToString("0.00", CultureInfo.InvariantCulture),
            data.NetEstimated.ToString("0.00", CultureInfo.InvariantCulture));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes)[..16];
    }
}
