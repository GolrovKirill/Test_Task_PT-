using FluentValidation;

namespace PT.Api.Features.Documents;

public sealed class DocumentEventQueryValidator : AbstractValidator<DocumentEventQuery>
{
    private const int MinSince = 0;
    private const int MinLimit = 1;
    private const int MaxLimit = 200;

    public DocumentEventQueryValidator()
    {
        RuleFor(x => x.Since)
            .GreaterThanOrEqualTo(MinSince).WithMessage("since должен быть не меньше 0.")
            .When(x => x.Since.HasValue);

        RuleFor(x => x.Limit)
            .InclusiveBetween(MinLimit, MaxLimit).WithMessage("limit должен быть от 1 до 200.")
            .When(x => x.Limit.HasValue);
    }
}
