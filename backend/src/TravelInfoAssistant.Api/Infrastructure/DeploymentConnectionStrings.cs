using Npgsql;

namespace TravelInfoAssistant.Api.Infrastructure;

public static class DeploymentConnectionStrings
{
    public static string NormalizePostgres(string connection)
    {
        if (!Uri.TryCreate(connection, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            return connection;
        }

        var (username, password) = ParseUserInfo(uri);
        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = database,
            Username = username,
            Password = password,
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }

    private static (string Username, string Password) ParseUserInfo(Uri uri)
    {
        if (string.IsNullOrEmpty(uri.UserInfo))
        {
            return (string.Empty, string.Empty);
        }

        var separator = uri.UserInfo.IndexOf(':');
        if (separator < 0)
        {
            return (Uri.UnescapeDataString(uri.UserInfo), string.Empty);
        }

        return (
            Uri.UnescapeDataString(uri.UserInfo[..separator]),
            Uri.UnescapeDataString(uri.UserInfo[(separator + 1)..]));
    }
}