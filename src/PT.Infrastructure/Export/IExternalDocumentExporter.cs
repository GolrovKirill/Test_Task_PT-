using PT.Domain.Entities;

namespace PT.Infrastructure.Export;

public interface IExternalDocumentExporter
{
    Task ExportBatchAsync(
        IReadOnlyList<Document> documents,
        CancellationToken cancellationToken);
}
