using PT.Domain.Entities;
using PT.Infrastructure.Export;

namespace PT.Tests.Integration;

public sealed class RecordingDocumentExporter : IExternalDocumentExporter
{
    private readonly List<IReadOnlyList<Document>> _batches = [];

    public int FailuresRemaining { get; set; }

    public IReadOnlyList<IReadOnlyList<Document>> Batches => _batches;

    public Task ExportBatchAsync(
        IReadOnlyList<Document> documents,
        CancellationToken cancellationToken)
    {
        if (FailuresRemaining > 0)
        {
            FailuresRemaining--;
            throw new HttpRequestException("Simulated export failure.");
        }

        _batches.Add(documents);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        _batches.Clear();
        FailuresRemaining = 0;
    }
}
