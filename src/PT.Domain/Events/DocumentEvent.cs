using PT.Domain.Entities;

namespace PT.Domain.Events;

public sealed record DocumentEvent(
    Guid EventId,
    Guid DocumentId,
    string EventType,
    DateTimeOffset OccurredAt,
    Document? Document);
