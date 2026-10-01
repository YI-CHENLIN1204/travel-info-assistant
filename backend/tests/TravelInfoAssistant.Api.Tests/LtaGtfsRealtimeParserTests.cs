using Google.Protobuf;
using TravelInfoAssistant.Api.Providers.LtaDataMall;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class LtaGtfsRealtimeParserTests
{
    [Fact]
    public void ParsesTripPredictionAndServiceAlert()
    {
        const ulong timestamp = 1790827200;
        var tripDescriptor = Message(output =>
        {
            String(output, 1, "trip-1");
            Enum(output, 4, 0);
            String(output, 5, "NS");
            UInt32(output, 6, 1);
        });
        var stopEvent = Message(output =>
        {
            Int32(output, 1, 120);
            Int64(output, 2, (long)timestamp + 180);
        });
        var stopUpdate = Message(output =>
        {
            UInt32(output, 1, 3);
            Nested(output, 2, stopEvent);
            Nested(output, 3, stopEvent);
            String(output, 4, "NS22-P1");
            Enum(output, 5, 1);
        });
        var tripUpdate = Message(output =>
        {
            Nested(output, 1, tripDescriptor);
            Nested(output, 2, stopUpdate);
            UInt64(output, 4, timestamp);
        });
        var tripEntity = Message(output =>
        {
            String(output, 1, "trip-entity");
            Nested(output, 3, tripUpdate);
        });

        var activePeriod = Message(output =>
        {
            UInt64(output, 1, timestamp - 60);
            UInt64(output, 2, timestamp + 600);
        });
        var selector = Message(output => String(output, 2, "NS"));
        var header = TranslatedString("Train delay", "en");
        var description = TranslatedString("Allow extra travel time.", "en");
        var alert = Message(output =>
        {
            Nested(output, 1, activePeriod);
            Nested(output, 5, selector);
            Enum(output, 7, 3);
            Nested(output, 10, header);
            Nested(output, 11, description);
        });
        var alertEntity = Message(output =>
        {
            String(output, 1, "alert-entity");
            Nested(output, 5, alert);
        });
        var headerMessage = Message(output => UInt64(output, 3, timestamp));
        var feed = Message(output =>
        {
            Nested(output, 1, headerMessage);
            Nested(output, 2, tripEntity);
            Nested(output, 2, alertEntity);
        });

        var result = LtaGtfsRealtimeParser.Parse(feed);

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds((long)timestamp), result.Timestamp);
        var trip = Assert.Single(result.TripUpdates);
        Assert.Equal("trip-1", trip.TripId);
        Assert.Equal("NS", trip.RouteId);
        Assert.Equal(1, trip.DirectionId);
        var stop = Assert.Single(trip.StopTimeUpdates);
        Assert.Equal("NS22-P1", stop.StopId);
        Assert.Equal(120, stop.DepartureDelaySeconds);
        Assert.Equal(1, stop.ScheduleRelationship);
        var serviceAlert = Assert.Single(result.Alerts);
        Assert.Equal(["NS"], serviceAlert.RouteIds);
        Assert.Equal(3, serviceAlert.Effect);
        Assert.Equal("Train delay", serviceAlert.Header);
        Assert.Equal("Allow extra travel time.", serviceAlert.Description);
    }

    private static byte[] TranslatedString(string text, string language)
    {
        var translation = Message(output =>
        {
            String(output, 1, text);
            String(output, 2, language);
        });
        return Message(output => Nested(output, 1, translation));
    }

    private static byte[] Message(Action<CodedOutputStream> write)
    {
        using var stream = new MemoryStream();
        using (var output = new CodedOutputStream(stream, leaveOpen: true))
        {
            write(output);
        }
        return stream.ToArray();
    }

    private static void Nested(CodedOutputStream output, int field, byte[] value)
    {
        output.WriteTag(field, WireFormat.WireType.LengthDelimited);
        output.WriteBytes(ByteString.CopyFrom(value));
    }

    private static void String(CodedOutputStream output, int field, string value)
    {
        output.WriteTag(field, WireFormat.WireType.LengthDelimited);
        output.WriteString(value);
    }

    private static void UInt64(CodedOutputStream output, int field, ulong value)
    {
        output.WriteTag(field, WireFormat.WireType.Varint);
        output.WriteUInt64(value);
    }

    private static void UInt32(CodedOutputStream output, int field, uint value)
    {
        output.WriteTag(field, WireFormat.WireType.Varint);
        output.WriteUInt32(value);
    }

    private static void Int64(CodedOutputStream output, int field, long value)
    {
        output.WriteTag(field, WireFormat.WireType.Varint);
        output.WriteInt64(value);
    }

    private static void Int32(CodedOutputStream output, int field, int value)
    {
        output.WriteTag(field, WireFormat.WireType.Varint);
        output.WriteInt32(value);
    }

    private static void Enum(CodedOutputStream output, int field, int value)
    {
        output.WriteTag(field, WireFormat.WireType.Varint);
        output.WriteEnum(value);
    }
}
