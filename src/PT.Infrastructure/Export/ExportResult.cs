namespace PT.Infrastructure.Export;

public sealed record ExportResult(
    int Total,
    int Exported,
    int Failed,
    int Batches,
    long DurationMs,
    List<ExportBatchError> Errors);
