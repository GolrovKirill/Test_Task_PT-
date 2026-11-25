using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PT.MockSiem;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(new MockState(
    builder.Configuration.GetValue("FailFirstNRequests", 0)));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/v1/events", (MockState state) => Results.Ok(new
{
    count = state.Count,
    items = state.Events,
}));

app.MapPost("/api/v1/events", (JsonElement body, MockState state) =>
{
    if (state.ShouldFail())
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    if (!body.TryGetProperty("eventId", out var eventIdProperty)
        || !Guid.TryParse(eventIdProperty.GetString(), out var eventId))
    {
        return Results.BadRequest(new { error = "eventId is required" });
    }

    var added = state.TryAdd(eventId, body.Clone());

    return Results.Accepted("/api/v1/events", new
    {
        accepted = true,
        duplicate = !added,
    });
});

app.MapDelete("/api/v1/events", (MockState state) =>
{
    state.Clear();
    return Results.NoContent();
});

app.Run();

public partial class Program;
