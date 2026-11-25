using PT.Domain.Enums;

namespace PT.Domain.Entities;

/// <summary>
/// Документ — основная сущность задания: 20 полей, 17 клиентских и 3 серверных
/// (Id, CreatedAt, UpdatedAt).
/// </summary>
public sealed class Document
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Content { get; set; } = string.Empty;

    public DocumentType DocumentType { get; set; }

    public DocumentStatus Status { get; set; }

    public ConfidentialityLevel Confidentiality { get; set; }

    public bool IsPublic { get; set; }

    public int Priority { get; set; }

    public decimal QualityScore { get; set; }

    public long SizeBytes { get; set; }

    public string[] Tags { get; set; } = [];

    public Guid[] RelatedDocumentIds { get; set; } = [];

    public string OwnerEmail { get; set; } = string.Empty;

    public DateTimeOffset EffectiveFrom { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateOnly? ArchiveDate { get; set; }

    public Dictionary<string, string> Metadata { get; set; } = [];
}
