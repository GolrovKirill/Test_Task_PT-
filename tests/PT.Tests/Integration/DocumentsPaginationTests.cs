using System.Net;
using NUnit.Framework;

namespace PT.Tests.Integration;

[TestFixture]
public sealed class DocumentsPaginationTests : DocumentsTestBase
{
    [Test]
    public async Task List_ReturnsRequestedPage()
    {
        for (var index = 1; index <= 5; index++)
        {
            await CreateAsync($"Paged {index}");
        }

        var page = await GetPageAsync("?page=2&pageSize=2");

        Assert.That(page.Page, Is.EqualTo(2));
        Assert.That(page.PageSize, Is.EqualTo(2));
        Assert.That(page.TotalItems, Is.EqualTo(5));
        Assert.That(page.TotalPages, Is.EqualTo(3));
        Assert.That(page.Items, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task List_DefaultPageSizeIsTwenty()
    {
        for (var index = 1; index <= 3; index++)
        {
            await CreateAsync($"Default page {index}");
        }

        var page = await GetPageAsync(string.Empty);

        Assert.That(page.Page, Is.EqualTo(1));
        Assert.That(page.PageSize, Is.EqualTo(20));
        Assert.That(page.TotalItems, Is.EqualTo(3));
        Assert.That(page.TotalPages, Is.EqualTo(1));
    }

    [Test]
    public async Task List_InvalidPage_ReturnsValidationProblem()
    {
        using var response = await Client.GetAsync("/api/v1/documents?page=0");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var problem = await ReadJsonAsync(response);
        Assert.That(problem.GetProperty("errors").TryGetProperty("page", out _), Is.True);
    }

    [Test]
    public async Task List_InvalidPageSize_ReturnsValidationProblem()
    {
        using var response = await Client.GetAsync("/api/v1/documents?pageSize=101");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var problem = await ReadJsonAsync(response);
        Assert.That(problem.GetProperty("errors").TryGetProperty("pageSize", out _), Is.True);
    }

    [Test]
    public async Task List_PageBeyondTotal_ReturnsEmptyPage()
    {
        await CreateAsync("Only one");

        var page = await GetPageAsync("?page=10&pageSize=10");

        Assert.That(page.Items, Is.Empty);
        Assert.That(page.Page, Is.EqualTo(10));
        Assert.That(page.TotalItems, Is.EqualTo(1));
    }

    [Test]
    public async Task List_PageSizeAtMaxBoundary_ReturnsOk()
    {
        var page = await GetPageAsync("?pageSize=100");

        Assert.That(page.PageSize, Is.EqualTo(100));
    }

    [Test]
    public async Task List_NonNumericPage_ReturnsBadRequest()
    {
        using var response = await Client.GetAsync("/api/v1/documents?page=abc");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
