using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ProductCatalogue.API.Infrastructure;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception,
            "Unhandled exception on {Method} {Path}",
            ctx.Request.Method, ctx.Request.Path);

        var (statusCode, title) = exception switch
        {
            ValidationException =>
                (StatusCodes.Status422UnprocessableEntity,
                 "One or more validation errors occurred."),
            _ =>
                (StatusCodes.Status500InternalServerError,
                 "An unexpected error occurred.")
        };

        ctx.Response.StatusCode = statusCode;

        var extensions = new Dictionary<string, object?>();

        if (exception is ValidationException ve)
        {
            extensions["errors"] = ve.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext    = ctx,
            Exception      = exception,
            ProblemDetails = new ProblemDetails
            {
                Title      = title,
                Status     = statusCode,
                Detail     = exception.Message,
                Extensions = extensions
            }
        });
    }
}
