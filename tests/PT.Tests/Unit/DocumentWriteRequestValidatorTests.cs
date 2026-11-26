using FluentValidation.TestHelper;
using NUnit.Framework;
using PT.Api.Features.Documents;
using PT.Domain.Enums;

namespace PT.Tests.Unit;

[TestFixture]
public sealed class DocumentWriteRequestValidatorTests
{
    private readonly DocumentWriteRequestValidator _validator = new();

    [Test]
    public void ValidRequest_HasNoErrors()
    {
        var result = _validator.TestValidate(TestData.ValidRequest());
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void MissingTitle_IsInvalid()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { Title = null });
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Test]
    public void TitleWithSpacesTooShort_IsInvalid()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { Title = "  a  " });
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Test]
    public void MissingDocumentType_IsInvalid()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { DocumentType = null });
        result.ShouldHaveValidationErrorFor(x => x.DocumentType);
    }

    [Test]
    public void MissingPriority_IsInvalid()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { Priority = null });
        result.ShouldHaveValidationErrorFor(x => x.Priority);
    }

    [Test]
    public void MissingOwnerEmail_IsInvalid()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { OwnerEmail = null });
        result.ShouldHaveValidationErrorFor(x => x.OwnerEmail);
    }

    [Test]
    public void QualityScoreOutOfRange_IsInvalid()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { QualityScore = 100m });
        result.ShouldHaveValidationErrorFor(x => x.QualityScore);
    }

    [Test]
    public void DuplicateTagsIgnoringCase_AreInvalid()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { Tags = ["A", "a"] });
        result.ShouldHaveValidationErrorFor(x => x.Tags);
    }

    [Test]
    public void InvalidMetadataKey_IsInvalid()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with
        {
            Metadata = new Dictionary<string, string>
            {
                ["BAD KEY"] = "value",
            },
        });

        result.ShouldHaveValidationErrorFor(x => x.Metadata);
    }

    [Test]
    public void IsPublicWithInternalConfidentiality_IsInvalid()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with
        {
            IsPublic = true,
            Confidentiality = ConfidentialityLevel.Internal,
        });

        result.ShouldHaveValidationErrorFor(x => x.Confidentiality);
    }

    [Test]
    public void ExpiresAtBeforeEffectiveFrom_IsInvalid()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with
        {
            ExpiresAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        });

        result.ShouldHaveValidationErrorFor(x => x.ExpiresAt);
    }

    [Test]
    public void ArchivedWithoutArchiveDate_IsInvalid()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with
        {
            Status = DocumentStatus.Archived,
        });

        result.ShouldHaveValidationErrorFor(x => x.ArchiveDate);
    }

    [Test]
    public void ArchiveDateWithDraftStatus_IsInvalid()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with
        {
            ArchiveDate = new DateOnly(2026, 1, 1),
        });

        result.ShouldHaveValidationErrorFor(x => x.Status);
    }
}
