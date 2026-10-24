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
    [InlineData("台灣", "TPE")]
    [InlineData("臺灣", "RMQ")]
    [InlineData("Taiwan", "KHH")]
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

    [Theory]
    [InlineData("JP", "日本")]
    [InlineData("KR", "韓國")]
    [InlineData("US", "美國")]
    [InlineData("DE", "德國")]
    [InlineData("FR", "法國")]
    [InlineData("ZA", "南非")]
    public void CountryNames_CoversGlobalChineseNames(string code, string name) =>
        Assert.Contains(name, AirportCountryNames.SearchText(code));

    [Fact]
    public void Search_CountryResultsCanBeBrowsedWithoutDuplicates()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(root, "Data"));
        try
        {
            var airports = new[] { "KHH", "RMQ", "TSA", "TPE" }.Select(iata => new {
                iata, name = "Airport", municipality = "City", countryCode = "TW",
                latitude = 25, longitude = 121 });
            File.WriteAllText(Path.Combine(root, "Data", "airports.min.json"),
                System.Text.Json.JsonSerializer.Serialize(new { generatedAt = "2026-10-24T00:00:00Z", airports }));
            var catalog = new AirportCatalog(new EnvironmentStub { ContentRootPath = root });
            var first = catalog.Search("台灣", 2);
            var next = catalog.Search("台灣", 2, 2);
            Assert.Equal(new[] { "TPE", "TSA", "RMQ", "KHH" },
                first.Concat(next).Select(item => item.Iata));
            Assert.Empty(catalog.Search("台灣", 2, 4));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("MFK", "馬祖北竿")]
    [InlineData("LZN", "馬祖南竿")]
    [InlineData("KYD", "蘭嶼")]
    [InlineData("CYI", "嘉義")]
    public void TaiwanAirportNames_CoversRemainingRegionalAirports(string code, string city)
    {
        Assert.Equal(city, AirportChineseNames.Find(code)?.City);
        Assert.Contains(city, AirportChineseNames.SearchText(code));
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
