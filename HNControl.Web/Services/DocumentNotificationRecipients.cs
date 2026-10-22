using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public class DocumentNotificationRecipients(ApplicationDbContext db)
{
    public static IReadOnlyCollection<string> Parse(string? value)
    {
        var addresses = (value ?? "").Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (addresses.Any(x => !ClientCommunicationTemplates.IsEmail(x)))
            throw new ArgumentException("Ingrese direcciones de correo válidas, separadas por renglón, coma o punto y coma.");
        return addresses;
    }

    public async Task<IReadOnlyCollection<string>> GetAsync()
    {
        var value = await db.SystemConfigurations.AsNoTracking().OrderByDescending(x => x.UpdatedAt)
            .Select(x => x.DocumentNotificationEmails).FirstOrDefaultAsync();
        // An explicitly empty list means no internal copies; only an absent configuration uses defaults.
        return Parse(value ?? SystemConfiguration.DefaultDocumentNotificationEmails);
    }
}
