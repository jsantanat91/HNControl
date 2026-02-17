using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Pages.Clients;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    public SelectList KindItems => new(Enum.GetValues<ClientKind>().Select(k => new { Id = k, Name = k.ToString() }), "Id", "Name");
    public ClientServiceType[] ServiceOptions => Enum.GetValues<ClientServiceType>();

    [BindProperty] public InputModel? Input { get; set; }
    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public Guid Id { get; set; }
        [Required] public ClientKind Kind { get; set; }
        [Required, MaxLength(200)] public string Name { get; set; } = "";
        [EmailAddress, MaxLength(256)] public string Email { get; set; } = "";
        [MaxLength(40)] public string Phone { get; set; } = "";
        [MaxLength(400)] public string Address { get; set; } = "";
        public List<int> SelectedServices { get; set; } = new();
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var client = await _db.Clients.Include(c => c.Services).FirstOrDefaultAsync(c => c.Id == id);
        if (client == null) return NotFound();

        Input = new InputModel
        {
            Id = client.Id,
            Kind = client.Kind,
            Name = client.Name,
            Email = client.Email,
            Phone = client.Phone,
            Address = client.Address,
            SelectedServices = client.Services.Select(s => (int)s.ServiceType).ToList()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input == null) return NotFound();
        if (!ModelState.IsValid) return Page();

        var client = await _db.Clients.Include(c => c.Services).FirstOrDefaultAsync(c => c.Id == Input.Id);
        if (client == null) return NotFound();

        client.Kind = Input.Kind;
        client.Name = Input.Name.Trim();
        client.Email = (Input.Email ?? "").Trim();
        client.Phone = (Input.Phone ?? "").Trim();
        client.Address = (Input.Address ?? "").Trim();

        // Replace services
        _db.ClientServices.RemoveRange(client.Services);
        client.Services.Clear();

        foreach (var s in Input.SelectedServices.Distinct())
        {
            client.Services.Add(new ClientService
            {
                ClientId = client.Id,
                ServiceType = (ClientServiceType)s
            });
        }

        await _db.SaveChangesAsync();
        return RedirectToPage("/Clients/Index");
    }
}
