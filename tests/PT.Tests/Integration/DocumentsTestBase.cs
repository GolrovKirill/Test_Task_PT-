using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NUnit.Framework;
using PT.Api.Features.Documents;
using PT.Domain.Entities;

namespace PT.Tests.Integration;

public abstract class DocumentsTestBase
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    protected HttpClient Client { get; private set; } = null!;

    protected static DocumentWriteRequest ValidRequest(
        string title = "Test document",
        string? ownerEmail = "owner@example.com") =>
        TestData.ValidRequest(title, ownerEmail);

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [SetUp]
    public async Task SetUp()
    {
        await TestHost.ResetDatabaseAsync();
        Client = TestHost.Factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        Client.Dispose();
    }

    protected async Task<Document> CreateAsync(
        string title = "Test document",
        string? ownerEmail = "owner@example.com")
    {
        using var response = await Client.PostAsJsonAsync(
            "/api/v1/documents",
            ValidRequest(title, ownerEmail),
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var document = await response.Content.ReadFromJsonAsync<Document>(JsonOptions);
        Assert.That(document, Is.Not.Null);
        return document!;
    }

    protected async Task<PagedResult<Document>> GetPageAsync(string query)
    {
        using var response = await Client.GetAsync($"/api/v1/documents{query}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var page = await response.Content.ReadFromJsonAsync<PagedResult<Document>>(JsonOptions);
        Assert.That(page, Is.Not.Null);
        return page!;
    }
}
