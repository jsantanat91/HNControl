namespace HNControl.Web.Services;

public static class TimeUtil
{
    public static DateTime UtcDate(DateTime d)
        => DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
}