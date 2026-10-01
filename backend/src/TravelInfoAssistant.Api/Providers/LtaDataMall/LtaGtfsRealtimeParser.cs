using Google.Protobuf;

namespace TravelInfoAssistant.Api.Providers.LtaDataMall;

public static class LtaGtfsRealtimeParser
{
    public static LtaRealtimeFeed Parse(byte[] bytes)
    {
        using var input = new CodedInputStream(bytes);
        DateTimeOffset? timestamp = null;
        var trips = new List<LtaTripUpdate>();
        var alerts = new List<LtaServiceAlert>();

        while (input.ReadTag() is var tag && tag != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    timestamp = ParseHeader(ReadMessage(input));
                    break;
                case 2:
                    ParseEntity(ReadMessage(input), trips, alerts);
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return new LtaRealtimeFeed(timestamp, trips, alerts);
    }

    private static DateTimeOffset? ParseHeader(byte[] bytes)
    {
        using var input = new CodedInputStream(bytes);
        DateTimeOffset? timestamp = null;
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == 3)
            {
                timestamp = FromUnixSeconds(input.ReadUInt64());
            }
            else
            {
                input.SkipLastField();
            }
        }
        return timestamp;
    }

    private static void ParseEntity(
        byte[] bytes,
        ICollection<LtaTripUpdate> trips,
        ICollection<LtaServiceAlert> alerts)
    {
        using var input = new CodedInputStream(bytes);
        var id = string.Empty;
        byte[]? tripBytes = null;
        byte[]? alertBytes = null;
        while (input.ReadTag() is var tag && tag != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    id = input.ReadString();
                    break;
                case 3:
                    tripBytes = ReadMessage(input);
                    break;
                case 5:
                    alertBytes = ReadMessage(input);
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        if (tripBytes is not null)
        {
            trips.Add(ParseTripUpdate(id, tripBytes));
        }
        if (alertBytes is not null)
        {
            alerts.Add(ParseAlert(id, alertBytes));
        }
    }

    private static LtaTripUpdate ParseTripUpdate(string id, byte[] bytes)
    {
        using var input = new CodedInputStream(bytes);
        var descriptor = new TripDescriptor();
        var stopUpdates = new List<LtaStopTimeUpdate>();
        DateTimeOffset? timestamp = null;
        while (input.ReadTag() is var tag && tag != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    descriptor = ParseTripDescriptor(ReadMessage(input));
                    break;
                case 2:
                    stopUpdates.Add(ParseStopTimeUpdate(ReadMessage(input)));
                    break;
                case 4:
                    timestamp = FromUnixSeconds(input.ReadUInt64());
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return new LtaTripUpdate(
            id,
            descriptor.TripId,
            descriptor.RouteId,
            descriptor.DirectionId,
            descriptor.ScheduleRelationship,
            timestamp,
            stopUpdates);
    }

    private static TripDescriptor ParseTripDescriptor(byte[] bytes)
    {
        using var input = new CodedInputStream(bytes);
        var descriptor = new TripDescriptor();
        while (input.ReadTag() is var tag && tag != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    descriptor.TripId = input.ReadString();
                    break;
                case 4:
                    descriptor.ScheduleRelationship = input.ReadEnum();
                    break;
                case 5:
                    descriptor.RouteId = input.ReadString();
                    break;
                case 6:
                    descriptor.DirectionId = checked((int)input.ReadUInt32());
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }
        return descriptor;
    }

    private static LtaStopTimeUpdate ParseStopTimeUpdate(byte[] bytes)
    {
        using var input = new CodedInputStream(bytes);
        string? stopId = null;
        int? sequence = null;
        var arrival = new StopTimeEvent();
        var departure = new StopTimeEvent();
        var relationship = 0;
        while (input.ReadTag() is var tag && tag != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    sequence = checked((int)input.ReadUInt32());
                    break;
                case 2:
                    arrival = ParseStopTimeEvent(ReadMessage(input));
                    break;
                case 3:
                    departure = ParseStopTimeEvent(ReadMessage(input));
                    break;
                case 4:
                    stopId = input.ReadString();
                    break;
                case 5:
                    relationship = input.ReadEnum();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return new LtaStopTimeUpdate(
            stopId,
            sequence,
            arrival.Time,
            departure.Time,
            arrival.DelaySeconds,
            departure.DelaySeconds,
            relationship);
    }

    private static StopTimeEvent ParseStopTimeEvent(byte[] bytes)
    {
        using var input = new CodedInputStream(bytes);
        var result = new StopTimeEvent();
        while (input.ReadTag() is var tag && tag != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    result.DelaySeconds = input.ReadInt32();
                    break;
                case 2:
                    result.Time = FromUnixSeconds(checked((ulong)input.ReadInt64()));
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }
        return result;
    }

    private static LtaServiceAlert ParseAlert(string id, byte[] bytes)
    {
        using var input = new CodedInputStream(bytes);
        var routeIds = new List<string>();
        var stopIds = new List<string>();
        var starts = new List<DateTimeOffset>();
        var ends = new List<DateTimeOffset>();
        string? header = null;
        string? description = null;
        var effect = 0;

        while (input.ReadTag() is var tag && tag != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    var period = ParseTimeRange(ReadMessage(input));
                    if (period.Start.HasValue) starts.Add(period.Start.Value);
                    if (period.End.HasValue) ends.Add(period.End.Value);
                    break;
                case 5:
                    var selector = ParseEntitySelector(ReadMessage(input));
                    if (!string.IsNullOrWhiteSpace(selector.RouteId))
                    {
                        routeIds.Add(selector.RouteId);
                    }
                    if (!string.IsNullOrWhiteSpace(selector.StopId))
                    {
                        stopIds.Add(selector.StopId);
                    }
                    break;
                case 7:
                    effect = input.ReadEnum();
                    break;
                case 10:
                    header = ParseTranslatedString(ReadMessage(input));
                    break;
                case 11:
                    description = ParseTranslatedString(ReadMessage(input));
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return new LtaServiceAlert(
            id,
            routeIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            stopIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            starts.Count == 0 ? null : starts.Min(),
            ends.Count == 0 ? null : ends.Max(),
            header,
            description,
            effect);
    }

    private static TimeRange ParseTimeRange(byte[] bytes)
    {
        using var input = new CodedInputStream(bytes);
        DateTimeOffset? start = null;
        DateTimeOffset? end = null;
        while (input.ReadTag() is var tag && tag != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    start = FromUnixSeconds(input.ReadUInt64());
                    break;
                case 2:
                    end = FromUnixSeconds(input.ReadUInt64());
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }
        return new TimeRange(start, end);
    }

    private static EntitySelector ParseEntitySelector(byte[] bytes)
    {
        using var input = new CodedInputStream(bytes);
        string? routeId = null;
        string? stopId = null;
        while (input.ReadTag() is var tag && tag != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 2:
                    routeId = input.ReadString();
                    break;
                case 5:
                    stopId = input.ReadString();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }
        return new EntitySelector(routeId, stopId);
    }

    private static string? ParseTranslatedString(byte[] bytes)
    {
        using var input = new CodedInputStream(bytes);
        var translations = new List<Translation>();
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == 1)
            {
                translations.Add(ParseTranslation(ReadMessage(input)));
            }
            else
            {
                input.SkipLastField();
            }
        }

        return translations
            .OrderBy(item => LanguagePriority(item.Language))
            .Select(item => item.Text)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
    }

    private static Translation ParseTranslation(byte[] bytes)
    {
        using var input = new CodedInputStream(bytes);
        string? text = null;
        string? language = null;
        while (input.ReadTag() is var tag && tag != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    text = input.ReadString();
                    break;
                case 2:
                    language = input.ReadString();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }
        return new Translation(text, language);
    }

    private static int LanguagePriority(string? language)
    {
        if (language?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true) return 0;
        if (language?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true) return 1;
        return 2;
    }

    private static byte[] ReadMessage(CodedInputStream input) =>
        input.ReadBytes().ToByteArray();

    private static DateTimeOffset? FromUnixSeconds(ulong seconds) =>
        seconds <= long.MaxValue
            ? DateTimeOffset.FromUnixTimeSeconds((long)seconds)
            : null;

    private sealed class TripDescriptor
    {
        public string? TripId { get; set; }
        public string? RouteId { get; set; }
        public int? DirectionId { get; set; }
        public int ScheduleRelationship { get; set; }
    }

    private sealed class StopTimeEvent
    {
        public DateTimeOffset? Time { get; set; }
        public int? DelaySeconds { get; set; }
    }

    private sealed record TimeRange(DateTimeOffset? Start, DateTimeOffset? End);
    private sealed record EntitySelector(string? RouteId, string? StopId);
    private sealed record Translation(string? Text, string? Language);
}
