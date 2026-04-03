using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Projects.Delivery;

[Authorize(Policy = "EmployeeOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public record Row(Guid Id, string Title, string Client, string Project, string DeliveryDate, string Status, string Receiver, bool HasPdf);
    public List<Row> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        Rows = await _db.ProjectDeliveryFormats
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.Project)
            .OrderByDescending(x => x.CreatedAt)
            .Take(300)
            .Select(x => new Row(
                x.Id,
                x.Title,
                x.Client != null ? x.Client.Name : "-",
                x.Project != null ? x.Project.Title : "-",
                x.DeliveryDate.ToString("yyyy-MM-dd"),
                x.Status == ProjectDeliveryFormatStatus.Draft ? "Borrador" :
                x.Status == ProjectDeliveryFormatStatus.SentForSignature ? "En firma" : "Firmado",
                x.ReceiverName,
                !string.IsNullOrWhiteSpace(x.PdfStoragePath)
            ))
            .ToListAsync();
    }
}

