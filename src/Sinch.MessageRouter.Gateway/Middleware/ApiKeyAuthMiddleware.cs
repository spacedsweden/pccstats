namespace Sinch.MessageRouter.Gateway.Middleware;

public sealed class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;

    private static readonly HashSet<string> SkipPaths = new(StringComparer.OrdinalIgnoreCase) { "/health", "/metrics", "/" };

    public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (SkipPaths.Contains(context.Request.Path.Value ?? "") ||
            context.Request.Path.StartsWithSegments("/v1/webhooks/inbound") ||
            !_config.GetValue("Auth:Enabled", false))
        {
            await _next(context);
            return;
        }

        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (apiKey is null)
        {
            var auth = context.Request.Headers.Authorization.FirstOrDefault();
            if (auth?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
                apiKey = auth["Bearer ".Length..];
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "API key required. Use X-API-Key header or Authorization: Bearer {key}." });
            return;
        }

        var validKeys = _config.GetSection("Auth:ApiKeys").Get<string[]>();
        if (validKeys is not null && !validKeys.Contains(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key." });
            return;
        }

        await _next(context);
    }
}
