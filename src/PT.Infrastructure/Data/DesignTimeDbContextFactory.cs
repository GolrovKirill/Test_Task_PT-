using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PT.Infrastructure.Data;

/// <summary>
/// Нужен, чтобы `dotnet ef migrations add` не зависел от запуска API,
/// user-secrets и конфигурации хоста.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=pt_test_db;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
