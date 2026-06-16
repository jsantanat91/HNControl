using System.Globalization;

namespace HNControl.Web.Services;

public static class WhatsAppTemplateRenderer
{
    private static readonly CultureInfo Mexico = new("es-MX");

    public static string Render(string? template, string fallback, IReadOnlyDictionary<string, string?> tokens)
    {
        var message = string.IsNullOrWhiteSpace(template) ? fallback : template.Trim();

        foreach (var token in tokens)
            message = message.Replace("{" + token.Key + "}", token.Value ?? "", StringComparison.OrdinalIgnoreCase);

        return message.Trim();
    }

    public static string Money(decimal value)
        => value.ToString("C2", Mexico);
}
