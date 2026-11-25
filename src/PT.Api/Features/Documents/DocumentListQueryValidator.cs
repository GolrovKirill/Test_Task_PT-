using FluentValidation;

namespace PT.Api.Features.Documents;

public sealed class DocumentListQueryValidator : AbstractValidator<DocumentListQuery>
{
    private const int MinPage = 1;
    private const int MaxPage = 1_000_000;
    private const int MinPageSize = 1;
    private const int MaxPageSize = 100;

    public DocumentListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(MinPage).WithMessage("page должен быть не меньше 1.")
            .LessThanOrEqualTo(MaxPage).WithMessage("page слишком большой.")
            .When(x => x.Page.HasValue);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(MinPageSize, MaxPageSize).WithMessage("pageSize должен быть от 1 до 100.")
            .When(x => x.PageSize.HasValue);
    }
}
