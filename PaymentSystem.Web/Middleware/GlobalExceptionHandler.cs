using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PaymentSystem.Web.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is ValidationException validationException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            httpContext.Response.ContentType = "application/json";

            var validationPayload = new
            {
                StatusCode = httpContext.Response.StatusCode,
                Message = "Validation failed.",
                Errors = validationException.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.ErrorMessage).ToArray())
            };

            await httpContext.Response.WriteAsJsonAsync(validationPayload, cancellationToken);
            return true;
        }

        if (exception is KeyNotFoundException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
            httpContext.Response.ContentType = "application/json";

            await httpContext.Response.WriteAsJsonAsync(new
            {
                StatusCode = httpContext.Response.StatusCode,
                Message = exception.Message
            }, cancellationToken);

            return true;
        }

        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        httpContext.Response.ContentType = "application/json";

        var errorPayload = new
        {
            StatusCode = httpContext.Response.StatusCode,
            Message = "An unexpected error occurred. Please try again or contact support.",
            Detail = _environment.IsDevelopment() ? exception.Message : null
        };

        await httpContext.Response.WriteAsJsonAsync(errorPayload, cancellationToken);
        return true;
    }
}
