using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;
using PT.Infrastructure.Export;

namespace PT.Tests.Integration;

[TestFixture]
public sealed class DocumentsExportTests : DocumentsTestBase
{
    [Test]
    public async Task Export_ReturnsSummaryAndSendsAllDocuments()
    {
        for (var index = 1; index <= 3; index++)
        {
            await CreateAsync($"Export {index}");
        }

        using var response = await Client.PostAsync("/api/v1/documents/export", content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<ExportResult>(JsonOptions);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Total, Is.EqualTo(3));
        Assert.That(result.Exported, Is.EqualTo(3));
        Assert.That(result.Failed, Is.Zero);
        Assert.That(result.Batches, Is.EqualTo(2));
        Assert.That(result.Errors, Is.Empty);

        Assert.That(TestHost.ExportExporter.Batches, Has.Count.EqualTo(2));
        Assert.That(
            TestHost.ExportExporter.Batches.Sum(batch => batch.Count),
            Is.EqualTo(3));
    }

    [Test]
    public async Task Export_NoDocuments_ReturnsZeroSummary()
    {
        using var response = await Client.PostAsync("/api/v1/documents/export", content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<ExportResult>(JsonOptions);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Total, Is.Zero);
        Assert.That(result.Exported, Is.Zero);
        Assert.That(result.Failed, Is.Zero);
        Assert.That(result.Batches, Is.Zero);
        Assert.That(TestHost.ExportExporter.Batches, Is.Empty);
    }

    [Test]
    public async Task Export_PartialFailure_ReturnsOkWithErrors()
    {
        for (var index = 1; index <= 3; index++)
        {
            await CreateAsync($"Partial export {index}");
        }

        TestHost.ExportExporter.FailuresRemaining = 1;

        using var response = await Client.PostAsync("/api/v1/documents/export", content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<ExportResult>(JsonOptions);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Total, Is.EqualTo(3));
        Assert.That(result.Exported, Is.EqualTo(1));
        Assert.That(result.Failed, Is.EqualTo(2));
        Assert.That(result.Batches, Is.EqualTo(2));
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(TestHost.ExportExporter.Batches, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Export_AllBatchesFail_ReturnsBadGateway()
    {
        for (var index = 1; index <= 3; index++)
        {
            await CreateAsync($"Failed export {index}");
        }

        TestHost.ExportExporter.FailuresRemaining = 2;

        using var response = await Client.PostAsync("/api/v1/documents/export", content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
        Assert.That(TestHost.ExportExporter.Batches, Is.Empty);

        var problem = await ReadJsonAsync(response);
        Assert.That(problem.GetProperty("status").GetInt32(), Is.EqualTo(502));
    }
}
