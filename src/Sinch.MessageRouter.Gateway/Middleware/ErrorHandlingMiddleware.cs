using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Sinch.MessageRouter.Gateway.Middleware;

public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request");
            await WriteProblem(context, HttpStatusCode.BadRequest, "Bad Request", ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            await WriteProblem(context, HttpStatusCode.NotFound, "Not Found", ex.Message);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteProblem(context, HttpStatusCode.InternalServerError, "Internal Server Error",
                context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() ? ex.ToString() : "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblem(HttpContext ctx, HttpStatusCode status, string title, string detail)
    {
        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(ctx.Response.Body, new ProblemDetails { Status = (int)status, Title = title, Detail = detail, Instance = ctx.Request.Path });
    }
}
