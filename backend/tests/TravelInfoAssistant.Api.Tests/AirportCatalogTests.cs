using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using TravelInfoAssistant.Api.Services.Flights;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class AirportCatalogTests
{
    [Theory]
    [InlineData("桃園", "TPE")]
    [InlineData("台北", "TSA")]
    [InlineData("臺北", "TPE")]
    [InlineData("台中", "RMQ")]
    [InlineData("香港", "HKG")]
    [InlineData("高雄", "KHH")]
    [InlineData("TPE", "TPE")]
    public void Search_FindsAirportByChineseCityAndCode(string query, string expected)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(root, "Data"));
        try
        {
            var airports = new[] { "TPE", "TSA", "RMQ", "HKG", "KHH" }
                .Select(iata => new { iata, name = "English airport", municipality = "English city",
                    countryCode = "TW", latitude = 25, longitude = 121 });
            File.WriteAllText(Path.Combine(root, "Data", "airports.min.json"),
                System.Text.Json.JsonSerializer.Serialize(new { generatedAt = "2026-10-24T00:00:00Z", airports }));
            var catalog = new AirportCatalog(new EnvironmentStub { ContentRootPath = root });
            var result = catalog.Search(query, 10);
            Assert.Contains(result, airport => airport.Iata == expected);
            Assert.DoesNotContain(result, airport => airport.Name == "English airport");
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = "";
        public string ApplicationName { get; set; } = "test";
        public string EnvironmentName { get; set; } = "test";
        public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
