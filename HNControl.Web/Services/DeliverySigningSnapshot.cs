using System.Text.Json;
using HNControl.Web.Models;

namespace HNControl.Web.Services;

public sealed record DeliverySigningSnapshot(Client Client, string? ProjectTitle)
{
    public const int CurrentTemplateRevision = 2;
    public int? TemplateRevision { get; init; }

    public static string Capture(Client client, Project? project, bool useCurrentTemplate = true)
    {
        var frozenClient = JsonSerializer.Deserialize<ClientSigningSnapshot>(ClientSigningSnapshot.Capture(client, null))!.Client;
        return JsonSerializer.Serialize(new DeliverySigningSnapshot(frozenClient, project?.Title)
        {
            TemplateRevision = useCurrentTemplate ? CurrentTemplateRevision : 1
        });
    }
}
