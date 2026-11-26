using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NUnit.Framework;
using PT.Infrastructure.Data;
using PT.Infrastructure.Events;
using PT.Infrastructure.Export;

namespace PT.Tests.Integration;

[SetUpFixture]
public sealed class TestHost
{
    public static string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
        ?? throw new InvalidOperationException(
            "Set the TEST_DB_CONNECTION environment variable before running tests "
            + "(e.g. run ./scripts/test.sh, or export it manually).");

    public static WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public static RecordingEventPublisher EventPublisher { get; private set; } = null!;

    public static RecordingDocumentExporter ExportExporter { get; private set; } = null!;

    public static async Task ResetDatabaseAsync()
    {
        EventPublisher.Clear();
        ExportExporter.Clear();

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
        Environment.SetEnvironmentVariable("Outbox__DispatcherEnabled", "false");
        Environment.SetEnvironmentVariable("Export__BatchSize", "2");

        EventPublisher = new RecordingEventPublisher();
        ExportExporter = new RecordingDocumentExporter();
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEventPublisher>();
                services.AddSingleton<IEventPublisher>(EventPublisher);

                services.RemoveAll<IExternalDocumentExporter>();
                services.AddSingleton<IExternalDocumentExporter>(ExportExporter);
            }));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        Factory.Dispose();
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
        Environment.SetEnvironmentVariable("Outbox__DispatcherEnabled", null);
        Environment.SetEnvironmentVariable("Export__BatchSize", null);
    }
}
