using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using PT.Domain.Entities;
using PT.Infrastructure.Export;

namespace PT.Tests.Unit;

[TestFixture]
public sealed class HttpDocumentExporterTests
{
    [Test]
    public async Task ExportBatchAsync_RetriesRetryableStatuses()
    {
        var handler = new StubHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadGateway,
            HttpStatusCode.OK);

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://mock/"),
        };

        var exporter = new HttpDocumentExporter(
            httpClient,
            NullLogger<HttpDocumentExporter>.Instance);

        await exporter.ExportBatchAsync([CreateDocument()], CancellationToken.None);

        Assert.That(handler.Requests, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task ExportBatchAsync_DoesNotRetryClientError()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest);

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://mock/"),
        };

        var exporter = new HttpDocumentExporter(
            httpClient,
            NullLogger<HttpDocumentExporter>.Instance);

        Assert.ThrowsAsync<HttpRequestException>(
            () => exporter.ExportBatchAsync([CreateDocument()], CancellationToken.None));

        Assert.That(handler.Requests, Has.Count.EqualTo(1));
    }

    private static Document CreateDocument() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Exported document",
        Content = "content",
        OwnerEmail = "owner@example.com",
        EffectiveFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        Tags = [],
        RelatedDocumentIds = [],
        Metadata = [],
    };

    private sealed class StubHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private int _index;

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            var status = statuses[Math.Min(_index++, statuses.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
