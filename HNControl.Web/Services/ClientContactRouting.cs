using System.Text.Json;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public sealed class ClientContactSelection
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string? Saved { get; set; }
    public bool Configured { get; set; }
    public Guid? PrimaryContactId { get; set; }
    public List<Guid> CopyContactIds { get; set; } = new();
}
public sealed record RoutedContact(Guid? Id, string Name, string Email);
public sealed record ClientContactRoute(Guid ClientId, RoutedContact Primary, IReadOnlyList<RoutedContact> Copies);
public sealed record ContactOption(string Id, string Name, string Email);
public sealed record ContactOptions(IReadOnlyList<ContactOption> Options, ClientContactSelection Selection, Guid ClientId);
public sealed record ContactSelectorModel(Guid ClientId, ClientContactSelection Selection, string Prefix,
    string? ClientSelector = null, string? ContractSelector = null, bool Locked = false);

/// <summary>Each document keeps its recipient snapshot, independent of later address-book edits.</summary>
public static class ClientContactRouting
{
    public static ClientContactRoute? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var value = JsonSerializer.Deserialize<ClientContactRoute>(json);
        if (value?.Primary == null || value.Copies == null)
            throw new InvalidOperationException("Los contactos guardados no son válidos. Revise los destinatarios.");
        return value;
    }

    public static ClientContactRoute Resolve(string? json, Client client)
    {
        var saved = Read(json);
        if (saved != null && saved.ClientId != client.Id)
            throw new InvalidOperationException("Los contactos no pertenecen al cliente de este documento.");
        return saved ?? new(client.Id, new(null,
            new[] { client.LegalRepresentative, client.ContactName, client.Name }.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Cliente",
            ClientCommunicationTemplates.Recipient(client)), Array.Empty<RoutedContact>());
    }

    public static ClientContactSelection Selection(string? json)
    {
        var route = Read(json);
        return new() { Saved = json, Configured = route != null, PrimaryContactId = route?.Primary.Id,
            CopyContactIds = route?.Copies.Where(c => c.Id.HasValue).Select(c => c.Id!.Value).ToList() ?? new() };
    }

    public static IReadOnlyCollection<string> Copies(ClientContactRoute route, IEnumerable<string> internalCopies) =>
        route.Copies.Select(c => c.Email).Concat(internalCopies).Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.Equals(route.Primary.Email, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static async Task<string?> CaptureAsync(ApplicationDbContext db, Guid clientId, ClientContactSelection selection, string? existing = null)
    {
        if (!selection.Configured)
        {
            if (Read(existing) is { } old && old.ClientId != clientId)
                throw new ArgumentException("Seleccione nuevamente los contactos del cliente.");
            return existing;
        }
        var client = await db.Clients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == clientId)
            ?? throw new ArgumentException("Seleccione un cliente válido.");
        var ids = (selection.CopyContactIds ?? new()).Distinct().ToList();
        if (ids.Count > 100) throw new ArgumentException("Seleccione como máximo 100 contactos en copia.");
        var primaryId = selection.PrimaryContactId == Guid.Empty ? null : selection.PrimaryContactId;
        if (Read(existing) is { } stored && stored.ClientId == clientId && stored.Primary.Id == primaryId
            && stored.Copies.Select(c => c.Id!.Value).ToHashSet().SetEquals(ids.Where(id => id != primaryId))) return existing;
        var wanted = ids.Concat(primaryId.HasValue ? new[] { primaryId.Value } : Array.Empty<Guid>()).Distinct().ToArray();
        var contacts = await db.ClientContacts.AsNoTracking().Where(c => c.ClientId == clientId && wanted.Contains(c.Id)).ToListAsync();
        if (contacts.Count != wanted.Length) throw new ArgumentException("Uno de los contactos no pertenece al cliente o fue eliminado. Revise la selección.");
        RoutedContact Convert(ClientContact c) => new(c.Id, c.Name.Trim(), c.Email.Trim());
        var main = primaryId.HasValue ? Convert(contacts.Single(c => c.Id == primaryId)) : Resolve(null, client).Primary;
        var copies = ids.Where(id => id != primaryId).Select(id => Convert(contacts.Single(c => c.Id == id)))
            .Where(c => !c.Email.Equals(main.Email, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(c => c.Email, StringComparer.OrdinalIgnoreCase).ToArray();
        if (main.Name.Length is < 2 or > 200 || !ClientCommunicationTemplates.IsEmail(main.Email)
            || copies.Any(c => !ClientCommunicationTemplates.IsEmail(c.Email)))
            throw new ArgumentException("El contacto principal y todos los contactos en copia deben tener un correo válido. Complételos en la ficha del cliente.");
        return JsonSerializer.Serialize(new ClientContactRoute(clientId, main, copies));
    }

    public static async Task<ContactOptions> OptionsAsync(ApplicationDbContext db, Guid clientId, string? saved = null)
    {
        var client = await db.Clients.AsNoTracking().SingleOrDefaultAsync(c => c.Id == clientId);
        var contacts = await db.ClientContacts.AsNoTracking().Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Name).ToListAsync();
        var fallback = client == null ? new RoutedContact(null, "Seleccione un cliente", "") : Resolve(null, client).Primary;
        var options = new[] { new ContactOption("", "Ficha del cliente · " + fallback.Name, fallback.Email) }
            .Concat(contacts.Select(c => new ContactOption(c.Id.ToString(), c.Name, c.Email))).ToList();
        // Preserve the stored names/addresses in the initial display, even if the address book changed.
        if (Read(saved) is { } route && route.ClientId == clientId)
            foreach (var c in route.Copies.Prepend(route.Primary))
            {
                var id = c.Id?.ToString() ?? "";
                options.RemoveAll(o => o.Id == id);
                options.Add(new ContactOption(id, c.Name + " (guardado)", c.Email));
            }
        return new(options, Selection(saved), clientId);
    }
}
