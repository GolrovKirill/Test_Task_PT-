using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PT.Infrastructure.Data;

/// <summary>
/// Lets the EF Core CLI run migrations without starting the API host.
/// The connection string must be provided via the <c>DB_CONNECTION</c> environment
/// variable; no credentials are stored in source code.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set the DB_CONNECTION environment variable before running the EF Core CLI.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
