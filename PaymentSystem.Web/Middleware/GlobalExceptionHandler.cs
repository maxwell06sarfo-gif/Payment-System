using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PaymentSystem.Web.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        System.Exception exception,
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
            Message = "A secure processing exception occurred on the core billing engine.",
            Detailed = httpContext.RequestServices.GetService(typeof(Microsoft.AspNetCore.Hosting.IWebHostEnvironment)) is Microsoft.AspNetCore.Hosting.IWebHostEnvironment env && env.EnvironmentName == "Development"
                ? exception.Message
                : "Contact system administration for transaction correlation."
        };

        await httpContext.Response.WriteAsJsonAsync(errorPayload, cancellationToken);
        return true;
    }
}
