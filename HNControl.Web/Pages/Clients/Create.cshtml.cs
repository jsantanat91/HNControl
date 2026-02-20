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

    public SelectList KindItems =>
        new(Enum.GetValues<ClientKind>().Select(k => new { Id = k, Name = k.ToString() }), "Id", "Name");

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public ClientKind Kind { get; set; } = ClientKind.Moral;

        [Required, MaxLength(200)]
        public string Name { get; set; } = "";

        [MaxLength(13)]
        public string Rfc { get; set; } = "";

        [EmailAddress, MaxLength(256)]
        public string Email { get; set; } = "";

        [MaxLength(40)]
        public string Phone { get; set; } = "";

        [MaxLength(400)]
        public string Address { get; set; } = "";
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var client = new Client
        {
            Kind = Input.Kind,
            Name = Input.Name.Trim(),
            Rfc = (Input.Rfc ?? "").Trim(),
            Email = (Input.Email ?? "").Trim(),
            Phone = (Input.Phone ?? "").Trim(),
            Address = (Input.Address ?? "").Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync();

        // ✅ Pro: al crear, te llevo a Detalles para que agregues contratos/servicios ya con el Id.
        return RedirectToPage("/Clients/Details", new { id = client.Id });
    }
}
