using PT.Domain.Entities;

namespace PT.Api.Features.Documents;

public sealed record DocumentEventFeedItem(
    long Sequence,
    Guid EventId,
    Guid DocumentId,
    string EventType,
    DateTimeOffset OccurredAt,
    Document? Document,
    bool Published,
    int Attempts);
