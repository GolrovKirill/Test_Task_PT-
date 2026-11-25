using PT.Domain.Enums;

namespace PT.Api.Contracts;

/// <summary>
/// Клиентский контракт создания документа. Серверные поля (Id, CreatedAt, UpdatedAt)
/// сюда не входят. Nullable-типы позволяют отличить «поле не прислали» от
/// «прислали значение по умолчанию»: обязательность проверяется валидатором.
/// </summary>
public sealed class CreateDocumentRequest
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
