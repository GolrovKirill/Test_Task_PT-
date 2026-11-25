using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PT.Api.Common;

/// <summary>
/// Maps exceptions to a single problem-details shape: 400 for malformed JSON/values,
/// 409 for unique violations, 499 for client aborts and 500 (without stack traces) otherwise.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // TaskCanceledException derives from OperationCanceledException, so a dedicated branch is not needed.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(exception, "Request aborted by client");

            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = ClientClosedRequest;
            }

            return true;
        }

        var (statusCode, title, detail) = exception switch
        {
            BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "Некорректный запрос",
                "Не удалось прочитать тело запроса: проверьте JSON и значения полей."),
            _ when IsPostgresError(exception, PostgresErrorCodes.UniqueViolation) => (
                StatusCodes.Status409Conflict,
                "Конфликт",
                "Документ с таким title уже существует."),
            _ when IsPostgresError(exception, PostgresErrorCodes.NumericValueOutOfRange) => (
                StatusCodes.Status400BadRequest,
                "Некорректное значение",
                "Значение не помещается в колонку. Проверьте QualityScore."),
            _ when IsPostgresError(exception, PostgresErrorCodes.CheckViolation) => (
                StatusCodes.Status400BadRequest,
                "Некорректное значение",
                "Нарушено ограничение базы данных."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Внутренняя ошибка",
                "Внутренняя ошибка сервера."),
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation(
                exception,
                "Request failed with {StatusCode} for {Method} {Path}",
                statusCode,
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = httpContext.TraceIdentifier,
            },
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }

    private static bool IsPostgresError(Exception exception, string sqlState) =>
        exception switch
        {
            PostgresException postgresException => postgresException.SqlState == sqlState,
            DbUpdateException { InnerException: PostgresException postgresException } =>
                postgresException.SqlState == sqlState,
            _ => false,
        };
}
