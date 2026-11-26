using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using PT.Domain.Events;

namespace PT.Infrastructure.Events;

public sealed class HttpEventPublisher(
    HttpClient httpClient,
    ILogger<HttpEventPublisher> logger) : IEventPublisher
{
    public async Task PublishAsync(DocumentEvent documentEvent, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/v1/events",
            documentEvent,
            DocumentEventJson.Options,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"MockSiem returned {(int)response.StatusCode} {response.StatusCode}.",
                inner: null,
                statusCode: response.StatusCode);
        }

        logger.LogDebug("Event {EventId} published to MockSiem", documentEvent.EventId);
    }
}
