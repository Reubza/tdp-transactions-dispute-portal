using Serilog.Context;

namespace Api.Common;

/// <summary>
/// Propagates a correlation id (from <c>X-Correlation-ID</c> or generated) into logs and the response,
/// and applies baseline security headers.
/// </summary>
internal sealed class RequestContextMiddleware(RequestDelegate next)
{
    public const string CorrelationHeader = "X-Correlation-ID";

    private const string ContentSecurityPolicy =
        "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; font-src 'self' data:; " +
        "script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationHeader].FirstOrDefault() is { Length: > 0 and <= 64 } supplied
            ? supplied
            : context.TraceIdentifier;
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers[CorrelationHeader] = correlationId;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            // The interactive API reference page loads its assets from a CDN, so it gets no CSP.
            if (!context.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase))
            {
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
            }

            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
