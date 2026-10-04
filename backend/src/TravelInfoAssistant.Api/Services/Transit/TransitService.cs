using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Providers.LtaDataMall;
using TravelInfoAssistant.Api.Providers.Kmb;
using TravelInfoAssistant.Api.Providers.HongKong;
using TravelInfoAssistant.Api.Providers.Mtr;
using TravelInfoAssistant.Api.Providers.Odpt;
using TravelInfoAssistant.Api.Providers.Tdx;

namespace TravelInfoAssistant.Api.Services.Transit;

public sealed class TransitService(
    ICityService cityService,
    ITdxTransitProvider tdxProvider,
    IOdptTransitProvider odptProvider,
    IMtrTransitProvider mtrProvider,
    ILtaDataMallTransitProvider ltaDataMallProvider,
    IHongKongBusTransitProvider hongKongBusProvider,
    ITdxUsageMeter usageMeter,
    TimeProvider timeProvider) : ITransitService
{
    public async Task<IReadOnlyList<TransitModeResponse>?> GetModesAsync(
        Guid cityId,
        CancellationToken cancellationToken)
    {
        var city = await cityService.GetCityAsync(cityId, cancellationToken);
        return city?.Services
            .Where(item => item.IntegrationStatus == "integrated")
            .Where(item => item.ServiceKey is "bus" or "metro" or "rail")
            .Select(item => new TransitModeResponse(
                item.ServiceKey,
                item.DisplayName,
                item.AvailabilityStatus,
                item.Message))
            .ToList();
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> SearchBusRoutesAsync(
        Guid cityId,
        string? query,
        CancellationToken cancellationToken)
    {
        var cityCode = await GetIntegratedCityCodeAsync(cityId, "bus", cancellationToken);
        ProviderQueryResult<IReadOnlyList<TransitRouteResponse>> result;
        if (cityCode == "taipei")
        {
            result = await tdxProvider.GetBusRoutesAsync(cancellationToken);
        }
        else if (cityCode == "hong-kong")
        {
            result = await hongKongBusProvider.GetBusRoutesAsync(cancellationToken);
        }
        else
        {
            return Unavailable<TransitRouteResponse>("這個城市目前尚未整合公車查詢。");
        }

        var search = query?.Trim();
        var filtered = result.Data
            .Where(item => string.IsNullOrWhiteSpace(search) || MatchesRoute(item, search))
            .Take(50)
            .ToList();
        return CopyMetadata(result, filtered);
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetBusStopsAsync(
        Guid cityId,
        string routeName,
        int direction,
        CancellationToken cancellationToken)
    {
        var cityCode = await GetIntegratedCityCodeAsync(cityId, "bus", cancellationToken);
        if (cityCode == "hong-kong")
        {
            return await hongKongBusProvider.GetBusStopsAsync(routeName.Trim(), direction, cancellationToken);
        }
        if (cityCode != "taipei")
        {
            return Unavailable<TransitStopResponse>("這個城市目前尚未整合公車查詢。");
        }

        return await tdxProvider.GetBusStopsAsync(
            routeName.Trim(),
            direction,
            cancellationToken);
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetBusArrivalsAsync(
        Guid cityId,
        string routeName,
        int direction,
        string stopId,
        CancellationToken cancellationToken)
    {
        var cityCode = await GetIntegratedCityCodeAsync(cityId, "bus", cancellationToken);
        if (cityCode == "hong-kong")
        {
            return await hongKongBusProvider.GetBusArrivalsAsync(
                routeName.Trim(), direction, stopId.Trim(), cancellationToken);
        }
        if (cityCode != "taipei")
        {
            return Unavailable<TransitArrivalResponse>("這個城市目前尚未整合公車查詢。");
        }

        return await tdxProvider.GetBusArrivalsAsync(
            routeName.Trim(),
            direction,
            stopId.Trim(),
            cancellationToken);
    }

    public async Task<ProviderQueryResult<IReadOnlyList<MetroStationResponse>>> SearchMetroStationsAsync(
        Guid cityId,
        string? query,
        CancellationToken cancellationToken)
    {
        var cityCode = await GetIntegratedCityCodeAsync(cityId, "metro", cancellationToken);
        ProviderQueryResult<IReadOnlyList<MetroStationResponse>> result;
        if (cityCode == "taipei")
        {
            result = await tdxProvider.GetMetroStationsAsync(cancellationToken);
        }
        else if (cityCode == "tokyo")
        {
            result = await odptProvider.GetMetroStationsAsync(cancellationToken);
        }
        else if (cityCode == "hong-kong")
        {
            result = await mtrProvider.GetMetroStationsAsync(cancellationToken);
        }
        else if (cityCode == "singapore")
        {
            result = await ltaDataMallProvider.GetMetroStationsAsync(cancellationToken);
        }
        else
        {
            return Unavailable<MetroStationResponse>("這個城市目前尚未整合捷運查詢。");
        }

        var search = query?.Trim();
        var filtered = result.Data
            .Where(item => string.IsNullOrWhiteSpace(search) || MatchesStation(item, search))
            .Take(50)
            .ToList();
        return CopyMetadata(result, filtered);
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> SearchMetroRoutesAsync(
        Guid cityId,
        string? query,
        CancellationToken cancellationToken)
    {
        var cityCode = await GetIntegratedCityCodeAsync(cityId, "metro", cancellationToken);
        ProviderQueryResult<IReadOnlyList<TransitRouteResponse>> result;
        if (cityCode == "taipei")
        {
            result = await tdxProvider.GetMetroRoutesAsync(cancellationToken);
        }
        else if (cityCode == "tokyo")
        {
            result = await odptProvider.GetMetroRoutesAsync(cancellationToken);
        }
        else if (cityCode == "hong-kong")
        {
            result = await mtrProvider.GetMetroRoutesAsync(cancellationToken);
        }
        else if (cityCode == "singapore")
        {
            result = await ltaDataMallProvider.GetMetroRoutesAsync(cancellationToken);
        }
        else
        {
            return Unavailable<TransitRouteResponse>("這個城市目前尚未整合地鐵路線搜尋。");
        }

        var search = query?.Trim();
        var filtered = result.Data
            .Where(item => string.IsNullOrWhiteSpace(search) || MatchesRoute(item, search))
            .Take(50)
            .ToList();
        return CopyMetadata(result, filtered);
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetMetroArrivalsAsync(
        Guid cityId,
        string stationId,
        CancellationToken cancellationToken)
    {
        var cityCode = await GetIntegratedCityCodeAsync(cityId, "metro", cancellationToken);
        if (cityCode == "tokyo")
        {
            return await odptProvider.GetMetroDeparturesAsync(
                stationId.Trim(),
                cancellationToken);
        }

        if (cityCode == "hong-kong")
        {
            return await mtrProvider.GetMetroArrivalsAsync(
                stationId.Trim(),
                cancellationToken);
        }

        if (cityCode == "singapore")
        {
            return await ltaDataMallProvider.GetMetroArrivalsAsync(
                stationId.Trim(),
                cancellationToken);
        }

        if (cityCode != "taipei")
        {
            return Unavailable<TransitArrivalResponse>("這個城市目前尚未整合捷運查詢。");
        }

        return await tdxProvider.GetMetroArrivalsAsync(stationId.Trim(), cancellationToken);
    }

    public async Task<ProviderQueryResult<IReadOnlyList<MetroServiceStatusResponse>>> GetMetroStatusAsync(
        Guid cityId,
        string? routeId,
        CancellationToken cancellationToken)
    {
        var cityCode = await GetIntegratedCityCodeAsync(cityId, "metro", cancellationToken);
        if (cityCode == "taipei" && !string.IsNullOrWhiteSpace(routeId))
        {
            return await tdxProvider.GetMetroStatusAsync(routeId.Trim(), cancellationToken);
        }

        if (cityCode == "taipei")
        {
            return Unavailable<MetroServiceStatusResponse>(
                "請先選擇一條台北捷運路線。",
                "TDX");
        }

        if (cityCode == "tokyo")
        {
            return await odptProvider.GetMetroStatusAsync(cancellationToken);
        }

        if (cityCode == "hong-kong" && !string.IsNullOrWhiteSpace(routeId))
        {
            return await mtrProvider.GetMetroStatusAsync(routeId.Trim(), cancellationToken);
        }

        if (cityCode == "hong-kong")
        {
            return Unavailable<MetroServiceStatusResponse>(
                "請先選擇一條港鐵路線。",
                "香港港鐵開放數據");
        }

        if (cityCode == "singapore" && !string.IsNullOrWhiteSpace(routeId))
        {
            return await ltaDataMallProvider.GetMetroStatusAsync(
                routeId.Trim(),
                cancellationToken);
        }

        if (cityCode == "singapore")
        {
            return Unavailable<MetroServiceStatusResponse>(
                "請先選擇一條新加坡 MRT 路線。",
                "新加坡 LTA DataMall");
        }

        return Unavailable<MetroServiceStatusResponse>("這個城市目前尚未整合地鐵運行狀態。");
    }

    public async Task<ProviderQueryResult<IReadOnlyList<RailStationResponse>>> SearchRailStationsAsync(
        Guid cityId,
        string? query,
        CancellationToken cancellationToken)
    {
        if (!await IsTaipeiAsync(cityId, "rail", cancellationToken))
        {
            return Unavailable<RailStationResponse>("這個城市目前尚未整合台鐵查詢。");
        }

        var result = await tdxProvider.GetRailStationsAsync(cancellationToken);
        var search = query?.Trim();
        var filtered = result.Data
            .Where(item => string.IsNullOrWhiteSpace(search) || MatchesStation(item, search))
            .Take(50)
            .ToList();
        return CopyMetadata(result, filtered);
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetRailArrivalsAsync(
        Guid cityId,
        string stationId,
        CancellationToken cancellationToken)
    {
        if (!await IsTaipeiAsync(cityId, "rail", cancellationToken))
        {
            return Unavailable<TransitArrivalResponse>("這個城市目前尚未整合台鐵查詢。");
        }

        return await tdxProvider.GetRailArrivalsAsync(stationId.Trim(), cancellationToken);
    }

    public Task<TdxProviderStatusResponse> GetTdxStatusAsync(CancellationToken cancellationToken) =>
        usageMeter.GetStatusAsync(cancellationToken);

    private async Task<bool> IsTaipeiAsync(
        Guid cityId,
        string serviceKey,
        CancellationToken cancellationToken)
    {
        var city = await cityService.GetCityAsync(cityId, cancellationToken);
        return city?.Code == "taipei" && city.Services.Any(item =>
            item.ServiceKey == serviceKey && item.IntegrationStatus == "integrated");
    }

    private async Task<string?> GetIntegratedCityCodeAsync(
        Guid cityId,
        string serviceKey,
        CancellationToken cancellationToken)
    {
        var city = await cityService.GetCityAsync(cityId, cancellationToken);
        return city?.Services.Any(item =>
            item.ServiceKey == serviceKey && item.IntegrationStatus == "integrated") == true
            ? city.Code
            : null;
    }

    private ProviderQueryResult<IReadOnlyList<T>> Unavailable<T>(
        string message,
        string source = "TDX") =>
        ProviderQueryResult<IReadOnlyList<T>>.Unavailable(
            Array.Empty<T>(),
            message,
            timeProvider,
            source);

    private static bool MatchesRoute(TransitRouteResponse route, string search) =>
        Contains(route.Id, search) ||
        Contains(route.NameZh, search) ||
        Contains(route.NameEn, search) ||
        Contains(route.OriginName, search) ||
        Contains(route.DestinationName, search) ||
        route.Operators.Any(item => Contains(item, search));

    private static bool MatchesStation(MetroStationResponse station, string search) =>
        Contains(station.Id, search) ||
        Contains(station.NameZh, search) ||
        Contains(station.NameEn, search) ||
        Contains(station.Address, search) ||
        Contains(station.Code, search) ||
        Contains(station.RailwayId, search) ||
        Contains(station.RailwayName, search);

    private static bool MatchesStation(RailStationResponse station, string search) =>
        Contains(station.Id, search) ||
        Contains(station.NameZh, search) ||
        Contains(station.NameEn, search) ||
        Contains(station.Address, search);

    private static bool Contains(string? value, string search)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               value.Replace('臺', '台').Contains(
                   search.Replace('臺', '台'),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderQueryResult<IReadOnlyList<T>> CopyMetadata<T>(
        ProviderQueryResult<IReadOnlyList<T>> source,
        IReadOnlyList<T> data) =>
        new(
            data,
            source.DataStatus,
            source.SourceUpdatedAt,
            source.FetchedAt,
            source.Stale,
            source.Message,
            source.Source,
            source.ServiceDayStatus,
            source.LastDepartureAt,
            source.LastDepartureDescription);
}
