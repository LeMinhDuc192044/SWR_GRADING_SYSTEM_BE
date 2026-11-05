namespace Application.Common;

public static class VietnamTime
{
    private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

    public static DateTime Now
    {
        get
        {
            var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
            return DateTime.SpecifyKind(vietnamNow, DateTimeKind.Unspecified);
        }
    }

    public static DateOnly Today => DateOnly.FromDateTime(Now);

    public static DateTime Convert(DateTime value)
    {
        var utcValue = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

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
