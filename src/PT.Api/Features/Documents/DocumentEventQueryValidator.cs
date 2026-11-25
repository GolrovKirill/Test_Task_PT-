using FluentValidation;

namespace PT.Api.Features.Documents;

public sealed class DocumentEventQueryValidator : AbstractValidator<DocumentEventQuery>
{
    public DocumentEventQueryValidator()
    {
        RuleFor(x => x.Since)
            .GreaterThanOrEqualTo(0).WithMessage("since должен быть не меньше 0.")
            .When(x => x.Since.HasValue);

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 200).WithMessage("limit должен быть от 1 до 200.")
            .When(x => x.Limit.HasValue);
    }
}
