using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.LtaDataMall;

public interface ILtaDataMallApiClient
{
    Task<LtaHttpResult<LtaGtfsNetwork>> GetNetworkAsync(CancellationToken cancellationToken);

    Task<LtaHttpResult<LtaRealtimeFeed>> GetTripUpdatesAsync(
        CancellationToken cancellationToken);

    Task<LtaHttpResult<LtaRealtimeFeed>> GetServiceAlertsAsync(
        CancellationToken cancellationToken);
}

public sealed class LtaDataMallApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<LtaDataMallOptions> options,
    TimeProvider timeProvider,
    ILogger<LtaDataMallApiClient> logger) : ILtaDataMallApiClient
{
    private const string ScheduleEndpoint = "GTFSScheduleTrain";
    private const string TripUpdatesEndpoint = "GTFSRealtimeTrainTripUpdates";
    private const string ServiceAlertsEndpoint = "GTFSRealTimeTrainServiceAlerts";
    private const string OfficialDownloadHost =
        "dmprod-datasets.s3.ap-southeast-1.amazonaws.com";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<LtaHttpResult<LtaGtfsNetwork>> GetNetworkAsync(
        CancellationToken cancellationToken)
    {
        var scheduleTask = DownloadSignedPayloadAsync(ScheduleEndpoint, cancellationToken);
        var chineseNamesTask = DownloadChineseNamesAsync(cancellationToken);
        await Task.WhenAll(scheduleTask, chineseNamesTask);

        var schedule = await scheduleTask;
        var chineseNames = await chineseNamesTask;
        return new LtaHttpResult<LtaGtfsNetwork>(
            LtaGtfsStaticParser.ParseNetwork(schedule.Data, chineseNames),
            timeProvider.GetUtcNow(),
            schedule.SourceUpdatedAt);
    }

    public async Task<LtaHttpResult<LtaRealtimeFeed>> GetTripUpdatesAsync(
        CancellationToken cancellationToken)
    {
        var payload = await DownloadSignedPayloadAsync(TripUpdatesEndpoint, cancellationToken);
        var feed = LtaGtfsRealtimeParser.Parse(payload.Data);
        return new LtaHttpResult<LtaRealtimeFeed>(
            feed,
            timeProvider.GetUtcNow(),
            feed.Timestamp ?? payload.SourceUpdatedAt);
    }

    public async Task<LtaHttpResult<LtaRealtimeFeed>> GetServiceAlertsAsync(
        CancellationToken cancellationToken)
    {
        var payload = await DownloadSignedPayloadAsync(ServiceAlertsEndpoint, cancellationToken);
        var feed = LtaGtfsRealtimeParser.Parse(payload.Data);
        return new LtaHttpResult<LtaRealtimeFeed>(
            feed,
            timeProvider.GetUtcNow(),
            feed.Timestamp ?? payload.SourceUpdatedAt);
    }

    private async Task<LtaHttpResult<byte[]>> DownloadSignedPayloadAsync(
        string endpoint,
        CancellationToken cancellationToken)
    {
        var accountKey = options.Value.AccountKey.Trim();
        if (string.IsNullOrWhiteSpace(accountKey))
        {
            throw new LtaDataMallNotConfiguredException();
        }

        var apiClient = httpClientFactory.CreateClient("lta-datamall");
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"./{endpoint}", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("AccountKey", accountKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await apiClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new LtaDataMallProviderException(
                $"LTA DataMall returned HTTP {(int)response.StatusCode} for {endpoint}.");
        }

        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var envelope = JsonSerializer.Deserialize<LtaDownloadResponse>(body, JsonOptions);
        var item = envelope?.Value.FirstOrDefault(value =>
            !string.IsNullOrWhiteSpace(value.Link));
        if (item is null ||
            !Uri.TryCreate(item.Link, UriKind.Absolute, out var downloadUri) ||
            downloadUri.Scheme != Uri.UriSchemeHttps ||
            !downloadUri.Host.Equals(OfficialDownloadHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new LtaDataMallProviderException(
                $"LTA DataMall returned an invalid download reference for {endpoint}.");
        }

        var downloadClient = httpClientFactory.CreateClient("lta-download");
        using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUri);
        using var downloadResponse = await downloadClient.SendAsync(
            downloadRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!downloadResponse.IsSuccessStatusCode)
        {
            throw new LtaDataMallProviderException(
                $"LTA DataMall download returned HTTP {(int)downloadResponse.StatusCode}.");
        }

        return new LtaHttpResult<byte[]>(
            await downloadResponse.Content.ReadAsByteArrayAsync(cancellationToken),
            timeProvider.GetUtcNow(),
            item.Timestamp);
    }

    private async Task<byte[]> DownloadChineseNamesAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!Uri.TryCreate(options.Value.ChineseNamesUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !uri.Host.Equals("datamall.lta.gov.sg", StringComparison.OrdinalIgnoreCase))
            {
                throw new LtaDataMallProviderException(
                    "The configured LTA Chinese station-name URL is invalid.");
            }

            var client = httpClientFactory.CreateClient("lta-download");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "LTA Chinese station names are unavailable ({ErrorType}); using GTFS names.",
                exception.GetType().Name);
            return CreateEmptyZip();
        }
    }

    private static byte[] CreateEmptyZip()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(
                   stream,
                   System.IO.Compression.ZipArchiveMode.Create,
                   leaveOpen: true))
        {
        }
        return stream.ToArray();
    }
}

public sealed class LtaDataMallNotConfiguredException : Exception
{
    public LtaDataMallNotConfiguredException()
        : base("LTA DataMall API Account Key is not configured.")
    {
    }
}

public sealed class LtaDataMallProviderException(string message) : Exception(message);
