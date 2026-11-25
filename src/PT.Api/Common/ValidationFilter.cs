using FluentValidation;

namespace PT.Api.Common;

/// <summary>
/// Запускает FluentValidation для аргумента типа <typeparamref name="T"/>
/// и возвращает 400 с ошибками по полям до входа в обработчик.
/// </summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().FirstOrDefault();
        if (request is null)
        {
            return await next(context);
        }

        var validationResult = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (validationResult.IsValid)
        {
            return await next(context);
        }

        var errors = validationResult.Errors
            .GroupBy(failure => ToCamelCase(failure.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray());

        return Results.ValidationProblem(
            errors,
            title: "Ошибка валидации",
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = context.HttpContext.TraceIdentifier,
            });
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "request";
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
