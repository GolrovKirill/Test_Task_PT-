using FluentValidation.TestHelper;
using NUnit.Framework;
using PT.Api.Features.Documents;

namespace PT.Tests.Unit;

[TestFixture]
public sealed class DocumentListQueryValidatorTests
{
    private readonly DocumentListQueryValidator _validator = new();

    [Test]
    public void EmptyQuery_IsValid()
    {
        var result = _validator.TestValidate(new DocumentListQuery());
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void PageZero_IsInvalid()
    {
        var result = _validator.TestValidate(new DocumentListQuery { Page = 0 });
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Test]
    public void PageSizeZero_IsInvalid()
    {
        var result = _validator.TestValidate(new DocumentListQuery { PageSize = 0 });
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Test]
    public void PageSizeTooLarge_IsInvalid()
    {
        var result = _validator.TestValidate(new DocumentListQuery { PageSize = 101 });
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Test]
    public void BoundaryValues_AreValid()
    {
        var result = _validator.TestValidate(new DocumentListQuery
        {
            Page = 1_000_000,
            PageSize = 100,
        });

        Assert.That(result.IsValid, Is.True);
    }
}
