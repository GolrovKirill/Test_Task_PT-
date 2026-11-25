using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NUnit.Framework;
using PT.Infrastructure.Data;

namespace PT.Tests.Integration;

[SetUpFixture]
public sealed class TestHost
{
    private const string DefaultConnectionString =
        "Host=127.0.0.1;Port=5432;Database=pt_test_db_tests;Username=postgres;Password=postgres";

    public static string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("TEST_DB_CONNECTION") ?? DefaultConnectionString;

    public static WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public static async Task ResetDatabaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"Documents\", \"OutboxMessages\" RESTART IDENTITY CASCADE;");
    }

    private static async Task EnsureDatabaseAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
        var databaseName = builder.Database ?? "pt_test_db_tests";

        builder.Database = "postgres";

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        await using var check = new NpgsqlCommand(
            "SELECT 1 FROM pg_database WHERE datname = @name",
            connection);
        check.Parameters.AddWithValue("name", databaseName);

        if (await check.ExecuteScalarAsync() is null)
        {
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await create.ExecuteNonQueryAsync();
        }
    }

    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        await EnsureDatabaseAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ConnectionString);
        Factory = new WebApplicationFactory<Program>();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        Factory.Dispose();
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
    }
}
