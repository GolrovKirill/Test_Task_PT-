using PT.Api.Features.Documents;
using PT.Domain.Enums;

namespace PT.Tests;

internal static class TestData
{
    public static DocumentWriteRequest ValidRequest(
        string title = "Test document",
        string? ownerEmail = "owner@example.com",
        DocumentStatus status = DocumentStatus.Draft,
        DateOnly? archiveDate = null)
    {
        return new DocumentWriteRequest
        {
            Title = title,
            Description = "description",
            Content = "content",
            DocumentType = DocumentType.Contract,
            Status = status,
            Confidentiality = ConfidentialityLevel.Public,
            IsPublic = true,
            Priority = 10,
            QualityScore = 0.9m,
            SizeBytes = 1024,
            Tags = ["tag"],
            RelatedDocumentIds = [],
            OwnerEmail = ownerEmail,
            EffectiveFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ArchiveDate = archiveDate,
            Metadata = new Dictionary<string, string>
            {
                ["team"] = "soc",
            },
        };
    }
}
