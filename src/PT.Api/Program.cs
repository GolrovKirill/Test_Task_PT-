using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PT.Api.Common;
using PT.Api.Contracts;
using PT.Api.Validation;
using PT.Domain.Entities;
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
builder.Services.AddValidatorsFromAssemblyContaining<CreateDocumentRequestValidator>();
builder.Services.AddScoped<ValidationFilter<CreateDocumentRequest>>();
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

app.MapPost("/api/v1/documents", async (
    CreateDocumentRequest request,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var now = DateTimeOffset.UtcNow;

    var document = new Document
    {
        Id = Guid.NewGuid(),
        Title = request.Title!.Trim(),
        Description = request.Description,
        Content = request.Content!,
        DocumentType = request.DocumentType!.Value,
        Status = request.Status!.Value,
        Confidentiality = request.Confidentiality!.Value,
        IsPublic = request.IsPublic,
        Priority = request.Priority!.Value,
        QualityScore = request.QualityScore!.Value,
        SizeBytes = request.SizeBytes!.Value,
        Tags = request.Tags,
        RelatedDocumentIds = request.RelatedDocumentIds,
        OwnerEmail = request.OwnerEmail!,
        EffectiveFrom = request.EffectiveFrom!.Value,
        ExpiresAt = request.ExpiresAt,
        CreatedAt = now,
        UpdatedAt = now,
        ArchiveDate = request.ArchiveDate,
        Metadata = request.Metadata,
    };

    db.Documents.Add(document);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/v1/documents/{document.Id}", document);
})
.WithSummary("Создать документ")
.WithDescription("Создаёт документ. Id и даты CreatedAt/UpdatedAt назначает сервер.")
.Produces<Document>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status409Conflict)
.AddEndpointFilter<ValidationFilter<CreateDocumentRequest>>();

app.MapGet("/api/v1/documents/{id:guid}", async (
    Guid id,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var document = await db.Documents
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    return document is null
        ? Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Документ не найден",
            detail: $"Документ с id '{id}' не существует.")
        : Results.Ok(document);
})
.WithSummary("Получить документ по Id")
.Produces<Document>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status404NotFound);

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
