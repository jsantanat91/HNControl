using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Security.Cryptography;
using System.Text.Json;

namespace HNControl.Web.Pages.Admin.Eval360.Results;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public Eval360Campaign Campaign { get; set; } = default!;
    public List<EmployeeProfile> Employees { get; set; } = new();
    public List<SelectListItem> CampaignItems { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    public record Row(string Competency, decimal AutoPct, decimal OthersPct, decimal DiffPct, string LevelAuto, string LevelOthers);
    public List<Row> Rows { get; set; } = new();

    public string LabelsJson { get; set; } = "[]";
    public string AutoJson { get; set; } = "[]";
    public string OthersJson { get; set; } = "[]";

    public int TeamEmployeesCount { get; set; } = 0;
    public string TeamLabelsJson { get; set; } = "[]";
    public string TeamExpectedJson { get; set; } = "[]";
    public string TeamEvalJson { get; set; } = "[]";

    public record ProgressRow(
        string UserId,
        string EmployeeName,
        int SelfAssigned,
        int SelfSubmitted,
        int OthersAssigned,
        int OthersSubmitted)
    {
        public decimal SelfProgressPct => SelfAssigned == 0 ? 100m : Math.Round((SelfSubmitted * 100m) / SelfAssigned, 0);
        public decimal OthersProgressPct => OthersAssigned == 0 ? 100m : Math.Round((OthersSubmitted * 100m) / OthersAssigned, 0);
        public decimal OverallProgressPct
        {
            get
            {
                var total = SelfAssigned + OthersAssigned;
                if (total == 0) return 100m;
                return Math.Round(((SelfSubmitted + OthersSubmitted) * 100m) / total, 0);
            }
        }

        public int MissingCount => (SelfAssigned - SelfSubmitted) + (OthersAssigned - OthersSubmitted);
    }

    public record CompetencyMetric(string Competency, decimal AvgScore, decimal Pct);
    public record PeerGivenMetric(string SubjectUserId, string SubjectName, int SubmittedAssignments, decimal AvgScore, decimal AvgPct);

    public class EmployeeSummary
    {
        public string UserId { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public decimal AutoOverallPct { get; set; }
        public decimal GivenOverallPct { get; set; }
        public List<CompetencyMetric> AutoCompetencies { get; set; } = new();
        public List<CompetencyMetric> TeamCompetencies { get; set; } = new();
        public List<PeerGivenMetric> GivenToPeers { get; set; } = new();
    }

    public record CrossAreaRow(
        string EvaluatorUserId,
        string EvaluatorName,
        string SubjectUserId,
        string SubjectName,
        string Competency,
        decimal AvgScore,
        decimal AvgPct,
        Eval360AssignmentStatus Status,
        bool IsSelf);

    public List<ProgressRow> ProgressRows { get; set; } = new();
    public List<EmployeeSummary> EmployeeSummaries { get; set; } = new();
    public List<CrossAreaRow> CrossAreaRows { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var campaigns = await _db.Eval360Campaigns
            .AsNoTracking()
            .OrderByDescending(x => x.PeriodStart ?? x.CreatedAt)
            .ToListAsync();

        CampaignItems = campaigns
            .Select(x =>
            {
                var ym = (x.PeriodStart ?? x.CreatedAt).ToString("yyyy-MM");
                var txt = $"{ym} - {x.Title}";
                return new SelectListItem(txt, x.Id.ToString(), x.Id == id);
            })
            .ToList();

        var c = campaigns.FirstOrDefault(x => x.Id == id);
        if (c == null) return RedirectToPage("/Admin/Eval360/Campaigns/Index");
        Campaign = c;

        Employees = await _db.EmployeeProfiles.AsNoTracking()
            .Where(e => !e.Email.ToLower().EndsWith("@hn.local"))
            .OrderBy(e => e.FullName)
            .ToListAsync();

        if (!Employees.Any())
        {
            TempData["Msg"] = "No hay empleados para mostrar.";
            return Page();
        }

        UserId ??= Employees.First().UserId;

        await BuildRowsAsync(id, UserId);
        await BuildTeamAsync(id);
        await BuildCampaignReportAsync(id);

        return Page();
    }

    public async Task<IActionResult> OnGetExportPdfAsync(Guid id)
    {
        Campaign = await _db.Eval360Campaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id) ?? new Eval360Campaign();

        if (Campaign.Id == Guid.Empty)
            return RedirectToPage("/Admin/Eval360/Campaigns/Index");

        Employees = await _db.EmployeeProfiles.AsNoTracking()
            .Where(e => !e.Email.ToLower().EndsWith("@hn.local"))
            .OrderBy(e => e.FullName)
            .ToListAsync();

        await BuildCampaignReportAsync(id);

        var period = $"{(Campaign.PeriodStart?.ToString("dd/MM/yyyy") ?? "-")} al {(Campaign.PeriodEnd?.ToString("dd/MM/yyyy") ?? "-")}";
        var signedAt = DateTime.Now;
        var signatureSeed = $"EVAL360|{Campaign.Id}|{Campaign.Title}|{signedAt:O}|{string.Join("|", ProgressRows.Select(x => $"{x.UserId}:{x.OverallProgressPct}"))}";
        var signatureHash = Convert.ToHexString(SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(signatureSeed)))[..24];
        var signatureLine = $"Firma digital HN Control  |  Token: {signatureHash}  |  Fecha: {signedAt:yyyy-MM-dd HH:mm}";

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(c =>
                {
                    c.Item().Text("HN Control - Reporte Eval360").FontSize(16).SemiBold();
                    c.Item().Text($"Campana: {Campaign.Title}").FontSize(11);
                    c.Item().Text($"Periodo: {period}").FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(8).Column(c =>
                {
                    c.Spacing(10);

                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(cc =>
                    {
                        cc.Item().Text("Resumen inicial por empleado").SemiBold();
                        cc.Item().Text("Avance y faltantes agrupados por persona.").FontColor(Colors.Grey.Darken2);
                    });

                    foreach (var p in ProgressRows.OrderBy(x => x.EmployeeName))
                    {
                        c.Item().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(7).Row(r =>
                        {
                            r.RelativeItem().Text(p.EmployeeName).SemiBold();
                            r.ConstantItem(260).AlignCenter().Text($"Auto {p.SelfProgressPct}%  |  Equipo {p.OthersProgressPct}%  |  Global {p.OverallProgressPct}%");
                            r.ConstantItem(90).AlignRight().Text($"Faltan: {p.MissingCount}");
                        });
                    }

                    c.Item().Text("Trazabilidad por area (agrupada por evaluador)").SemiBold();
                    var groupedTrace = CrossAreaRows
                        .Where(x => !x.IsSelf)
                        .GroupBy(x => new { x.EvaluatorUserId, x.EvaluatorName })
                        .OrderBy(g => g.Key.EvaluatorName)
                        .ToList();

                    foreach (var grp in groupedTrace)
                    {
                        c.Item().Text(grp.Key.EvaluatorName).SemiBold();
                        c.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });

                            t.Header(h =>
                            {
                                h.Cell().Element(CellHead).Text("Evaluado");
                                h.Cell().Element(CellHead).Text("Area");
                                h.Cell().Element(CellHead).AlignRight().Text("Prom");
                                h.Cell().Element(CellHead).AlignRight().Text("%");
                            });

                            foreach (var r in grp.OrderBy(x => x.SubjectName).ThenBy(x => x.Competency))
                            {
                                t.Cell().Element(CellBody).Text(r.SubjectName);
                                t.Cell().Element(CellBody).Text(r.Competency);
                                t.Cell().Element(CellBody).AlignRight().Text(r.AvgScore.ToString("0.00"));
                                t.Cell().Element(CellBody).AlignRight().Text($"{r.AvgPct}%");
                            }
                        });
                    }
                });

                page.Footer().Column(f =>
                {
                    f.Item().AlignRight().Text($"Generado: {signedAt:yyyy-MM-dd HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken2);
                    f.Item().AlignRight().Text(signatureLine).FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });

            foreach (var s in EmployeeSummaries)
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().Column(c =>
                    {
                        c.Item().Text("Resumen por empleado").FontSize(14).SemiBold();
                        c.Item().Text($"{s.EmployeeName} - {Campaign.Title}").FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().PaddingTop(8).Column(c =>
                    {
                        c.Spacing(10);

                        c.Item().Text("Grafica comparativa por competencia (Auto vs Otros)").SemiBold();
                        c.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });

                            t.Header(h =>
                            {
                                h.Cell().Element(CellHead).Text("Area");
                                h.Cell().Element(CellHead).Text("Auto");
                                h.Cell().Element(CellHead).Text("Otros");
                                h.Cell().Element(CellHead).AlignRight().Text("Auto %");
                                h.Cell().Element(CellHead).AlignRight().Text("Otros %");
                            });

                            var areas = s.AutoCompetencies.Select(x => x.Competency)
                                .Union(s.TeamCompetencies.Select(x => x.Competency))
                                .Distinct()
                                .OrderBy(x => x)
                                .ToList();

                            foreach (var area in areas)
                            {
                                var autoPct = s.AutoCompetencies.FirstOrDefault(x => x.Competency == area)?.Pct ?? 0m;
                                var teamPct = s.TeamCompetencies.FirstOrDefault(x => x.Competency == area)?.Pct ?? 0m;

                                t.Cell().Element(CellBody).Text(area);
                                t.Cell().Element(CellBody).Element(x => PercentageBar(x, autoPct, "#10B981"));
                                t.Cell().Element(CellBody).Element(x => PercentageBar(x, teamPct, "#3B82F6"));
                                t.Cell().Element(CellBody).AlignRight().Text($"{autoPct}%");
                                t.Cell().Element(CellBody).AlignRight().Text($"{teamPct}%");
                            }
                        });

                        c.Item().Text("Autoevaluacion del empleado (como se califico en cada rubro)").SemiBold();
                        c.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });

                            t.Header(h =>
                            {
                                h.Cell().Element(CellHead).Text("Area");
                                h.Cell().Element(CellHead).AlignRight().Text("Prom");
                                h.Cell().Element(CellHead).AlignRight().Text("% ");
                            });

                            if (s.AutoCompetencies.Any())
                            {
                                foreach (var a in s.AutoCompetencies)
                                {
                                    t.Cell().Element(CellBody).Text(a.Competency);
                                    t.Cell().Element(CellBody).AlignRight().Text(a.AvgScore.ToString("0.00"));
                                    t.Cell().Element(CellBody).AlignRight().Text($"{a.Pct}%");
                                }
                            }
                            else
                            {
                                t.Cell().Element(CellBody).Text("Sin autoevaluacion enviada");
                                t.Cell().Element(CellBody).Text("-");
                                t.Cell().Element(CellBody).Text("-");
                            }
                        });
                        c.Item().AlignRight().Text($"Autoevaluacion global del empleado: {s.AutoOverallPct}%").SemiBold();

                        c.Item().Text("Evaluaciones a companeros (resumen)").SemiBold();
                        c.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });

                            t.Header(h =>
                            {
                                h.Cell().Element(CellHead).Text("Companero evaluado");
                                h.Cell().Element(CellHead).AlignRight().Text("Asign.");
                                h.Cell().Element(CellHead).AlignRight().Text("Prom");
                                h.Cell().Element(CellHead).AlignRight().Text("% ");
                            });

                            if (s.GivenToPeers.Any())
                            {
                                foreach (var g in s.GivenToPeers)
                                {
                                    t.Cell().Element(CellBody).Text(g.SubjectName);
                                    t.Cell().Element(CellBody).AlignRight().Text(g.SubmittedAssignments.ToString());
                                    t.Cell().Element(CellBody).AlignRight().Text(g.AvgScore.ToString("0.00"));
                                    t.Cell().Element(CellBody).AlignRight().Text($"{g.AvgPct}%");
                                }
                            }
                            else
                            {
                                t.Cell().Element(CellBody).Text("Sin evaluaciones a companeros");
                                t.Cell().Element(CellBody).Text("-");
                                t.Cell().Element(CellBody).Text("-");
                                t.Cell().Element(CellBody).Text("-");
                            }
                        });
                        c.Item().AlignRight().Text($"Promedio global evaluando a companeros: {s.GivenOverallPct}%").SemiBold();
                    });

                    page.Footer().Column(f =>
                    {
                        f.Item().AlignRight().Text($"Generado: {signedAt:yyyy-MM-dd HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken2);
                        f.Item().AlignRight().Text(signatureLine).FontSize(8).FontColor(Colors.Grey.Darken2);
                    });
                });
            }
        }).GeneratePdf();

        var file = $"eval360_reporte_{(Campaign.PeriodStart ?? Campaign.CreatedAt):yyyyMM}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
        return File(pdf, "application/pdf", file);
    }

    private static IContainer CellHead(IContainer c) =>
        c.Background(Colors.Grey.Lighten4)
            .Border(1).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(4).PaddingHorizontal(6)
            .DefaultTextStyle(x => x.SemiBold().FontSize(9).FontColor(Colors.Grey.Darken2));

    private static IContainer CellBody(IContainer c) =>
        c.Border(1).BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(5).PaddingHorizontal(6);

    private static IContainer PercentageBar(IContainer c, decimal pct, string colorHex)
    {
        var clamped = Math.Max(0m, Math.Min(100m, pct));
        var fill = (float)(clamped * 1.15m); // max ~115pt

        return c.PaddingVertical(2).Element(x =>
        {
            var bar = x
                .Width(115)
                .Height(9)
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background("#E5E7EB");

            bar.Row(r =>
            {
                if (fill > 0.1f)
                    r.ConstantItem(fill).Background(colorHex);
                r.RelativeItem();
            });

            return bar;
        });
    }

    private static string Level(decimal pct)
    {
        if (pct <= 0) return "Sin datos";
        if (pct < 50) return "Critico";
        if (pct < 75) return "Alto";
        if (pct < 85) return "Moderado";
        return "Desarrollado";
    }

    private async Task BuildRowsAsync(Guid campaignId, string subjectUserId)
    {
        Rows.Clear();

        var answers = await _db.Eval360Answers
            .AsNoTracking()
            .Where(a => a.Assignment!.CampaignId == campaignId
                        && a.Assignment.SubjectUserId == subjectUserId
                        && a.Assignment.Status == Eval360AssignmentStatus.Submitted)
            .Select(a => new
            {
                a.Assignment!.IsSelf,
                Competency = a.Question!.Competency!.Name,
                Score = a.Score
            })
            .ToListAsync();

        var comps = await _db.Eval360Competencies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Name)
            .ToListAsync();

        foreach (var comp in comps)
        {
            var compAnswers = answers.Where(x => x.Competency == comp).ToList();

            decimal expectedAvg = 0m;
            decimal evalAvg = 0m;

            var aSelf = compAnswers.Where(x => x.IsSelf).Select(x => (decimal)x.Score).ToList();
            var aOth = compAnswers.Where(x => !x.IsSelf).Select(x => (decimal)x.Score).ToList();

            if (aSelf.Any()) expectedAvg = aSelf.Average();
            if (aOth.Any()) evalAvg = aOth.Average();

            var expectedPct = Math.Round((expectedAvg / 5m) * 100m, 0);
            var evalPct = Math.Round((evalAvg / 5m) * 100m, 0);
            var diffPct = Math.Round(expectedPct - evalPct, 0);

            Rows.Add(new Row(comp, expectedPct, evalPct, diffPct, Level(expectedPct), Level(evalPct)));
        }

        LabelsJson = JsonSerializer.Serialize(Rows.Select(r => r.Competency));
        AutoJson = JsonSerializer.Serialize(Rows.Select(r => (double)r.AutoPct));
        OthersJson = JsonSerializer.Serialize(Rows.Select(r => (double)r.OthersPct));
    }

    private async Task BuildTeamAsync(Guid campaignId)
    {
        var raw = await _db.Eval360Answers
            .AsNoTracking()
            .Where(a => a.Assignment!.CampaignId == campaignId
                        && a.Assignment.Status == Eval360AssignmentStatus.Submitted)
            .Select(a => new
            {
                a.Assignment!.SubjectUserId,
                a.Assignment!.IsSelf,
                Competency = a.Question!.Competency!.Name,
                Score = a.Score
            })
            .ToListAsync();

        if (!raw.Any())
        {
            TeamEmployeesCount = 0;
            TeamLabelsJson = TeamExpectedJson = TeamEvalJson = "[]";
            return;
        }

        var comps = await _db.Eval360Competencies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Name)
            .ToListAsync();

        var subjects = raw.Select(x => x.SubjectUserId).Distinct().ToList();
        TeamEmployeesCount = subjects.Count;

        var teamLabels = new List<string>();
        var teamExpected = new List<double>();
        var teamEval = new List<double>();

        foreach (var comp in comps)
        {
            var bySubject = raw.Where(x => x.Competency == comp).GroupBy(x => x.SubjectUserId);

            var expectedAvgs = new List<decimal>();
            var evalAvgs = new List<decimal>();

            foreach (var g in bySubject)
            {
                var selfScores = g.Where(x => x.IsSelf).Select(x => (decimal)x.Score).ToList();
                var othScores = g.Where(x => !x.IsSelf).Select(x => (decimal)x.Score).ToList();

                if (selfScores.Any()) expectedAvgs.Add(selfScores.Average());
                if (othScores.Any()) evalAvgs.Add(othScores.Average());
            }

            var expectedAvg = expectedAvgs.Any() ? expectedAvgs.Average() : 0m;
            var evalAvg = evalAvgs.Any() ? evalAvgs.Average() : 0m;

            var expectedPct = Math.Round((expectedAvg / 5m) * 100m, 0);
            var evalPct = Math.Round((evalAvg / 5m) * 100m, 0);

            teamLabels.Add(comp);
            teamExpected.Add((double)expectedPct);
            teamEval.Add((double)evalPct);
        }

        TeamLabelsJson = JsonSerializer.Serialize(teamLabels);
        TeamExpectedJson = JsonSerializer.Serialize(teamExpected);
        TeamEvalJson = JsonSerializer.Serialize(teamEval);
    }

    private async Task BuildCampaignReportAsync(Guid campaignId)
    {
        ProgressRows.Clear();
        EmployeeSummaries.Clear();
        CrossAreaRows.Clear();

        var assignments = await _db.Eval360Assignments
            .AsNoTracking()
            .Where(a => a.CampaignId == campaignId)
            .Select(a => new
            {
                a.Id,
                a.EvaluatorUserId,
                a.SubjectUserId,
                a.IsSelf,
                a.Status
            })
            .ToListAsync();

        if (!assignments.Any())
            return;

        var userIds = assignments
            .SelectMany(a => new[] { a.EvaluatorUserId, a.SubjectUserId })
            .Distinct()
            .ToList();

        var names = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(e => userIds.Contains(e.UserId))
            .Select(e => new { e.UserId, e.FullName, e.Email })
            .ToListAsync();

        var nameMap = names.ToDictionary(x => x.UserId, x => string.IsNullOrWhiteSpace(x.FullName) ? x.Email : x.FullName);

        var answerRows = await _db.Eval360Answers
            .AsNoTracking()
            .Where(a => a.Assignment!.CampaignId == campaignId)
            .Select(a => new
            {
                a.AssignmentId,
                a.Assignment!.EvaluatorUserId,
                a.Assignment!.SubjectUserId,
                a.Assignment!.IsSelf,
                a.Assignment!.Status,
                Competency = a.Question!.Competency!.Name,
                Score = (decimal)a.Score
            })
            .ToListAsync();

        ProgressRows = assignments
            .GroupBy(a => a.EvaluatorUserId)
            .Select(g => new ProgressRow(
                g.Key,
                nameMap.TryGetValue(g.Key, out var n) ? n : g.Key,
                g.Count(x => x.IsSelf),
                g.Count(x => x.IsSelf && x.Status == Eval360AssignmentStatus.Submitted),
                g.Count(x => !x.IsSelf),
                g.Count(x => !x.IsSelf && x.Status == Eval360AssignmentStatus.Submitted)
            ))
            .OrderBy(x => x.EmployeeName)
            .ToList();

        var subjectIds = assignments.Select(x => x.SubjectUserId).Distinct().OrderBy(x => nameMap.TryGetValue(x, out var n) ? n : x).ToList();

        foreach (var subjectId in subjectIds)
        {
            var subjectName = nameMap.TryGetValue(subjectId, out var n) ? n : subjectId;

            var autoAnswers = answerRows
                .Where(x => x.SubjectUserId == subjectId && x.IsSelf && x.Status == Eval360AssignmentStatus.Submitted)
                .ToList();

            var autoComp = autoAnswers
                .GroupBy(x => x.Competency)
                .Select(g =>
                {
                    var avg = g.Average(x => x.Score);
                    return new CompetencyMetric(g.Key, Math.Round(avg, 2), Math.Round((avg / 5m) * 100m, 0));
                })
                .OrderBy(x => x.Competency)
                .ToList();

            var teamForSubject = answerRows
                .Where(x => x.SubjectUserId == subjectId && !x.IsSelf && x.Status == Eval360AssignmentStatus.Submitted)
                .GroupBy(x => x.Competency)
                .Select(g =>
                {
                    var avg = g.Average(x => x.Score);
                    return new CompetencyMetric(g.Key, Math.Round(avg, 2), Math.Round((avg / 5m) * 100m, 0));
                })
                .OrderBy(x => x.Competency)
                .ToList();

            var givenAnswers = answerRows
                .Where(x => x.EvaluatorUserId == subjectId && !x.IsSelf && x.Status == Eval360AssignmentStatus.Submitted)
                .ToList();

            var givenByPeer = givenAnswers
                .GroupBy(x => x.SubjectUserId)
                .Select(g =>
                {
                    var avg = g.Average(x => x.Score);
                    return new PeerGivenMetric(
                        g.Key,
                        nameMap.TryGetValue(g.Key, out var peerName) ? peerName : g.Key,
                        g.Select(x => x.AssignmentId).Distinct().Count(),
                        Math.Round(avg, 2),
                        Math.Round((avg / 5m) * 100m, 0));
                })
                .OrderBy(x => x.SubjectName)
                .ToList();

            var autoOverall = autoAnswers.Any() ? Math.Round((autoAnswers.Average(x => x.Score) / 5m) * 100m, 0) : 0m;
            var givenOverall = givenAnswers.Any() ? Math.Round((givenAnswers.Average(x => x.Score) / 5m) * 100m, 0) : 0m;

            EmployeeSummaries.Add(new EmployeeSummary
            {
                UserId = subjectId,
                EmployeeName = subjectName,
                AutoOverallPct = autoOverall,
                GivenOverallPct = givenOverall,
                AutoCompetencies = autoComp,
                TeamCompetencies = teamForSubject,
                GivenToPeers = givenByPeer
            });
        }

        CrossAreaRows = answerRows
            .Where(x => x.Status == Eval360AssignmentStatus.Submitted)
            .GroupBy(x => new { x.EvaluatorUserId, x.SubjectUserId, x.Competency, x.Status, x.IsSelf })
            .Select(g =>
            {
                var avg = g.Average(x => x.Score);
                return new CrossAreaRow(
                    g.Key.EvaluatorUserId,
                    nameMap.TryGetValue(g.Key.EvaluatorUserId, out var evaluatorName) ? evaluatorName : g.Key.EvaluatorUserId,
                    g.Key.SubjectUserId,
                    nameMap.TryGetValue(g.Key.SubjectUserId, out var subjectName) ? subjectName : g.Key.SubjectUserId,
                    g.Key.Competency,
                    Math.Round(avg, 2),
                    Math.Round((avg / 5m) * 100m, 0),
                    g.Key.Status,
                    g.Key.IsSelf);
            })
            .OrderBy(x => x.EvaluatorName)
            .ThenBy(x => x.SubjectName)
            .ThenBy(x => x.Competency)
            .ToList();
    }
}
