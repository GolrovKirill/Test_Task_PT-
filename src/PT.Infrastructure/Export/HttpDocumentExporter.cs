using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PT.Domain.Entities;

namespace PT.Infrastructure.Export;

public sealed class HttpDocumentExporter(
    HttpClient httpClient,
    ILogger<HttpDocumentExporter> logger) : IExternalDocumentExporter
{
    private const int MaxAttempts = 3;
    private const int FirstRetryDelayMs = 200;
    private const int SecondRetryDelayMs = 400;

    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromMilliseconds(FirstRetryDelayMs),
        TimeSpan.FromMilliseconds(SecondRetryDelayMs),
    ];

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    public async Task ExportBatchAsync(
        IReadOnlyList<Document> documents,
        CancellationToken cancellationToken)
    {
        var payload = new { items = documents };

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            HttpResponseMessage? response = null;

            try
            {
                response = await httpClient.PostAsJsonAsync(
                    "api/v1/import",
                    payload,
                    JsonOptions,
                    cancellationToken);
            }
            catch (HttpRequestException exception) when (attempt < MaxAttempts)
            {
                logger.LogWarning(exception, "MockSiem import network error; attempt {Attempt}", attempt);
                await DelayAsync(attempt, cancellationToken);
                continue;
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested && attempt < MaxAttempts)
            {
                logger.LogWarning(exception, "MockSiem import timeout; attempt {Attempt}", attempt);
                await DelayAsync(attempt, cancellationToken);
                continue;
            }

            if (response is null)
            {
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Exported {Count} documents to MockSiem", documents.Count);
                    return;
                }

                if (!IsRetryable(response.StatusCode))
                {
                    throw new HttpRequestException(
                        $"MockSiem import failed with {(int)response.StatusCode} {response.StatusCode}.",
                        inner: null,
                        statusCode: response.StatusCode);
                }

                if (attempt == MaxAttempts)
                {
                    throw new HttpRequestException(
                        $"MockSiem import failed after {MaxAttempts} attempts (last {(int)response.StatusCode}).",
                        inner: null,
                        statusCode: response.StatusCode);
                }

                logger.LogWarning(
                    "MockSiem import returned {StatusCode}; attempt {Attempt}",
                    response.StatusCode,
                    attempt);
            }

            await DelayAsync(attempt, cancellationToken);
        }
    }

    private static async Task DelayAsync(int attempt, CancellationToken cancellationToken)
    {
        var delay = Delays[Math.Min(attempt - 1, Delays.Length - 1)];
        await Task.Delay(delay, cancellationToken);
    }

    private static bool IsRetryable(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout
        || statusCode == HttpStatusCode.TooManyRequests
        || (int)statusCode >= (int)HttpStatusCode.InternalServerError;

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
