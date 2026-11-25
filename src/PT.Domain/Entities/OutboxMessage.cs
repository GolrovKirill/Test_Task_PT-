namespace PT.Domain.Entities;

/// <summary>
/// Событие изменения документа, записываемое в ту же транзакцию, что и изменение
/// (transactional outbox).
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    /// <summary>Монотонный курсор ленты событий и порядка публикации; генерирует БД.</summary>
    public long Sequence { get; set; }

    public string Type { get; set; } = string.Empty;

    /// <summary>Сериализованное событие в JSON.</summary>
    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset OccurredOn { get; set; }

    public DateTimeOffset? ProcessedOn { get; set; }

    public int RetryCount { get; set; }

    public DateTimeOffset? NextAttemptOn { get; set; }

    public string? LastError { get; set; }
}
