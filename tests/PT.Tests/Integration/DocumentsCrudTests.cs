using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using PT.Api.Features.Documents;
using PT.Domain.Entities;

namespace PT.Tests.Integration;

[TestFixture]
public sealed class DocumentsCrudTests : DocumentsTestBase
{
    [Test]
    public async Task Create_ReturnsCreatedDocument()
    {
        using var response = await Client.PostAsJsonAsync(
            "/api/v1/documents",
            ValidRequest("Create test"),
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location, Is.Not.Null);

        var document = await response.Content.ReadFromJsonAsync<Document>(JsonOptions);
        Assert.That(document, Is.Not.Null);
        Assert.That(document!.Title, Is.EqualTo("Create test"));
        Assert.That(document.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(document.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        Assert.That(document.UpdatedAt, Is.EqualTo(document.CreatedAt));
    }

    [Test]
    public async Task Create_DuplicateTitle_ReturnsConflict()
    {
        await CreateAsync("Duplicate title");

        using var response = await Client.PostAsJsonAsync(
            "/api/v1/documents",
            ValidRequest("duplicate title"),
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Create_NumericEnum_ReturnsBadRequest()
    {
        var json = JsonSerializer.Serialize(TestData.ValidRequest("Numeric enum"), JsonOptions)
            .Replace("\"documentType\":\"contract\"", "\"documentType\":0", StringComparison.Ordinal);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await Client.PostAsync("/api/v1/documents", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Create_MalformedJson_ReturnsBadRequest()
    {
        using var content = new StringContent("{", Encoding.UTF8, "application/json");
        using var response = await Client.PostAsync("/api/v1/documents", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Create_MissingOwnerEmail_ReturnsValidationProblem()
    {
        using var response = await Client.PostAsJsonAsync(
            "/api/v1/documents",
            ValidRequest("No owner", ownerEmail: null),
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var problem = await ReadJsonAsync(response);
        Assert.That(problem.GetProperty("errors").TryGetProperty("ownerEmail", out _), Is.True);
    }

    [Test]
    public async Task GetById_ReturnsDocument()
    {
        var created = await CreateAsync("Get by id");

        using var response = await Client.GetAsync($"/api/v1/documents/{created.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var document = await response.Content.ReadFromJsonAsync<Document>(JsonOptions);
        Assert.That(document, Is.Not.Null);
        Assert.That(document!.Id, Is.EqualTo(created.Id));
        Assert.That(document.Title, Is.EqualTo("Get by id"));
    }

    [Test]
    public async Task GetById_Unknown_ReturnsNotFound()
    {
        using var response = await Client.GetAsync($"/api/v1/documents/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Update_ChangesFieldsAndUpdatedAt()
    {
        var created = await CreateAsync("Before update");

        var update = ValidRequest("After update") with { Priority = 55 };
        using var response = await Client.PutAsJsonAsync(
            $"/api/v1/documents/{created.Id}",
            update,
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updated = await response.Content.ReadFromJsonAsync<Document>(JsonOptions);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Title, Is.EqualTo("After update"));
        Assert.That(updated.Priority, Is.EqualTo(55));
        Assert.That(
            updated.CreatedAt.ToUnixTimeMilliseconds(),
            Is.EqualTo(created.CreatedAt.ToUnixTimeMilliseconds()));
        Assert.That(updated.UpdatedAt, Is.GreaterThanOrEqualTo(created.UpdatedAt));
    }

    [Test]
    public async Task Update_Unknown_ReturnsNotFound()
    {
        using var response = await Client.PutAsJsonAsync(
            $"/api/v1/documents/{Guid.NewGuid()}",
            ValidRequest("Unknown"),
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Update_DuplicateTitle_ReturnsConflict()
    {
        await CreateAsync("First title");
        var second = await CreateAsync("Second title");

        using var response = await Client.PutAsJsonAsync(
            $"/api/v1/documents/{second.Id}",
            ValidRequest("First title"),
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Delete_RemovesDocument()
    {
        var created = await CreateAsync("To delete");

        using var deleteResponse = await Client.DeleteAsync($"/api/v1/documents/{created.Id}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        using var getResponse = await Client.GetAsync($"/api/v1/documents/{created.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        using var secondDeleteResponse = await Client.DeleteAsync($"/api/v1/documents/{created.Id}");
        Assert.That(secondDeleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
