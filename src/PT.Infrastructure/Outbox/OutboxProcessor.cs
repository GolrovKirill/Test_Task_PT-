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

    // Бронь должна быть длиннее, чем максимальная обработка пачки:
    // 50 сообщений * 5 секунд таймаута HTTP = 250 секунд.
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    public async Task<int> ProcessBatchAsync(
        int batchSize = DefaultBatchSize,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        // Короткая транзакция: только забронировать строки.
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

            // Сохраняем результат каждого сообщения отдельно:
            // падение процесса не откатит уже опубликованные сообщения.
            await db.SaveChangesAsync(cancellationToken);
        }

        return messages.Count;

        static TimeSpan GetBackoff(int retryCount) =>
            TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, retryCount)));
    }

    private async Task<List<Guid>> ClaimBatchAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Транзакция нужна только для SELECT ... FOR UPDATE + UPDATE lease.
        // HTTP-запросы внутри неё не выполняются.
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

        // Теперь строки забронированы на ClaimLease.
        // Пока NextAttemptOn в будущем, другие воркеры их не возьмут.
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
