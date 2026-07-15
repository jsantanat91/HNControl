namespace HNControl.Web.Services;

/// <summary>
/// Hora de negocio de HN Control (America/Mexico_City), independiente de la zona
/// horaria del contenedor. Usar para "hoy"/"ahora" de lógica de negocio
/// (nómina, periodos, deducciones). Para timestamps de auditoría seguir usando UtcNow.
/// </summary>
public static class AppTime
{
    private static readonly TimeZoneInfo MxZone = ResolveMxZone();

    private static TimeZoneInfo ResolveMxZone()
    {
        // IANA en Linux; el segundo id es el equivalente en Windows.
        foreach (var id in new[] { "America/Mexico_City", "Central Standard Time (Mexico)" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* probar siguiente */ }
        }
        return TimeZoneInfo.Utc;
    }

    /// <summary>Fecha y hora actual en México.</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MxZone);

    /// <summary>Fecha actual (sin hora) en México.</summary>
    public static DateTime Today => Now.Date;

    /// <summary>Convierte un UTC a hora de México.</summary>
    public static DateTime ToMx(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), MxZone);
}
