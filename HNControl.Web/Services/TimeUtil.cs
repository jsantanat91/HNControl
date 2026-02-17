using System;

namespace HNControl.Web.Services;

/// <summary>
/// Helpers para evitar el error clásico de Npgsql con timestamptz:
/// 'timestamp with time zone' solo acepta DateTime con Kind=Utc.
/// Para fechas tipo 'date' (solo día), marcamos 00:00:00 y Kind=Utc (sin convertir).
/// </summary>
public static class TimeUtil
{
    /// <summary>
    /// Devuelve la fecha (00:00:00) con Kind=Utc (no convierte huso, solo marca el Kind).
    /// Útil cuando el dato representa un día (quincena, SLA, etc).
    /// </summary>
    public static DateTime UtcDate(DateTime dt)
        => DateTime.SpecifyKind(dt.Date, DateTimeKind.Utc);

    public static DateTime? UtcDate(DateTime? dt)
        => dt.HasValue ? UtcDate(dt.Value) : null;

    /// <summary>
    /// Fuerza Kind=Utc para timestamps.
    /// Si llega Local, convierte a UTC.
    /// Si llega Unspecified, lo marca como UTC (asumiendo que ya viene en UTC).
    /// </summary>
    public static DateTime EnsureUtc(DateTime dt)
        => dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };

    public static DateTime? EnsureUtc(DateTime? dt)
        => dt.HasValue ? EnsureUtc(dt.Value) : null;
}
