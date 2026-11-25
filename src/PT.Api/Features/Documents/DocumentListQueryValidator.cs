using FluentValidation;

namespace PT.Api.Features.Documents;

public sealed class DocumentListQueryValidator : AbstractValidator<DocumentListQuery>
{
    public DocumentListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("page должен быть не меньше 1.")
            .LessThanOrEqualTo(1_000_000).WithMessage("page слишком большой.")
            .When(x => x.Page.HasValue);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("pageSize должен быть от 1 до 100.")
            .When(x => x.PageSize.HasValue);
    }
}
