using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients.Services;

[Authorize(Roles = AppRoles.Admin)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public CreateModel(ApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [BindProperty(SupportsGet = true)]
    public Guid ClientId { get; set; }

    public string ClientName { get; set; } = "";

    public SelectList ServiceTypeItems =>
        new(Enum.GetValues<ClientServiceType>().Select(x => new { Id = x, Name = x.ToString() }), "Id", "Name");

    public SelectList ProjectItems { get; set; } = default!;

    [BindProperty] public InputModel Input { get; set; } = new();

    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public ClientServiceType ServiceType { get; set; } = ClientServiceType.Internet;

        [Required, MaxLength(200)]
        public string Label { get; set; } = "";

        [MaxLength(120)]
        public string Provider { get; set; } = "";

        [MaxLength(120)]
        public string AccountNumber { get; set; } = "";

        [MaxLength(120)]
        public string ContractNumber { get; set; } = "";

        [DataType(DataType.Date)]
        public DateTime? ContractStartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ContractEndDate { get; set; }

        public Guid? ProjectId { get; set; }

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        public IFormFile? SignedContract { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == ClientId);
        if (client == null) return NotFound();

        ClientName = client.Name;
        await LoadProjectsAsync();

        if (Input.ContractStartDate == null)
            Input.ContractStartDate = DateTime.Today;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == ClientId);
        if (client == null) return NotFound();

        ClientName = client.Name;
        await LoadProjectsAsync();

        if (!ModelState.IsValid) return Page();

        var contract = new ClientServiceContract
        {
            ClientId = ClientId,
            ProjectId = Input.ProjectId,
            ServiceType = Input.ServiceType,
            Label = (Input.Label ?? "").Trim(),
            Provider = (Input.Provider ?? "").Trim(),
            AccountNumber = (Input.AccountNumber ?? "").Trim(),
            ContractNumber = (Input.ContractNumber ?? "").Trim(),
            ContractStartDate = Input.ContractStartDate?.Date,
            ContractEndDate = Input.ContractEndDate?.Date,
            Notes = (Input.Notes ?? "").Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ClientServiceContracts.Add(contract);
        await _db.SaveChangesAsync();

        if (Input.SignedContract != null && Input.SignedContract.Length > 0)
        {
            try
            {
                var (path, size, contentType, original) = await _storage.SaveFileAsync(
                    Input.SignedContract,
                    subFolder: $"client-contracts/{ClientId}",
                    fileNameNoExt: $"contract_{contract.Id}",
                    allowedExtensions: new[] { ".pdf" },
                    maxBytes: 20L * 1024L * 1024L);

                contract.SignedContractStoragePath = path;
                contract.SignedContractSizeBytes = size;
                contract.SignedContractContentType = contentType;
                contract.SignedContractOriginalFileName = original;
                contract.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                return Page();
            }
        }

        return RedirectToPage("/Clients/Details", new { id = ClientId });
    }

    private async Task LoadProjectsAsync()
    {
        var projs = await _db.Projects
            .Where(p => p.ClientId == ClientId)
            .OrderByDescending(p => p.StartDate)
            .Select(p => new { p.Id, p.Title })
            .ToListAsync();

        ProjectItems = new SelectList(projs, "Id", "Title");
    }
}
