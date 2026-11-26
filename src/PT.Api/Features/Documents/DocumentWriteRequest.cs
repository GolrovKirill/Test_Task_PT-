using PT.Domain.Enums;

namespace PT.Api.Features.Documents;

/// <summary>
/// Client contract for creating or fully updating a document. Server-managed fields
/// (Id, CreatedAt, UpdatedAt) are not part of it. Nullable types distinguish "field omitted"
/// from "explicit default value"; requiredness is enforced by the validator.
/// </summary>
public sealed record DocumentWriteRequest
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? Content { get; init; }

    public DocumentType? DocumentType { get; init; }

    public DocumentStatus? Status { get; init; }

    public ConfidentialityLevel? Confidentiality { get; init; }

    public bool IsPublic { get; init; }

    public int? Priority { get; init; }

    public decimal? QualityScore { get; init; }

    public long? SizeBytes { get; init; }

    public string[] Tags { get; init; } = [];

    public Guid[] RelatedDocumentIds { get; init; } = [];

    public string? OwnerEmail { get; init; }

    public DateTimeOffset? EffectiveFrom { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public DateOnly? ArchiveDate { get; init; }

    public Dictionary<string, string> Metadata { get; init; } = [];
}
