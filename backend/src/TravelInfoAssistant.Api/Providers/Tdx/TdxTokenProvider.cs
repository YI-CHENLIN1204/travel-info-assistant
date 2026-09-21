using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.Tdx;

public interface ITdxTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
    void Invalidate();
}

public sealed class TdxTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<TdxOptions> options,
    TimeProvider timeProvider) : ITdxTokenProvider
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private TokenState? _token;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            throw new TdxNotConfiguredException();
        }

        if (IsUsable(_token))
        {
            return _token!.AccessToken;
        }

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            if (IsUsable(_token))
            {
                return _token!.AccessToken;
            }

            using var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", settings.ClientId),
                new KeyValuePair<string, string>("client_secret", settings.ClientSecret)
            ]);

            var client = httpClientFactory.CreateClient("tdx-auth");
            using var response = await client.PostAsync(settings.TokenUrl, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new TdxProviderException(
                    "TDX authentication failed.",
                    (int)response.StatusCode);
            }

            var token = await response.Content.ReadFromJsonAsync<TdxTokenResponse>(
                cancellationToken: cancellationToken)
                ?? throw new TdxProviderException("TDX returned an empty authentication response.");

            var lifetime = TimeSpan.FromSeconds(Math.Max(60, token.ExpiresIn));
            _token = new TokenState(
                token.AccessToken,
                timeProvider.GetUtcNow().Add(lifetime).AddSeconds(-30));

            return _token.AccessToken;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public void Invalidate() => _token = null;

    private bool IsUsable(TokenState? state) =>
        state is not null && state.ExpiresAt > timeProvider.GetUtcNow().AddSeconds(15);

    private sealed record TokenState(string AccessToken, DateTimeOffset ExpiresAt);
}
