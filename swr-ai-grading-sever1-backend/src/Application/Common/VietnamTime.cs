namespace Application.Common;

public static class VietnamTime
{
    private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

    // Database timestamps are stored in PostgreSQL as timestamp with time zone.
    // They must always be DateTime values with Kind=Utc.
    public static DateTime Now => DateTime.UtcNow;

    public static DateTime LocalNow
    {
        get
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
            return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        }
    }

    public static DateOnly Today => DateOnly.FromDateTime(LocalNow);

    public static DateTime Convert(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public static DateTime ToVietnamTime(DateTime value)
    {
        var utcValue = Convert(value);
        var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(utcValue, VietnamTimeZone);
        return DateTime.SpecifyKind(vietnamTime, DateTimeKind.Unspecified);
    }

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("Vietnam Standard Time", TimeSpan.FromHours(7), "Vietnam Standard Time", "Vietnam Standard Time");
            }
        }
    }
}
