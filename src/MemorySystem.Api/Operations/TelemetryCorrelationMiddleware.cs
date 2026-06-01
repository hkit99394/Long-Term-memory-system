using System.Diagnostics;
using MemorySystem.Infrastructure.Observability;

namespace MemorySystem.Api.Operations;

public sealed class TelemetryCorrelationMiddleware(
    RequestDelegate next,
    ILogger<TelemetryCorrelationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ReadOrCreateCorrelationId(context);
        context.Items[MemorySystemTelemetry.CorrelationItemName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[MemorySystemTelemetry.CorrelationHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        Activity.Current?.SetTag(MemorySystemTelemetry.CorrelationIdAttribute, correlationId);

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = Activity.Current?.TraceId.ToString()
        });

        await next(context);
    }

    private static string ReadOrCreateCorrelationId(HttpContext context)
    {
        var requestedCorrelationId = context.Request.Headers[MemorySystemTelemetry.CorrelationHeaderName].ToString();

        return IsSafeCorrelationId(requestedCorrelationId)
            ? requestedCorrelationId
            : CreateCorrelationId(context);
    }

    private static string CreateCorrelationId(HttpContext context)
    {
        return Activity.Current?.TraceId.ToString() is { Length: > 0 } traceId
            ? traceId
            : context.TraceIdentifier;
    }

    private static bool IsSafeCorrelationId(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= 128
            && value.All(IsSafeCorrelationIdCharacter);
    }

    private static bool IsSafeCorrelationIdCharacter(char value)
    {
        return char.IsAsciiLetterOrDigit(value)
            || value is '-' or '_' or '.' or ':' or '/';
    }
}
