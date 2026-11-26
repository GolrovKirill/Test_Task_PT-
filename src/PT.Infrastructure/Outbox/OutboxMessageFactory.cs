using System.Text.Json;
using PT.Domain.Entities;
using PT.Domain.Events;

namespace PT.Infrastructure.Outbox;

public static class OutboxMessageFactory
{
    public static OutboxMessage Create(DocumentEvent documentEvent)
    {
        return new OutboxMessage
        {
            Id = documentEvent.EventId,
            Type = documentEvent.EventType,
            Payload = JsonSerializer.Serialize(documentEvent, DocumentEventJson.Options),
            OccurredOn = documentEvent.OccurredAt,
            RetryCount = 0,
            NextAttemptOn = null,
        };
    }
}
