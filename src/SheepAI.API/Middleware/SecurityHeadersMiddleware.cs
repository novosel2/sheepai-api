namespace SheepAI.API.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"]  = "nosniff";
            headers["X-Frame-Options"]         = "DENY";
            headers["X-XSS-Protection"]        = "1; mode=block";
            headers["Referrer-Policy"]         = "strict-origin-when-cross-origin";
            headers["Content-Security-Policy"] = "default-src 'self'";
            return Task.CompletedTask;
        });

        await next(context);
    }
}
