namespace PT.Infrastructure.Export;

public sealed class ExportOptions
{
    public const int DefaultBatchSize = 100;

    public int BatchSize { get; set; } = DefaultBatchSize;
}
