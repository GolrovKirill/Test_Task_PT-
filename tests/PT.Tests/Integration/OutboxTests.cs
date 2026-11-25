using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PT.Api.Features.Documents;
using PT.Domain.Entities;
using PT.Domain.Events;
using PT.Infrastructure.Data;
using PT.Infrastructure.Outbox;

namespace PT.Tests.Integration;

[TestFixture]
public sealed class OutboxTests : DocumentsTestBase
{
    [Test]
    public async Task Create_WritesCreatedEvent()
    {
        var document = await CreateAsync("Outbox create");

        var messages = await GetMessagesAsync();

        Assert.That(messages, Has.Count.EqualTo(1));

        var message = messages[0];
        Assert.That(message.Type, Is.EqualTo(DocumentEventTypes.Created));
        Assert.That(message.ProcessedOn, Is.Null);

        var documentEvent = Deserialize(message);
        Assert.That(documentEvent.DocumentId, Is.EqualTo(document.Id));
        Assert.That(documentEvent.EventType, Is.EqualTo(DocumentEventTypes.Created));
        Assert.That(documentEvent.Document, Is.Not.Null);
        Assert.That(documentEvent.Document!.Title, Is.EqualTo("Outbox create"));
    }

    [Test]
    public async Task Update_WritesUpdatedEventWithNewSnapshot()
    {
        var document = await CreateAsync("Outbox before update");

        var update = ValidRequest("Outbox after update") with { Priority = 77 };
        using var response = await Client.PutAsJsonAsync(
            $"/api/v1/documents/{document.Id}",
            update,
            JsonOptions);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var messages = await GetMessagesAsync();
        Assert.That(messages, Has.Count.EqualTo(2));
        Assert.That(messages[0].Type, Is.EqualTo(DocumentEventTypes.Created));
        Assert.That(messages[1].Type, Is.EqualTo(DocumentEventTypes.Updated));

        var updatedEvent = Deserialize(messages[1]);
        Assert.That(updatedEvent.Document, Is.Not.Null);
        Assert.That(updatedEvent.Document!.Title, Is.EqualTo("Outbox after update"));
        Assert.That(updatedEvent.Document.Priority, Is.EqualTo(77));
    }

    [Test]
    public async Task Delete_WritesDeletedEventWithoutDocument()
    {
        var document = await CreateAsync("Outbox delete");

        using var response = await Client.DeleteAsync($"/api/v1/documents/{document.Id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var messages = await GetMessagesAsync();
        Assert.That(messages, Has.Count.EqualTo(2));

        var deletedEvent = Deserialize(messages[1]);
        Assert.That(deletedEvent.EventType, Is.EqualTo(DocumentEventTypes.Deleted));
        Assert.That(deletedEvent.DocumentId, Is.EqualTo(document.Id));
        Assert.That(deletedEvent.Document, Is.Null);
    }

    [Test]
    public async Task Feed_ReturnsEventsWithCursor()
    {
        var document = await CreateAsync("Feed event");

        var update = ValidRequest("Feed event updated");
        using var updateResponse = await Client.PutAsJsonAsync(
            $"/api/v1/documents/{document.Id}",
            update,
            JsonOptions);
        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var deleteResponse = await Client.DeleteAsync($"/api/v1/documents/{document.Id}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var page = await GetEventsAsync(string.Empty);

        Assert.That(page.Items, Has.Count.EqualTo(3));
        Assert.That(page.Items.Select(item => item.EventType), Is.EqualTo(new[]
        {
            DocumentEventTypes.Created,
            DocumentEventTypes.Updated,
            DocumentEventTypes.Deleted,
        }));
        Assert.That(page.Items[2].Document, Is.Null);
        Assert.That(page.Items.All(item => !item.Published), Is.True);
        Assert.That(page.Items.All(item => item.Attempts == 0), Is.True);
        Assert.That(page.HasMore, Is.False);
        Assert.That(page.NextSince, Is.EqualTo(page.Items[^1].Sequence));

        var firstPage = await GetEventsAsync("?limit=2");
        Assert.That(firstPage.Items, Has.Count.EqualTo(2));
        Assert.That(firstPage.HasMore, Is.True);

        var secondPage = await GetEventsAsync($"?since={firstPage.NextSince}&limit=2");
        Assert.That(secondPage.Items, Has.Count.EqualTo(1));
        Assert.That(secondPage.Items[0].EventType, Is.EqualTo(DocumentEventTypes.Deleted));
        Assert.That(secondPage.HasMore, Is.False);
    }

    [TestCase("?since=-1")]
    [TestCase("?limit=0")]
    [TestCase("?limit=201")]
    public async Task Feed_InvalidQuery_ReturnsBadRequest(string query)
    {
        using var response = await Client.GetAsync($"/api/v1/documents/events{query}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Processor_PublishesEventAndMarksItProcessed()
    {
        await CreateAsync("Processor success");

        var processed = await ProcessAsync();

        Assert.That(processed, Is.EqualTo(1));
        Assert.That(TestHost.EventPublisher.Events, Has.Count.EqualTo(1));

        var message = (await GetMessagesAsync()).Single();
        Assert.That(message.ProcessedOn, Is.Not.Null);
        Assert.That(message.NextAttemptOn, Is.Null);
        Assert.That(message.RetryCount, Is.Zero);
    }

    [Test]
    public async Task Processor_RetriesAfterPublisherFailure()
    {
        await CreateAsync("Processor retry");

        TestHost.EventPublisher.FailuresRemaining = 1;
        await ProcessAsync();

        var failed = (await GetMessagesAsync()).Single();
        Assert.That(failed.ProcessedOn, Is.Null);
        Assert.That(failed.RetryCount, Is.EqualTo(1));
        Assert.That(failed.NextAttemptOn, Is.Not.Null);
        Assert.That(TestHost.EventPublisher.Events, Is.Empty);

        await MakeMessageDueAsync(failed.Id);
        await ProcessAsync();

        var retried = (await GetMessagesAsync()).Single();
        Assert.That(retried.ProcessedOn, Is.Not.Null);
        Assert.That(retried.RetryCount, Is.EqualTo(1));
        Assert.That(TestHost.EventPublisher.Events, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TwoProcessors_DoNotProcessTheSameMessage()
    {
        await CreateAsync("Concurrent 1");
        await CreateAsync("Concurrent 2");

        using var firstScope = TestHost.Factory.Services.CreateScope();
        using var secondScope = TestHost.Factory.Services.CreateScope();

        var firstProcessor = firstScope.ServiceProvider.GetRequiredService<OutboxProcessor>();
        var secondProcessor = secondScope.ServiceProvider.GetRequiredService<OutboxProcessor>();

        var results = await Task.WhenAll(
            firstProcessor.ProcessBatchAsync(1),
            secondProcessor.ProcessBatchAsync(1));

        Assert.That(results.Sum(), Is.EqualTo(2));
        Assert.That(TestHost.EventPublisher.Events, Has.Count.EqualTo(2));
        Assert.That(
            TestHost.EventPublisher.Events.Select(item => item.EventId).Distinct().Count(),
            Is.EqualTo(2));

        var messages = await GetMessagesAsync();
        Assert.That(messages.All(message => message.ProcessedOn is not null), Is.True);
    }

    [Test]
    public async Task Processor_StopsAfterMaxRetries()
    {
        await CreateAsync("Processor max retries");

        TestHost.EventPublisher.FailuresRemaining = OutboxProcessor.MaxRetries;

        for (var attempt = 1; attempt <= OutboxProcessor.MaxRetries; attempt++)
        {
            await ProcessAsync();

            var message = (await GetMessagesAsync()).Single();
            Assert.That(message.RetryCount, Is.EqualTo(attempt));

            if (attempt < OutboxProcessor.MaxRetries)
            {
                await MakeMessageDueAsync(message.Id);
            }
        }

        var failed = (await GetMessagesAsync()).Single();
        Assert.That(failed.ProcessedOn, Is.Null);
        Assert.That(failed.RetryCount, Is.EqualTo(OutboxProcessor.MaxRetries));
        Assert.That(failed.NextAttemptOn, Is.Null);

        var processedAgain = await ProcessAsync();
        Assert.That(processedAgain, Is.Zero);
        Assert.That(TestHost.EventPublisher.Events, Is.Empty);
    }

    private static DocumentEvent Deserialize(OutboxMessage message) =>
        JsonSerializer.Deserialize<DocumentEvent>(message.Payload, DocumentEventJson.Options)!;

    private static async Task<List<OutboxMessage>> GetMessagesAsync()
    {
        using var scope = TestHost.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.OutboxMessages
            .AsNoTracking()
            .OrderBy(message => message.Sequence)
            .ToListAsync();
    }

    private static async Task<int> ProcessAsync()
    {
        using var scope = TestHost.Factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

        return await processor.ProcessBatchAsync();
    }

    private static async Task MakeMessageDueAsync(Guid messageId)
    {
        using var scope = TestHost.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = await db.OutboxMessages.SingleAsync(x => x.Id == messageId);

        message.NextAttemptOn = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
    }

    private async Task<DocumentEventFeedPage> GetEventsAsync(string query)
    {
        using var response = await Client.GetAsync($"/api/v1/documents/events{query}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var page = await response.Content.ReadFromJsonAsync<DocumentEventFeedPage>(JsonOptions);
        Assert.That(page, Is.Not.Null);
        return page!;
    }
}
