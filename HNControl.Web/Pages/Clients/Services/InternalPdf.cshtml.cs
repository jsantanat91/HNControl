using System.Security.Claims;
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

namespace HNControl.Web.Pages.Clients.Services;

[Authorize]
public class InternalPdfModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IActionAccessService _actions;

    public InternalPdfModel(ApplicationDbContext db, IActionAccessService actions)
    {
        _db = db;
        _actions = actions;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var contract = await _db.ClientServiceContracts
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (contract?.Client == null)
            return NotFound();

        if (!await CanAccessClientAsync(contract.ClientId))
            return Forbid();

        var pdf = Render(contract);
        var safeNumber = string.IsNullOrWhiteSpace(contract.ContractNumber) ? contract.Id.ToString("N")[..8] : contract.ContractNumber;
        return File(pdf, "application/pdf", $"contrato-interno-{safeNumber}.pdf");
    }

    private async Task<bool> CanAccessClientAsync(Guid clientId)
    {
        if (AppRoles.IsGlobalAdmin(User))
            return true;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        if (await _actions.HasActionAsync(User, AppActions.ClientsView))
            return true;

        if (!await _actions.HasActionAsync(User, AppActions.ClientsViewOwn))
            return false;

        return await _db.Clients
            .AsNoTracking()
            .AnyAsync(x => x.Id == clientId && x.OwnerUserId == userId);
    }

    private static byte[] Render(ClientServiceContract contract)
    {
        var tech = ClientServiceContractMetadata.ParseTechnical(contract.Notes);
        var serviceTypes = tech.ServiceTypes.Any() ? tech.ServiceTypes : [contract.ServiceType.ToString()];
        var notes = ClientServiceContractMetadata.StripMeta(contract.Notes);
        var recurrence = MetaValue(contract.Notes, "Recurrencia");
        var term = MetaValue(contract.Notes, "Plazo");
        var saleId = MetaValue(contract.Notes, "VentaId");
        var feasibilityId = MetaValue(contract.Notes, "FactibilidadId");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.Letter);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("HN Solutions").FontSize(20).Bold().FontColor(Colors.Blue.Medium);
                            left.Item().Text("Contrato interno de servicio").FontSize(12).FontColor(Colors.Grey.Darken2);
                        });

                        row.ConstantItem(210).AlignRight().Column(right =>
                        {
                            right.Item().Text(contract.ContractNumber ?? "-").Bold().FontSize(12);
                            right.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}");
                        });
                    });

                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Blue.Medium);
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Element(Card).Column(c =>
                    {
                        c.Item().Text("Datos generales").Bold().FontSize(12);
                        c.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                            });

                            Row(table, "Cliente", contract.Client?.Name);
                            Row(table, "Sucursal", contract.Branch);
                            Row(table, "Direccion sucursal", contract.BranchAddress);
                            Row(table, "Contrato", contract.Label);
                            Row(table, "Proveedor", contract.Provider);
                            Row(table, "Cuenta", contract.AccountNumber);
                            Row(table, "Numero de contrato", contract.ContractNumber);
                            Row(table, "Proyecto", contract.Project?.Title);
                        });
                    });

                    col.Item().Element(Card).Column(c =>
                    {
                        c.Item().Text("Servicios incluidos").Bold().FontSize(12);
                        c.Item().PaddingTop(8).Text(string.Join(", ", serviceTypes)).SemiBold();
                    });

                    col.Item().Element(Card).Column(c =>
                    {
                        c.Item().Text("Condiciones comerciales").Bold().FontSize(12);
                        c.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                            });

                            Row(table, "Monto mensual antes de IVA", (contract.MonthlyAmount ?? 0m).ToString("C2"));
                            Row(table, "Costo de instalacion antes de IVA", tech.InstallationCost.ToString("C2"));
                            Row(table, "Recurrencia", recurrence);
                            Row(table, "Tiempo de contrato", FormatTerm(term));
                            Row(table, "Inicio", contract.ContractStartDate?.ToString("dd/MM/yyyy"));
                            Row(table, "Vencimiento", contract.ContractEndDate?.ToString("dd/MM/yyyy"));
                            Row(table, "Venta relacionada", saleId);
                            Row(table, "Factibilidad origen", feasibilityId);
                        });
                    });

                    col.Item().Element(Card).Column(c =>
                    {
                        c.Item().Text("Ficha tecnica completa").Bold().FontSize(12);

                        if (serviceTypes.Contains("Internet", StringComparer.OrdinalIgnoreCase))
                        {
                            c.Item().PaddingTop(8).Text("Internet").Bold().FontColor(Colors.Blue.Medium);
                            c.Item().PaddingTop(4).Table(table =>
                            {
                                DefineTwoColumns(table);
                                Row(table, "Capacidad MB", tech.InternetCapacity);
                                Row(table, "Capacidad personalizada", tech.InternetCapacityOther);
                                Row(table, "Capacidad operativa", PickOther(tech.InternetCapacity, tech.InternetCapacityOther, "MB", appendSuffixForOther: false));
                            });
                        }

                        if (serviceTypes.Contains("Telefonia", StringComparer.OrdinalIgnoreCase))
                        {
                            c.Item().PaddingTop(10).Text("Telefonia").Bold().FontColor(Colors.Blue.Medium);
                            c.Item().PaddingTop(4).Table(table =>
                            {
                                DefineTwoColumns(table);
                                Row(table, "Extensiones", tech.TelephonyExtensions);
                                Row(table, "Troncales", tech.TelephonyTrunks);
                                Row(table, "DID", tech.TelephonyDids);
                            });
                        }

                        if (serviceTypes.Contains("CCTV", StringComparer.OrdinalIgnoreCase))
                        {
                            c.Item().PaddingTop(10).Text("CCTV").Bold().FontColor(Colors.Blue.Medium);
                            c.Item().PaddingTop(4).Table(table =>
                            {
                                DefineTwoColumns(table);
                                Row(table, "Canales", tech.CctvChannels);
                                Row(table, "Canales personalizados", tech.CctvChannelsOther);
                                Row(table, "Canales operativos", PickOther(tech.CctvChannels, tech.CctvChannelsOther, "canales", appendSuffixForOther: false));
                            });
                        }

                        if (serviceTypes.Contains("Seguridad", StringComparer.OrdinalIgnoreCase))
                        {
                            c.Item().PaddingTop(10).Text("Seguridad").Bold().FontColor(Colors.Blue.Medium);
                            c.Item().PaddingTop(4).Table(table =>
                            {
                                DefineTwoColumns(table);
                                Row(table, "Fabricante / plataforma", tech.SecurityBrand);
                                Row(table, "Detalle personalizado", tech.SecurityBrandOther);
                                Row(table, "Plataforma operativa", PickOther(tech.SecurityBrand, tech.SecurityBrandOther));
                            });
                        }

                        if (serviceTypes.Contains("Servidores", StringComparer.OrdinalIgnoreCase))
                        {
                            c.Item().PaddingTop(10).Text("Servidores").Bold().FontColor(Colors.Blue.Medium);
                            c.Item().PaddingTop(4).Table(table =>
                            {
                                DefineTwoColumns(table);
                                Row(table, "Sistema operativo", tech.ServerOs);
                                Row(table, "Nucleos", tech.ServerCpuCores);
                                Row(table, "RAM", tech.ServerRam);
                                Row(table, "Disco duro", tech.ServerDisk);
                            });
                        }

                        if (serviceTypes.Contains("Hardware", StringComparer.OrdinalIgnoreCase))
                        {
                            c.Item().PaddingTop(10).Text("Hardware").Bold().FontColor(Colors.Blue.Medium);
                            c.Item().PaddingTop(4).Text("El detalle de hardware se documenta en notas del contrato.");
                        }

                        if (serviceTypes.Contains("Otro", StringComparer.OrdinalIgnoreCase))
                        {
                            c.Item().PaddingTop(10).Text("Otro").Bold().FontColor(Colors.Blue.Medium);
                            c.Item().PaddingTop(4).Text("Servicio adicional documentado en notas del contrato.");
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(notes))
                    {
                        col.Item().Element(Card).Column(c =>
                        {
                            c.Item().Text("Notas").Bold().FontSize(12);
                            c.Item().PaddingTop(8).Text(notes);
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("HN Control - Documento interno operativo - ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static IContainer Card(IContainer container) =>
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.White).Padding(12);

    private static void Row(TableDescriptor table, string label, string? value)
    {
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4)
            .Text(label).FontColor(Colors.Grey.Darken2);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4)
            .Text(string.IsNullOrWhiteSpace(value) ? "-" : value.Trim()).SemiBold();
    }

    private static void DefineTwoColumns(TableDescriptor table)
    {
        table.ColumnsDefinition(cols =>
        {
            cols.RelativeColumn();
            cols.RelativeColumn();
        });
    }

    private static string MetaValue(string? notes, string key)
    {
        foreach (var line in (notes ?? string.Empty).Split('\n'))
        {
            var clean = line.Trim().TrimEnd('\r');
            if (!clean.StartsWith("[META]", StringComparison.OrdinalIgnoreCase))
                continue;

            var payload = clean[6..].Trim();
            var parts = payload.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && parts[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                return parts[1].Trim();
        }

        return string.Empty;
    }

    private static string FormatTerm(string? term)
    {
        var clean = (term ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(clean))
            return string.Empty;
        if (clean.Equals("Indefinido", StringComparison.OrdinalIgnoreCase))
            return "Indefinido";
        return clean.EndsWith("meses", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean} meses";
    }

    private static string PickOther(string value, string other, string suffix = "", bool appendSuffixForOther = true)
    {
        var isOther = string.Equals(value, "Otro", StringComparison.OrdinalIgnoreCase);
        var result = isOther ? other : value;
        if (string.IsNullOrWhiteSpace(result))
            return "-";
        return string.IsNullOrWhiteSpace(suffix) || (isOther && !appendSuffixForOther) || result.Contains(suffix, StringComparison.OrdinalIgnoreCase)
            ? result
            : $"{result} {suffix}";
    }
}
