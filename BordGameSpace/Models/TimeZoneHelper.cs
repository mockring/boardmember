namespace BordGameSpace.Models;

public static class TimeZoneHelper
{
    public static readonly TimeZoneInfo TaiwanZone =
        TimeZoneInfo.CreateCustomTimeZone("Taipei Standard Time",
            TimeSpan.FromHours(8), "Taipei Standard Time", "Taipei Standard Time");
}
