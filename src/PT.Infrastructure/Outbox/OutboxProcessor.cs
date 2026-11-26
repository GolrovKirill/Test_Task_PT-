using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PT.Domain.Events;
using PT.Infrastructure.Data;
using PT.Infrastructure.Events;

namespace PT.Infrastructure.Outbox;

public sealed class OutboxProcessor(
    AppDbContext db,
    IEventPublisher publisher,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger)
{
    public const int MaxRetries = 8;
    public const int DefaultBatchSize = 50;
    private const int MaxBackoffSeconds = 60;

    // The lease must outlive the worst-case batch: 50 messages * 5s HTTP timeout = 250s.
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    public async Task<int> ProcessBatchAsync(
        int batchSize = DefaultBatchSize,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        // Short transaction: only claim the rows.
        var ids = await ClaimBatchAsync(batchSize, now, cancellationToken);
        if (ids.Count == 0)
        {
            return 0;
        }

        var messages = await db.OutboxMessages
            .Where(message => ids.Contains(message.Id))
            .OrderBy(message => message.Sequence)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var documentEvent = JsonSerializer.Deserialize<DocumentEvent>(
                                        message.Payload,
                                        DocumentEventJson.Options)
                                    ?? throw new InvalidOperationException(
                                        $"Outbox message {message.Id} has an empty payload.");

                await publisher.PublishAsync(documentEvent, cancellationToken);

                message.ProcessedOn = timeProvider.GetUtcNow();
                message.NextAttemptOn = null;
                message.LastError = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                message.RetryCount++;
                message.LastError = exception.Message;
                message.NextAttemptOn = message.RetryCount >= MaxRetries
                    ? null
                    : timeProvider.GetUtcNow().Add(GetBackoff(message.RetryCount));

                logger.LogWarning(
                    exception,
                    "Failed to publish outbox message {MessageId}; attempt {Attempt}",
                    message.Id,
                    message.RetryCount);
            }

            // Persist each message separately so a crash never rolls back already-published ones.
            await db.SaveChangesAsync(cancellationToken);
        }

        return messages.Count;

        static TimeSpan GetBackoff(int retryCount) =>
            TimeSpan.FromSeconds(Math.Min(MaxBackoffSeconds, Math.Pow(2, retryCount)));
    }

    private async Task<List<Guid>> ClaimBatchAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // The transaction only wraps SELECT ... FOR UPDATE + the lease UPDATE; no HTTP inside.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var ids = await db.Database.SqlQueryRaw<Guid>(
            """
            SELECT "Id"
            FROM "OutboxMessages"
            WHERE "ProcessedOn" IS NULL
              AND "RetryCount" < {0}
              AND ("NextAttemptOn" IS NULL OR "NextAttemptOn" <= {1})
            ORDER BY "Sequence"
            FOR UPDATE SKIP LOCKED
            LIMIT {2}
            """,
            MaxRetries,
            now,
            batchSize).ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return ids;
        }

        // Rows are now leased for ClaimLease; while NextAttemptOn is in the future other workers skip them.
        var leaseUntil = now.Add(ClaimLease);

        await db.OutboxMessages
            .Where(message => ids.Contains(message.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(message => message.NextAttemptOn, leaseUntil),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return ids;
    }
}
