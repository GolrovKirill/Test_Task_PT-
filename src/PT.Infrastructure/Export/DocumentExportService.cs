using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PT.Infrastructure.Data;

namespace PT.Infrastructure.Export;

public sealed class DocumentExportService(
    AppDbContext db,
    IExternalDocumentExporter exporter,
    IOptions<ExportOptions> options,
    TimeProvider timeProvider,
    ILogger<DocumentExportService> logger)
{
    private const int MinBatchSize = 1;
    private const int MaxBatchSize = 500;

    public async Task<ExportResult> ExportAllAsync(CancellationToken cancellationToken)
    {
        var started = timeProvider.GetUtcNow();
        var batchSize = Math.Clamp(options.Value.BatchSize, MinBatchSize, MaxBatchSize);

        var ids = await db.Documents
            .AsNoTracking()
            .OrderBy(document => document.Id)
            .Select(document => document.Id)
            .ToListAsync(cancellationToken);

        var errors = new List<ExportBatchError>();
        var exported = 0;
        var batches = 0;

        foreach (var chunk in ids.Chunk(batchSize))
        {
            var documents = await db.Documents
                .AsNoTracking()
                .Where(document => chunk.Contains(document.Id))
                .OrderBy(document => document.Id)
                .ToListAsync(cancellationToken);

            if (documents.Count == 0)
            {
                continue;
            }

            var batchNumber = ++batches;

            if (documents.Count != chunk.Length)
            {
                errors.Add(new ExportBatchError(
                    batchNumber,
                    $"Не найдено документов из батча: {chunk.Length - documents.Count}."));
            }

            try
            {
                await exporter.ExportBatchAsync(documents, cancellationToken);
                exported += documents.Count;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                errors.Add(new ExportBatchError(batchNumber, exception.Message));
                logger.LogWarning(
                    exception,
                    "Export batch {BatchNumber} failed ({Count} documents)",
                    batchNumber,
                    documents.Count);
            }
        }

        var duration = (long)(timeProvider.GetUtcNow() - started).TotalMilliseconds;

        logger.LogInformation(
            "Export finished: total={Total} exported={Exported} failed={Failed} batches={Batches} durationMs={DurationMs}",
            ids.Count,
            exported,
            ids.Count - exported,
            batches,
            duration);

        return new ExportResult(
            Total: ids.Count,
            Exported: exported,
            Failed: ids.Count - exported,
            Batches: batches,
            DurationMs: duration,
            Errors: errors);
    }
}
