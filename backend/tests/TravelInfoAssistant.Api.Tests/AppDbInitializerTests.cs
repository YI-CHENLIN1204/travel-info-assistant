using TravelInfoAssistant.Api.Infrastructure;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class AppDbInitializerTests
{
    [Fact]
    public void CoreSchemaSqlCreatesRequiredSchemaIdempotently()
    {
        var sql = AppDbInitializer.CoreSchemaSql;

        Assert.Contains("CREATE TABLE IF NOT EXISTS cities", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS city_service_capabilities", sql);
        Assert.Contains(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_cities_Code\"",
            sql);
        Assert.Contains(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_city_service_capabilities_CityId_ServiceKey\"",
            sql);
        Assert.Contains(
            "FOREIGN KEY (\"CityId\") REFERENCES cities (\"Id\") ON DELETE CASCADE",
            sql);
        Assert.DoesNotContain("DROP ", sql.ToUpperInvariant());
        Assert.DoesNotContain("TRUNCATE ", sql.ToUpperInvariant());
    }
}
