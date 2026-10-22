using Npgsql;
using TravelInfoAssistant.Api.Infrastructure;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class DeploymentConnectionStringsTests
{
    [Fact]
    public void NormalizePostgres_ConvertsSupabasePoolerUrl()
    {
        var normalized = DeploymentConnectionStrings.NormalizePostgres(
            "postgresql://postgres.project-ref:p%40ssword@pooler.example.com:5432/postgres");

        var settings = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal("pooler.example.com", settings.Host);
        Assert.Equal(5432, settings.Port);
        Assert.Equal("postgres", settings.Database);
        Assert.Equal("postgres.project-ref", settings.Username);
        Assert.Equal("p@ssword", settings.Password);
        Assert.Equal(SslMode.Require, settings.SslMode);
    }

    [Fact]
    public void NormalizePostgres_PreservesNpgsqlConnectionString()
    {
        const string connection =
            "Host=localhost;Database=travel_info;Username=travel_app";

        Assert.Equal(
            connection,
            DeploymentConnectionStrings.NormalizePostgres(connection));
    }
}