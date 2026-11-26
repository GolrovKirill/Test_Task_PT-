using System.Text.RegularExpressions;
using FluentValidation;
using PT.Domain.Enums;

namespace PT.Api.Features.Documents;

public sealed class DocumentWriteRequestValidator : AbstractValidator<DocumentWriteRequest>
{
    private const int MinTitleLength = 3;
    private const int MaxTitleLength = 200;
    private const int MaxDescriptionLength = 2000;
    private const int MaxContentLength = 20000;
    private const int MinPriority = 0;
    private const int MaxPriority = 100;
    private const decimal MinQualityScore = -99.99m;
    private const decimal MaxQualityScore = 99.99m;
    private const int QualityScorePrecision = 4;
    private const int QualityScoreScale = 2;
    private const long MinSizeBytes = 0L;
    private const long MaxSizeBytes = 1_000_000_000_000L;
    private const int MaxTagsCount = 20;
    private const int MaxTagLength = 50;
    private const int MaxRelatedDocuments = 10;
    private const int MaxOwnerEmailLength = 254;
    private const int MaxMetadataEntries = 20;
    private const int MaxMetadataKeyLength = 50;
    private const int MaxMetadataValueLength = 200;

    private static readonly Regex MetadataKeyPattern = new(
        $"^[a-z0-9_.-]{{1,{MaxMetadataKeyLength}}}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public DocumentWriteRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Title)
            .NotNull().WithMessage("title обязателен.")
            .Must(title => !string.IsNullOrWhiteSpace(title)).WithMessage("title не может быть пустым.")
            .Must(title => title is not null && title.Trim().Length is >= MinTitleLength and <= MaxTitleLength)
            .WithMessage("title должен быть от 3 до 200 символов (без учёта пробелов по краям).");

        RuleFor(x => x.Description)
            .MaximumLength(MaxDescriptionLength).WithMessage("description не должен превышать 2000 символов.");

        RuleFor(x => x.Content)
            .NotNull().WithMessage("content обязателен.")
            .Must(content => !string.IsNullOrWhiteSpace(content)).WithMessage("content не может быть пустым.")
            .MaximumLength(MaxContentLength).WithMessage("content не должен превышать 20000 символов.");

        RuleFor(x => x.DocumentType)
            .NotNull().WithMessage("documentType обязателен.");

        RuleFor(x => x.Status)
            .NotNull().WithMessage("status обязателен.");

        RuleFor(x => x.Confidentiality)
            .NotNull().WithMessage("confidentiality обязателен.");

        RuleFor(x => x.Priority)
            .NotNull().WithMessage("priority обязателен.")
            .InclusiveBetween(MinPriority, MaxPriority).WithMessage("priority должен быть от 0 до 100.");

        RuleFor(x => x.QualityScore)
            .NotNull().WithMessage("qualityScore обязателен.")
            .InclusiveBetween(MinQualityScore, MaxQualityScore).WithMessage("qualityScore должен быть от -99.99 до 99.99.")
            .PrecisionScale(QualityScorePrecision, QualityScoreScale, false).WithMessage("qualityScore должен иметь не более 2 знаков после запятой.");

        RuleFor(x => x.SizeBytes)
            .NotNull().WithMessage("sizeBytes обязателен.")
            .InclusiveBetween(MinSizeBytes, MaxSizeBytes).WithMessage("sizeBytes должен быть от 0 до 1 000 000 000 000.");

        RuleFor(x => x.Tags)
            .NotNull().WithMessage("tags не может быть null.")
            .Must(tags => tags is null || tags.Length <= MaxTagsCount).WithMessage("tags: не более 20 элементов.")
            .Must(tags => tags is null || tags.All(tag => !string.IsNullOrWhiteSpace(tag) && tag.Length <= MaxTagLength))
            .WithMessage("tags: каждый тег должен быть непустой строкой до 50 символов.")
            .Must(tags => tags is null || tags.Distinct(StringComparer.OrdinalIgnoreCase).Count() == tags.Length)
            .WithMessage("tags: значения не должны повторяться.");

        RuleFor(x => x.RelatedDocumentIds)
            .NotNull().WithMessage("relatedDocumentIds не может быть null.")
            .Must(ids => ids is null || ids.Length <= MaxRelatedDocuments).WithMessage("relatedDocumentIds: не более 10 элементов.")
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Length)
            .WithMessage("relatedDocumentIds: значения не должны повторяться.");

        RuleFor(x => x.OwnerEmail)
            .NotNull().WithMessage("ownerEmail обязателен.")
            .Must(email => !string.IsNullOrWhiteSpace(email)).WithMessage("ownerEmail не может быть пустым.")
            .MaximumLength(MaxOwnerEmailLength).WithMessage("ownerEmail не должен превышать 254 символа.")
            .EmailAddress().WithMessage("ownerEmail должен быть валидным email.");

        RuleFor(x => x.EffectiveFrom)
            .NotNull().WithMessage("effectiveFrom обязателен.");

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(x => x.EffectiveFrom!.Value)
            .When(x => x.ExpiresAt.HasValue && x.EffectiveFrom.HasValue)
            .WithMessage("expiresAt должен быть больше effectiveFrom.");

        RuleFor(x => x.Metadata)
            .NotNull().WithMessage("metadata не может быть null.")
            .Must(metadata => metadata is null || metadata.Count <= MaxMetadataEntries).WithMessage("metadata: не более 20 пар.")
            .Must(metadata => metadata is null || metadata.All(pair => MetadataKeyPattern.IsMatch(pair.Key)))
            .WithMessage("metadata: ключи должны соответствовать ^[a-z0-9_.-]{1,50}$.")
            .Must(metadata => metadata is null || metadata.All(pair => pair.Value is not null))
            .WithMessage("metadata: значения не могут быть null.")
            .Must(metadata => metadata is null || metadata.All(pair => pair.Value is null || pair.Value.Length <= MaxMetadataValueLength))
            .WithMessage("metadata: значения не должны превышать 200 символов.");

        RuleFor(x => x.Confidentiality)
            .Equal(ConfidentialityLevel.Public)
            .When(x => x.IsPublic)
            .WithMessage("при isPublic=true confidentiality должна быть public.");

        RuleFor(x => x.ArchiveDate)
            .NotNull()
            .When(x => x.Status == DocumentStatus.Archived)
            .WithMessage("при status=archived archiveDate обязательна.");

        RuleFor(x => x.Status)
            .Equal(DocumentStatus.Archived)
            .When(x => x.ArchiveDate.HasValue)
            .WithMessage("archiveDate задаётся только при status=archived.");
    }
}
