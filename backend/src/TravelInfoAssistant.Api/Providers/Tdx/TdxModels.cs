using System.Text.Json.Serialization;

namespace TravelInfoAssistant.Api.Providers.Tdx;

public sealed class TdxLocalizedName
{
    [JsonPropertyName("Zh_tw")]
    public string? ZhTw { get; init; }

    public string? En { get; init; }
    public string? Ja { get; init; }
    public string? Ko { get; init; }
}

public sealed class TdxOperator
{
    public string? OperatorID { get; init; }
    public TdxLocalizedName? OperatorName { get; init; }
}

public sealed class TdxBusSubRoute
{
    public string? SubRouteUID { get; init; }
    public string? SubRouteID { get; init; }
    public TdxLocalizedName? SubRouteName { get; init; }
    public string? Headsign { get; init; }
    public string? HeadsignEn { get; init; }
    public int Direction { get; init; }
    public string? DepartureStopNameZh { get; init; }
    public string? DepartureStopNameEn { get; init; }
    public string? DestinationStopNameZh { get; init; }
    public string? DestinationStopNameEn { get; init; }
}

public sealed class TdxBusRoute
{
    public string? RouteUID { get; init; }
    public string? RouteID { get; init; }
    public TdxLocalizedName? RouteName { get; init; }
    public string? DepartureStopNameZh { get; init; }
    public string? DepartureStopNameEn { get; init; }
    public string? DestinationStopNameZh { get; init; }
    public string? DestinationStopNameEn { get; init; }
    public IReadOnlyList<TdxOperator> Operators { get; init; } = [];
    public IReadOnlyList<TdxBusSubRoute> SubRoutes { get; init; } = [];
    public DateTimeOffset? UpdateTime { get; init; }
}

public sealed class TdxPosition
{
    public double? PositionLon { get; init; }
    public double? PositionLat { get; init; }
}

public sealed class TdxBusStop
{
    public string? StopUID { get; init; }
    public string? StopID { get; init; }
    public TdxLocalizedName? StopName { get; init; }
    public int StopSequence { get; init; }
    public TdxPosition? StopPosition { get; init; }
}

public sealed class TdxBusStopOfRoute
{
    public string? RouteUID { get; init; }
    public string? RouteID { get; init; }
    public TdxLocalizedName? RouteName { get; init; }
    public string? SubRouteUID { get; init; }
    public int Direction { get; init; }
    public IReadOnlyList<TdxBusStop> Stops { get; init; } = [];
    public DateTimeOffset? UpdateTime { get; init; }
}

public sealed class TdxBusEstimate
{
    public string? PlateNumb { get; init; }
    public int? EstimateTime { get; init; }
    public bool IsLastBus { get; init; }
}

public sealed class TdxBusArrival
{
    public string? PlateNumb { get; init; }
    public string? StopUID { get; init; }
    public string? StopID { get; init; }
    public TdxLocalizedName? StopName { get; init; }
    public string? RouteUID { get; init; }
    public string? RouteID { get; init; }
    public TdxLocalizedName? RouteName { get; init; }
    public int Direction { get; init; }
    public int? EstimateTime { get; init; }
    public string? ScheduledTime { get; init; }
    public int StopStatus { get; init; }
    public DateTimeOffset? NextBusTime { get; init; }
    public bool IsLastBus { get; init; }
    public IReadOnlyList<TdxBusEstimate> Estimates { get; init; } = [];
    public DateTimeOffset? DataTime { get; init; }
    public DateTimeOffset? SrcUpdateTime { get; init; }
    public DateTimeOffset? UpdateTime { get; init; }
}

public sealed class TdxMetroStation
{
    public TdxPosition? StationPosition { get; init; }
    public string? StationUID { get; init; }
    public string? StationID { get; init; }
    public TdxLocalizedName? StationName { get; init; }
    public string? StationAddress { get; init; }
    public DateTimeOffset? SrcUpdateTime { get; init; }
    public DateTimeOffset? UpdateTime { get; init; }
}

public sealed class TdxMetroLiveBoard
{
    public string? LineNO { get; init; }
    public string? LineID { get; init; }
    public TdxLocalizedName? LineName { get; init; }
    public string? StationID { get; init; }
    public TdxLocalizedName? StationName { get; init; }
    public string? TripHeadSign { get; init; }

    // The upstream v2 schema exposes both spellings. Keep both for compatibility.
    public string? DestinationStaionID { get; init; }
    public string? DestinationStationID { get; init; }
    public TdxLocalizedName? DestinationStationName { get; init; }
    public int ServiceStatus { get; init; }
    public int? EstimateTime { get; init; }
    public DateTimeOffset? SrcUpdateTime { get; init; }
    public DateTimeOffset? UpdateTime { get; init; }
}

public sealed class TdxMetroTimetableEntry
{
    public int Sequence { get; init; }
    public string? TrainNo { get; init; }
    public string? ArrivalTime { get; init; }
    public string? DepartureTime { get; init; }
}

public sealed class TdxServiceDay
{
    public bool Monday { get; init; }
    public bool Tuesday { get; init; }
    public bool Wednesday { get; init; }
    public bool Thursday { get; init; }
    public bool Friday { get; init; }
    public bool Saturday { get; init; }
    public bool Sunday { get; init; }
    public bool NationalHolidays { get; init; }
}

public sealed class TdxMetroStationTimetable
{
    public string? RouteID { get; init; }
    public string? LineID { get; init; }
    public string? StationID { get; init; }
    public TdxLocalizedName? StationName { get; init; }
    public int Direction { get; init; }
    public string? DestinationStaionID { get; init; }
    public TdxLocalizedName? DestinationStationName { get; init; }
    public IReadOnlyList<TdxMetroTimetableEntry> Timetables { get; init; } = [];
    public TdxServiceDay? ServiceDay { get; init; }
    public DateTimeOffset? SrcUpdateTime { get; init; }
    public DateTimeOffset? UpdateTime { get; init; }
}

public sealed class TdxTraStationResponse
{
    public DateTimeOffset? UpdateTime { get; init; }
    public DateTimeOffset? SrcUpdateTime { get; init; }
    public IReadOnlyList<TdxTraStation> Stations { get; init; } = [];
}

public sealed class TdxTraStation
{
    public string? StationUID { get; init; }
    public string? StationID { get; init; }
    public TdxLocalizedName? StationName { get; init; }
    public TdxPosition? StationPosition { get; init; }
    public string? StationAddress { get; init; }
}

public sealed class TdxTraDailyStationTimetableResponse
{
    public DateTimeOffset? UpdateTime { get; init; }
    public DateTimeOffset? SrcUpdateTime { get; init; }
    public string? TrainDate { get; init; }
    public IReadOnlyList<TdxTraStationTimetable> StationTimetables { get; init; } = [];
}

public sealed class TdxTraStationTimetable
{
    public string? RouteID { get; init; }
    public string? StationID { get; init; }
    public TdxLocalizedName? StationName { get; init; }
    public int? Direction { get; init; }
    public IReadOnlyList<TdxTraTimetableEntry> TimeTables { get; init; } = [];
}

public sealed class TdxTraTimetableEntry
{
    public int Sequence { get; init; }
    public string? TrainNo { get; init; }
    public string? DestinationStationID { get; init; }
    public TdxLocalizedName? DestinationStationName { get; init; }
    public string? TrainTypeID { get; init; }
    public string? TrainTypeCode { get; init; }
    public TdxLocalizedName? TrainTypeName { get; init; }
    public string? ArrivalTime { get; init; }
    public string? DepartureTime { get; init; }
    public int SuspendedFlag { get; init; }
}

public sealed class TdxTraStationLiveBoardResponse
{
    public DateTimeOffset? UpdateTime { get; init; }
    public DateTimeOffset? SrcUpdateTime { get; init; }
    public IReadOnlyList<TdxTraStationLiveBoard> StationLiveBoards { get; init; } = [];
}

public sealed class TdxTraStationLiveBoard
{
    public string? StationID { get; init; }
    public TdxLocalizedName? StationName { get; init; }
    public string? TrainNo { get; init; }
    public int? Direction { get; init; }
    public string? TrainTypeID { get; init; }
    public string? TrainTypeCode { get; init; }
    public TdxLocalizedName? TrainTypeName { get; init; }
    public string? EndingStationID { get; init; }
    public TdxLocalizedName? EndingStationName { get; init; }
    public string? Platform { get; init; }
    public string? ScheduleArrivalTime { get; init; }
    public string? ScheduleDepartureTime { get; init; }
    public int DelayTime { get; init; }
    public int? RunningStatus { get; init; }
    public DateTimeOffset? UpdateTime { get; init; }
}

public sealed class TdxTokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }
}
