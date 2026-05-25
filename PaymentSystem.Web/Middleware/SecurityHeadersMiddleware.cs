namespace PaymentSystem.Web.Middleware;

/// <summary>
/// Adds a hardened set of HTTP security headers to every outbound response.
///
/// Registered early in the pipeline so headers are present on all responses —
/// including errors, redirects, and OPTIONS preflight — not just successful ones.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent MIME-type sniffing — browsers must honour the declared Content-Type.
        headers["X-Content-Type-Options"] = "nosniff";

        // Disallow this application from being framed by any other origin.
        headers["X-Frame-Options"] = "DENY";

        // Instruct browsers that support the legacy XSS auditor to block the page on detection.
        headers["X-XSS-Protection"] = "1; mode=block";

        // Send the full origin only when navigating between two secure contexts.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Restrict browser feature access to what this application actually uses.
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        // API responses must not be served from a cache. Stale auth or subscription
        // data in a shared proxy cache would be a genuine security issue.
        headers["Cache-Control"] = "no-store";

        await _next(context);
    }
}
