using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PT.Api.Common;
using PT.Api.Features.Documents;
using PT.Infrastructure.Data;
using PT.Infrastructure.Events;
using PT.Infrastructure.Export;
using PT.Infrastructure.Outbox;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

const int HttpClientTimeoutSeconds = 5;
const int MigrationRetryDelaySeconds = 2;

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
builder.Services.AddScoped<ValidationFilter<DocumentEventQuery>>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<OutboxProcessor>();
builder.Services.AddScoped<DocumentExportService>();
builder.Services.Configure<ExportOptions>(builder.Configuration.GetSection("Export"));
builder.Services.AddHttpClient<IEventPublisher, HttpEventPublisher>(client =>
{
    var mockSiemBaseUrl = builder.Configuration["MockSiem:BaseUrl"] ?? "http://127.0.0.1:5082/";
    client.BaseAddress = new Uri(mockSiemBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(HttpClientTimeoutSeconds);
});
builder.Services.AddHttpClient<IExternalDocumentExporter, HttpDocumentExporter>(client =>
{
    var mockSiemBaseUrl = builder.Configuration["MockSiem:BaseUrl"] ?? "http://127.0.0.1:5082/";
    client.BaseAddress = new Uri(mockSiemBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(HttpClientTimeoutSeconds);
});

if (builder.Configuration.GetValue("Outbox:DispatcherEnabled", true))
{
    builder.Services.AddHostedService<OutboxDispatcher>();
}

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
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).WithTags("Health");
app.MapGet("/health/ready", async (AppDbContext db, CancellationToken cancellationToken) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
    .WithTags("Health");
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
                "Database migration attempt {Attempt}/{MaxAttempts} failed; retrying in {DelaySeconds}s",
                attempt,
                maxAttempts,
                MigrationRetryDelaySeconds);
            await Task.Delay(TimeSpan.FromSeconds(MigrationRetryDelaySeconds));
        }
    }
}

public partial class Program;
