namespace PT.Api.Features.Documents;

public sealed record DocumentEventFeedPage(
    List<DocumentEventFeedItem> Items,
    long NextSince,
    bool HasMore);
