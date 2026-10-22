using Microsoft.Extensions.Configuration;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class ProductionConfigurationTests
{
    [Fact]
    public void ProductionConfiguration_DoesNotUseLocalRedisDefault()
    {
        var configuration = CreateProductionConfiguration();

        Assert.True(string.IsNullOrWhiteSpace(
            configuration["Redis:ConnectionString"]));
    }

    [Fact]
    public void EnvironmentConfiguration_CanEnableRedisInProduction()
    {
        var configuration = CreateProductionConfiguration(
            new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = "managed-redis:6379"
            });

        Assert.Equal(
            "managed-redis:6379",
            configuration["Redis:ConnectionString"]);
    }

    private static IConfigurationRoot CreateProductionConfiguration(
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var apiProjectDirectory = FindApiProjectDirectory();
        var builder = new ConfigurationBuilder()
            .SetBasePath(apiProjectDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Production.json", optional: false);

        if (overrides is not null)
        {
            builder.AddInMemoryCollection(overrides);
        }

        return builder.Build();
    }

    private static string FindApiProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "backend",
                "src",
                "TravelInfoAssistant.Api");

            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the TravelInfoAssistant.Api project directory.");
    }
}
