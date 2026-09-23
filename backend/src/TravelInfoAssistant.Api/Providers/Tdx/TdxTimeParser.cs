using System.Globalization;

namespace TravelInfoAssistant.Api.Providers.Tdx;

public static class TdxTimeParser
{
    private static readonly TimeZoneInfo TaipeiTimeZone = CreateTaipeiTimeZone();

    public static DateTimeOffset ToTaipei(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, TaipeiTimeZone);

    public static DateTimeOffset? ParseNextOccurrence(
        string? timeText,
        DateTimeOffset referenceUtc)
    {
        var candidate = ParseOccurrenceOnReferenceDate(timeText, referenceUtc);
        if (candidate is null)
        {
            return null;
        }

        var value = candidate.Value;
        if (value < referenceUtc.AddMinutes(-5))
        {
            value = value.AddDays(1);
        }

        return value;
    }

    public static DateTimeOffset? ParseOccurrenceOnReferenceDate(
        string? timeText,
        DateTimeOffset referenceUtc)
    {
        if (!TryParseTime(timeText, out var time))
        {
            return null;
        }

        var localReference = ToTaipei(referenceUtc);
        var localDateTime = localReference.Date.Add(time);
        var offset = TaipeiTimeZone.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset).ToUniversalTime();
    }

    public static bool IsServiceDay(TdxServiceDay? serviceDay, DateTimeOffset referenceUtc)
    {
        if (serviceDay is null)
        {
            return true;
        }

        return ToTaipei(referenceUtc).DayOfWeek switch
        {
            DayOfWeek.Monday => serviceDay.Monday,
            DayOfWeek.Tuesday => serviceDay.Tuesday,
            DayOfWeek.Wednesday => serviceDay.Wednesday,
            DayOfWeek.Thursday => serviceDay.Thursday,
            DayOfWeek.Friday => serviceDay.Friday,
            DayOfWeek.Saturday => serviceDay.Saturday,
            DayOfWeek.Sunday => serviceDay.Sunday,
            _ => false
        };
    }

    private static bool TryParseTime(string? value, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return TimeSpan.TryParseExact(
                   value,
                   ["h\\:mm", "hh\\:mm", "h\\:mm\\:ss", "hh\\:mm\\:ss"],
                   CultureInfo.InvariantCulture,
                   out time)
               || TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out time);
    }

    private static TimeZoneInfo CreateTaipeiTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "Asia/Taipei",
                TimeSpan.FromHours(8),
                "Taipei Standard Time",
                "Taipei Standard Time");
        }
    }
}
