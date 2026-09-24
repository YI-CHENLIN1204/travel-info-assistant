using System.Globalization;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.MetNorway;

public sealed class MetNorwayWeatherProvider(
    IMetNorwayApiClient apiClient,
    IProviderCache cache,
    TimeProvider timeProvider,
    ILogger<MetNorwayWeatherProvider> logger) : IMetNorwayWeatherProvider
{
    private static readonly TimeSpan DefaultFreshFor = TimeSpan.FromHours(1);
    private static readonly TimeSpan RetainFor = TimeSpan.FromHours(24);

    public async Task<ProviderQueryResult<WeatherResponse?>> GetForecastAsync(
        string locationName,
        double latitude,
        double longitude,
        string timeZone,
        CancellationToken cancellationToken)
    {
        var roundedLatitude = Math.Round(latitude, 4);
        var roundedLongitude = Math.Round(longitude, 4);
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"weather:metno:{roundedLatitude:0.####}:{roundedLongitude:0.####}:v1");

        try
        {
            return await cache.GetOrCreateAsync<WeatherResponse?>(
                key,
                DefaultFreshFor,
                RetainFor,
                async token =>
                {
                    var response = await apiClient.GetForecastAsync(
                        roundedLatitude,
                        roundedLongitude,
                        token);
                    var weather = MetNorwayWeatherMapper.Map(
                        response.Data,
                        locationName,
                        timeZone,
                        roundedLatitude,
                        roundedLongitude);

                    return new ProviderPayload<WeatherResponse?>(
                        weather,
                        "scheduled",
                        response.Data.Properties?.Meta?.UpdatedAt ?? response.LastModified,
                        response.FetchedAt,
                        response.ExpiresAt);
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "MET Norway query failed for {Latitude}, {Longitude}.",
                roundedLatitude,
                roundedLongitude);
            return ProviderQueryResult<WeatherResponse?>.Unavailable(
                null,
                GetPublicErrorMessage(exception),
                timeProvider);
        }
    }

    private static string GetPublicErrorMessage(Exception exception) => exception switch
    {
        MetNorwayProviderException { StatusCode: 403 } =>
            "天氣資料來源目前拒絕連線，系統已停止重試並保留功能入口。",
        MetNorwayProviderException { StatusCode: 429 } =>
            "天氣資料來源目前忙碌，請稍後再試。",
        MetNorwayProviderException => "MET Norway 暫時無法提供天氣資料，請稍後再試。",
        HttpRequestException => "目前無法連線至 MET Norway，請稍後再試。",
        _ => "天氣資料暫時無法更新，請稍後再試。"
    };
}
