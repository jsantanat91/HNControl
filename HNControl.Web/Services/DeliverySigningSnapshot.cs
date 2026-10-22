using System.Text.Json;
using HNControl.Web.Models;

namespace HNControl.Web.Services;

public sealed record DeliverySigningSnapshot(Client Client, string? ProjectTitle)
{
    public static string Capture(Client client, Project? project)
    {
        var frozenClient = JsonSerializer.Deserialize<ClientSigningSnapshot>(ClientSigningSnapshot.Capture(client, null))!.Client;
        return JsonSerializer.Serialize(new DeliverySigningSnapshot(frozenClient, project?.Title));
    }
}
