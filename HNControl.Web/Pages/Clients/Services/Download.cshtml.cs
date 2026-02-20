using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients.Services;

[Authorize(Roles = AppRoles.Admin)]
public class DownloadModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public DownloadModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var contract = await _db.ClientServiceContracts
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (contract == null) return NotFound();
        if (string.IsNullOrWhiteSpace(contract.SignedContractStoragePath)) return NotFound();

        var downloadName = string.IsNullOrWhiteSpace(contract.SignedContractOriginalFileName)
            ? $"Contrato_{contract.Client?.Name}_{contract.Label}.pdf".Replace(' ', '_')
            : contract.SignedContractOriginalFileName;

        var (stream, contentType, name) = await _storage.OpenAsync(contract.SignedContractStoragePath, downloadName);
        return File(stream, contentType, name);
    }
}
