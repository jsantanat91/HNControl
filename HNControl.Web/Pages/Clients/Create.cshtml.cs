using System.ComponentModel.DataAnnotations;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HNControl.Web.Pages.Clients;

[Authorize(Roles = AppRoles.Admin)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    public SelectList KindItems => new(Enum.GetValues<ClientKind>().Select(k => new { Id = k, Name = k.ToString() }), "Id", "Name");
    public ClientServiceType[] ServiceOptions => Enum.GetValues<ClientServiceType>();

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public ClientKind Kind { get; set; } = ClientKind.Moral;
        [Required, MaxLength(200)] public string Name { get; set; } = "";
        [EmailAddress, MaxLength(256)] public string Email { get; set; } = "";
        [MaxLength(40)] public string Phone { get; set; } = "";
        [MaxLength(400)] public string Address { get; set; } = "";

        // checkboxes
        public List<int> SelectedServices { get; set; } = new();
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var client = new Client
        {
            Kind = Input.Kind,
            Name = Input.Name.Trim(),
            Email = (Input.Email ?? "").Trim(),
            Phone = (Input.Phone ?? "").Trim(),
            Address = (Input.Address ?? "").Trim(),
            CreatedAt = DateTime.UtcNow
        };

        foreach (var s in Input.SelectedServices.Distinct())
        {
            client.Services.Add(new ClientService
            {
                ClientId = client.Id,
                ServiceType = (ClientServiceType)s
            });
        }

        _db.Clients.Add(client);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Clients/Index");
    }
}
