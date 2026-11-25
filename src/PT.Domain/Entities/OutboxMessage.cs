namespace PT.Domain.Entities;

/// <summary>
/// A document change event written in the same transaction as the change itself
/// (transactional outbox).
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    /// <summary>Database-generated monotonic cursor for the event feed and publication order.</summary>
    public long Sequence { get; set; }

    public string Type { get; set; } = string.Empty;

    /// <summary>The serialized <see cref="PT.Domain.Events.DocumentEvent"/> payload as JSON.</summary>
    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset OccurredOn { get; set; }

    public DateTimeOffset? ProcessedOn { get; set; }

    public int RetryCount { get; set; }

    public DateTimeOffset? NextAttemptOn { get; set; }

    public string? LastError { get; set; }
}
