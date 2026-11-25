using System.Text.RegularExpressions;
using FluentValidation;
using PT.Domain.Enums;

namespace PT.Api.Features.Documents;

public sealed class DocumentWriteRequestValidator : AbstractValidator<DocumentWriteRequest>
{
    private static readonly Regex MetadataKeyPattern = new(
        "^[a-z0-9_.-]{1,50}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public DocumentWriteRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Title)
            .NotNull().WithMessage("title обязателен.")
            .Must(title => !string.IsNullOrWhiteSpace(title)).WithMessage("title не может быть пустым.")
            .Must(title => title is not null && title.Trim().Length is >= 3 and <= 200)
            .WithMessage("title должен быть от 3 до 200 символов (без учёта пробелов по краям).");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("description не должен превышать 2000 символов.");

        RuleFor(x => x.Content)
            .NotNull().WithMessage("content обязателен.")
            .Must(content => !string.IsNullOrWhiteSpace(content)).WithMessage("content не может быть пустым.")
            .MaximumLength(20000).WithMessage("content не должен превышать 20000 символов.");

        RuleFor(x => x.DocumentType)
            .NotNull().WithMessage("documentType обязателен.");

        RuleFor(x => x.Status)
            .NotNull().WithMessage("status обязателен.");

        RuleFor(x => x.Confidentiality)
            .NotNull().WithMessage("confidentiality обязателен.");

        RuleFor(x => x.Priority)
            .NotNull().WithMessage("priority обязателен.")
            .InclusiveBetween(0, 100).WithMessage("priority должен быть от 0 до 100.");

        RuleFor(x => x.QualityScore)
            .NotNull().WithMessage("qualityScore обязателен.")
            .InclusiveBetween(-99.99m, 99.99m).WithMessage("qualityScore должен быть от -99.99 до 99.99.")
            .PrecisionScale(4, 2, false).WithMessage("qualityScore должен иметь не более 2 знаков после запятой.");

        RuleFor(x => x.SizeBytes)
            .NotNull().WithMessage("sizeBytes обязателен.")
            .InclusiveBetween(0L, 1_000_000_000_000L).WithMessage("sizeBytes должен быть от 0 до 1 000 000 000 000.");

        RuleFor(x => x.Tags)
            .NotNull().WithMessage("tags не может быть null.")
            .Must(tags => tags is null || tags.Length <= 20).WithMessage("tags: не более 20 элементов.")
            .Must(tags => tags is null || tags.All(tag => !string.IsNullOrWhiteSpace(tag) && tag.Length <= 50))
            .WithMessage("tags: каждый тег должен быть непустой строкой до 50 символов.")
            .Must(tags => tags is null || tags.Distinct(StringComparer.OrdinalIgnoreCase).Count() == tags.Length)
            .WithMessage("tags: значения не должны повторяться.");

        RuleFor(x => x.RelatedDocumentIds)
            .NotNull().WithMessage("relatedDocumentIds не может быть null.")
            .Must(ids => ids is null || ids.Length <= 10).WithMessage("relatedDocumentIds: не более 10 элементов.")
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Length)
            .WithMessage("relatedDocumentIds: значения не должны повторяться.");

        RuleFor(x => x.OwnerEmail)
            .NotNull().WithMessage("ownerEmail обязателен.")
            .Must(email => !string.IsNullOrWhiteSpace(email)).WithMessage("ownerEmail не может быть пустым.")
            .MaximumLength(254).WithMessage("ownerEmail не должен превышать 254 символа.")
            .EmailAddress().WithMessage("ownerEmail должен быть валидным email.");

        RuleFor(x => x.EffectiveFrom)
            .NotNull().WithMessage("effectiveFrom обязателен.");

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(x => x.EffectiveFrom!.Value)
            .When(x => x.ExpiresAt.HasValue && x.EffectiveFrom.HasValue)
            .WithMessage("expiresAt должен быть больше effectiveFrom.");

        RuleFor(x => x.Metadata)
            .NotNull().WithMessage("metadata не может быть null.")
            .Must(metadata => metadata is null || metadata.Count <= 20).WithMessage("metadata: не более 20 пар.")
            .Must(metadata => metadata is null || metadata.All(pair => MetadataKeyPattern.IsMatch(pair.Key)))
            .WithMessage("metadata: ключи должны соответствовать ^[a-z0-9_.-]{1,50}$.")
            .Must(metadata => metadata is null || metadata.All(pair => pair.Value is not null))
            .WithMessage("metadata: значения не могут быть null.")
            .Must(metadata => metadata is null || metadata.All(pair => pair.Value is null || pair.Value.Length <= 200))
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
