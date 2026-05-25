namespace PaymentSystem.Web.Middleware;

/// <summary>
/// Applies a hardened set of HTTP security response headers to every outbound response.
/// This middleware should be registered early in the pipeline, before CORS and authentication,
/// so headers are present on all responses including error and redirect responses.
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

        // Prevent MIME-type sniffing — browsers must honour declared Content-Type.
        headers["X-Content-Type-Options"] = "nosniff";

        // Block this application from being embedded in any frame or iframe.
        headers["X-Frame-Options"] = "DENY";

        // Enable the browser's built-in XSS filter and block the page on detection.
        headers["X-XSS-Protection"] = "1; mode=block";

        // Only send the origin when navigating to a secure context from a secure context.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Restrict access to sensitive browser APIs that this application does not use.
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        // Prevent sensitive API responses from being cached by intermediate proxies.
        headers["Cache-Control"] = "no-store";

        await _next(context);
    }
}
