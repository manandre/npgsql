using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.Tests;
using Npgsql.Tests.Support;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;

[SetUpFixture]
public class AssemblySetUp
{
    PostgreSqlContainer? _postgreSqlContainer = default;

    [OneTimeSetUp]
    public async Task Setup()
    {
        var connString = TestUtil.ConnectionString;
        using var conn = new NpgsqlConnection(connString);
        try
        {
            conn.Open();
        }
        catch (NpgsqlException e) when (e.IsTransient)
        {
            var connectionString = new NpgsqlConnectionStringBuilder(TestUtil.DefaultConnectionString);
            _postgreSqlContainer = new PostgreSqlBuilder()
                .WithPortBinding(connectionString.Port)
                .WithDatabase(connectionString.Database)
                .WithUsername(connectionString.Username)
                .WithPassword(connectionString.Password)
                .Build();
            await _postgreSqlContainer.StartAsync();
        }
        catch (PostgresException e)
        {
            if (e.SqlState == PostgresErrorCodes.InvalidPassword && connString == TestUtil.DefaultConnectionString)
                throw new Exception("Please create a user npgsql_tests as follows: CREATE USER npgsql_tests PASSWORD 'npgsql_tests' SUPERUSER");

            if (e.SqlState == PostgresErrorCodes.InvalidCatalogName)
            {
                var builder = new NpgsqlConnectionStringBuilder(connString)
                {
                    Pooling = false,
                    Multiplexing = false,
                    Database = "postgres"
                };

                using var adminConn = new NpgsqlConnection(builder.ConnectionString);
                adminConn.Open();
                adminConn.ExecuteNonQuery("CREATE DATABASE " + conn.Database);
                adminConn.Close();
                Thread.Sleep(1000);

                conn.Open();
                return;
            }

            throw;
        }
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        if (_postgreSqlContainer is not null)
            await _postgreSqlContainer.DisposeAsync();
    }
}
