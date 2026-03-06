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

    public SelectList KindItems =>
        new(Enum.GetValues<ClientKind>().Select(k => new { Id = k, Name = k.ToString() }), "Id", "Name");

    [BindProperty] public InputModel? Input { get; set; }
    public string? Error { get; set; }

    public class InputModel
    {
        [Required] public Guid Id { get; set; }
        public string ClientCode { get; set; } = "";

        [Required] public ClientKind Kind { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = "";

        [MaxLength(13)]
        public string Rfc { get; set; } = "";

        [EmailAddress, MaxLength(256)]
        public string Email { get; set; } = "";

        [MaxLength(40)]
        public string Phone { get; set; } = "";

        [MaxLength(120)]
        public string ContactName { get; set; } = "";

        [MaxLength(400)]
        public string Address { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id);
        if (client == null) return NotFound();

        Input = new InputModel
        {
            Id = client.Id,
            ClientCode = client.ClientCode,
            Kind = client.Kind,
            Name = client.Name,
            Rfc = client.Rfc ?? "",
            Email = client.Email ?? "",
            Phone = client.Phone ?? "",
            ContactName = client.ContactName ?? "",
            Address = client.Address ?? ""
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input == null) return NotFound();
        if (!ModelState.IsValid) return Page();

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == Input.Id);
        if (client == null) return NotFound();

        client.Type = (ClientType)Input.Kind;
        client.Name = Input.Name.Trim();
        client.Rfc = (Input.Rfc ?? "").Trim();
        client.Email = (Input.Email ?? "").Trim();
        client.Phone = (Input.Phone ?? "").Trim();
        client.ContactName = (Input.ContactName ?? "").Trim();
        client.Address = (Input.Address ?? "").Trim();

        await _db.SaveChangesAsync();
        return RedirectToPage("/Clients/Details", new { id = client.Id });
    }
}
