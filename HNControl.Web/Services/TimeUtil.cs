namespace HNControl.Web.Services;

public static class TimeUtil
{
    public static DateTime UtcDate(DateTime d)
        => DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);

    public static DateTime UtcDateTime(DateTime d)
        => DateTime.SpecifyKind(d, DateTimeKind.Utc);
}
