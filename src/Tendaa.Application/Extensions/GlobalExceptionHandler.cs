using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Tendaa.Application.Common;

namespace Tendaa.Application.Extensions;

public sealed class GlobalExceptionHandler(IWebHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var message = environment.IsDevelopment() ? exception.Message : "An unexpected error occurred.";

        var result = exception switch
        {
            UnauthorizedAccessException => TendaaResult.Unauthorized(message),
            KeyNotFoundException => TendaaResult.NotFound(message),
            ArgumentException => TendaaResult.BadRequest(message),
            _ => TendaaResult.InternalServerError(message),
        };

        httpContext.Response.StatusCode = result.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(result, cancellationToken);

        return true;
    }
}
