namespace JobApp.Api;

using System.Net;
using Anthropic.Exceptions;

/// <summary>
/// Converts unhandled exceptions into JSON instead of the default HTML error
/// page. Claude auth/rate-limit failures get a message that actually tells you
/// what to do, since "Unauthorized" alone doesn't say where the key goes.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var (status, message) = Classify(ex);

            if (status == HttpStatusCode.InternalServerError)
            {
                log.LogError(ex, "Unhandled exception on {Path}", context.Request.Path);
            }
            else
            {
                log.LogWarning(ex, "{Status} on {Path}", status, context.Request.Path);
            }

            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message }, context.RequestAborted);
        }
    }

    private static (HttpStatusCode Status, string Message) Classify(Exception ex) => ex switch
    {
        AnthropicUnauthorizedException => (HttpStatusCode.BadGateway,
            "Claude API key is missing or invalid. Set ANTHROPIC_API_KEY, or run "
            + "'dotnet user-secrets set Claude:ApiKey <key>' in JobApp.Api, then restart the app."),

        AnthropicRateLimitException => (HttpStatusCode.TooManyRequests,
            "Claude API rate limit hit. Try again shortly."),

        Anthropic5xxException => (HttpStatusCode.BadGateway,
            "Claude API is temporarily unavailable. Try again shortly."),

        KeyNotFoundException => (HttpStatusCode.NotFound, ex.Message),

        InvalidOperationException or NotSupportedException or ArgumentException
            => (HttpStatusCode.UnprocessableEntity, ex.Message),

        _ => (HttpStatusCode.InternalServerError,
            "Something went wrong on the server. Check the API logs for details."),
    };
}

public static class ExceptionHandlingExtensions
{
    public static IApplicationBuilder UseJobAppExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
