using Npgsql;
using Npgsql.Tests;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;

internal static class TestContainer
{
    private static PostgreSqlContainer? PostgreSqlContainer;
    public static string? ConnectionString;

    public static async Task Setup()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(TestUtil.DefaultConnectionString);
        PostgreSqlContainer = new PostgreSqlBuilder()
            .WithDatabase(connectionString.Database)
            .WithUsername(connectionString.Username)
            .WithPassword(connectionString.Password)
            .Build();
        await PostgreSqlContainer.StartAsync();
        connectionString.Port = PostgreSqlContainer.GetMappedPublicPort(connectionString.Port);
        ConnectionString = connectionString.ToString();
    }

    public static async Task TearDown()
    {
        if (PostgreSqlContainer is not null)
            await PostgreSqlContainer.DisposeAsync();
    }
}
