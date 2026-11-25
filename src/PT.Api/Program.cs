using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PT.Api.Common;
using PT.Api.Features.Documents;
using PT.Infrastructure.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration["DB_CONNECTION"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string is not configured. Set ConnectionStrings:Default (user-secrets) or DB_CONNECTION (env).");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddValidatorsFromAssemblyContaining<DocumentWriteRequestValidator>();
builder.Services.AddScoped<ValidationFilter<DocumentWriteRequest>>();
builder.Services.AddScoped<ValidationFilter<DocumentListQuery>>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
});

var app = builder.Build();

await ApplyMigrationsAsync(app.Services);

app.UseExceptionHandler();
app.MapOpenApi();
app.MapScalarApiReference();
app.MapDocumentEndpoints();

app.Run();

static async Task ApplyMigrationsAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");

    const int maxAttempts = 10;

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            return;
        }
        catch (Exception exception) when (attempt < maxAttempts)
        {
            logger.LogWarning(
                exception,
                "Database migration attempt {Attempt}/{MaxAttempts} failed; retrying in 2s",
                attempt,
                maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

public partial class Program;
