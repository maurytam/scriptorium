using Microsoft.AspNetCore.Diagnostics;
using Scriptorium.Core.Dtos;

namespace Scriptorium.API.ErrorHandling;

/// <summary>Turns any unhandled exception into the API's JSON error shape instead of a crash or a stack trace.</summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true; // The client went away; there is nobody to answer.
        }

        if (exception is BadHttpRequestException badRequest)
        {
            // Client mistakes detected by the framework (e.g. an unparsable form value) keep their status.
            _logger.LogInformation(exception, "Rejected request {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            await WriteAsync(httpContext, badRequest.StatusCode, "The request was not valid.", cancellationToken);
            return true;
        }

        _logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        await WriteAsync(httpContext, StatusCodes.Status500InternalServerError, "An unexpected error occurred.", cancellationToken);
        return true;
    }

    private static async Task WriteAsync(HttpContext httpContext, int statusCode, string message, CancellationToken ct)
    {
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ErrorDto(message), ct);
    }
}
