using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PT.MockSiem;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(new MockState(
    builder.Configuration.GetValue("FailFirstNRequests", 0),
    builder.Configuration.GetValue("FailFirstNImportRequests", 0)));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/v1/events", (MockState state) => Results.Ok(new
{
    count = state.EventCount,
    items = state.Events,
}));

app.MapPost("/api/v1/events", (JsonElement body, MockState state) =>
{
    if (state.ShouldFailEvent())
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    if (!body.TryGetProperty("eventId", out var eventIdProperty)
        || !Guid.TryParse(eventIdProperty.GetString(), out var eventId))
    {
        return Results.BadRequest(new { error = "eventId is required" });
    }

    var added = state.TryAddEvent(eventId, body.Clone());

    return Results.Accepted("/api/v1/events", new
    {
        accepted = true,
        duplicate = !added,
    });
});

app.MapDelete("/api/v1/events", (MockState state) =>
{
    state.ClearEvents();
    return Results.NoContent();
});

app.MapGet("/api/v1/documents", (MockState state) => Results.Ok(new
{
    count = state.DocumentCount,
    items = state.Documents,
}));

app.MapPost("/api/v1/import", (JsonElement body, MockState state) =>
{
    if (state.ShouldFailImport())
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    if (!body.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
    {
        return Results.BadRequest(new { error = "items[] is required" });
    }

    var (received, added) = state.Import(items.EnumerateArray());

    return Results.Ok(new
    {
        received,
        added,
    });
});

app.MapDelete("/api/v1/documents", (MockState state) =>
{
    state.ClearDocuments();
    return Results.NoContent();
});

app.Run();

public partial class Program;
