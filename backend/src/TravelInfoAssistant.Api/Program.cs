using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Infrastructure;
using TravelInfoAssistant.Api.Options;
using TravelInfoAssistant.Api.Providers.AeroDataBox;
using TravelInfoAssistant.Api.Providers.Boca;
using TravelInfoAssistant.Api.Providers.MetNorway;
using TravelInfoAssistant.Api.Providers.Odpt;
using TravelInfoAssistant.Api.Providers.Tdx;
using TravelInfoAssistant.Api.Services;
using TravelInfoAssistant.Api.Services.Alerts;
using TravelInfoAssistant.Api.Services.Emergency;
using TravelInfoAssistant.Api.Services.Flights;
using TravelInfoAssistant.Api.Services.Transit;
using TravelInfoAssistant.Api.Services.Weather;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.Configure<TdxOptions>(builder.Configuration.GetSection(TdxOptions.SectionName));
builder.Services.Configure<AeroDataBoxOptions>(
    builder.Configuration.GetSection(AeroDataBoxOptions.SectionName));
builder.Services.Configure<MetNorwayOptions>(
    builder.Configuration.GetSection(MetNorwayOptions.SectionName));
builder.Services.Configure<BocaOptions>(
    builder.Configuration.GetSection(BocaOptions.SectionName));
builder.Services.Configure<OdptOptions>(
    builder.Configuration.GetSection(OdptOptions.SectionName));
builder.Services
    .AddHttpClient("tdx-api", (services, client) =>
    {
        var settings = services.GetRequiredService<IOptions<TdxOptions>>().Value;
        client.BaseAddress = new Uri(settings.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds));
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });
builder.Services
    .AddHttpClient("aerodatabox-api", (services, client) =>
    {
        var settings = services.GetRequiredService<IOptions<AeroDataBoxOptions>>().Value;
        client.BaseAddress = new Uri(settings.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds));
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });
builder.Services
    .AddHttpClient("met-norway-api", (services, client) =>
    {
        var settings = services.GetRequiredService<IOptions<MetNorwayOptions>>().Value;
        client.BaseAddress = new Uri(settings.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds));
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });
builder.Services
    .AddHttpClient("boca-api", (services, client) =>
    {
        var settings = services.GetRequiredService<IOptions<BocaOptions>>().Value;
        client.BaseAddress = new Uri(settings.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds));
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });
builder.Services
    .AddHttpClient("odpt-api", (services, client) =>
    {
        var settings = services.GetRequiredService<IOptions<OdptOptions>>().Value;
        client.BaseAddress = new Uri(settings.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds));
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });
builder.Services
    .AddHttpClient("tdx-auth", (services, client) =>
    {
        var settings = services.GetRequiredService<IOptions<TdxOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds));
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });

var postgresConnection = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(postgresConnection));

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"]
        ?? throw new InvalidOperationException("Redis:ConnectionString is required.");
    options.InstanceName = "travel-info:";
});

builder.Services.AddScoped<ICityService, CityService>();
builder.Services.AddScoped<ISystemHealthService, SystemHealthService>();
builder.Services.AddScoped<ITransitService, TransitService>();
builder.Services.AddScoped<IFlightService, FlightService>();
builder.Services.AddScoped<IWeatherService, WeatherService>();
builder.Services.AddScoped<ITravelAlertService, TravelAlertService>();
builder.Services.AddScoped<IEmergencyInfoService, EmergencyInfoService>();
builder.Services.AddSingleton<IProviderCache, ProviderCache>();
builder.Services.AddSingleton<IAirportCatalog, AirportCatalog>();
builder.Services.AddSingleton<ITdxRateGate, TdxRateGate>();
builder.Services.AddSingleton<ITdxUsageMeter, TdxUsageMeter>();
builder.Services.AddSingleton<ITdxTokenProvider, TdxTokenProvider>();
builder.Services.AddSingleton<ITdxApiClient, TdxApiClient>();
builder.Services.AddSingleton<ITdxTransitProvider, TdxTransitProvider>();
builder.Services.AddSingleton<IAeroDataBoxRateGate, AeroDataBoxRateGate>();
builder.Services.AddSingleton<IAeroDataBoxUsageMeter, AeroDataBoxUsageMeter>();
builder.Services.AddSingleton<IAeroDataBoxApiClient, AeroDataBoxApiClient>();
builder.Services.AddSingleton<IAeroDataBoxFlightProvider, AeroDataBoxFlightProvider>();
builder.Services.AddSingleton<IMetNorwayRateGate, MetNorwayRateGate>();
builder.Services.AddSingleton<IMetNorwayApiClient, MetNorwayApiClient>();
builder.Services.AddSingleton<IMetNorwayWeatherProvider, MetNorwayWeatherProvider>();
builder.Services.AddSingleton<IBocaApiClient, BocaApiClient>();
builder.Services.AddSingleton<IBocaAlertsProvider, BocaAlertsProvider>();
builder.Services.AddSingleton<IOdptApiClient, OdptApiClient>();
builder.Services.AddSingleton<IOdptTransitProvider, OdptTransitProvider>();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:Origins")
    .Get<string[]>() ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.MapControllers();

await AppDbInitializer.InitializeAsync(app.Services);

await app.RunAsync();

public partial class Program
{
}
