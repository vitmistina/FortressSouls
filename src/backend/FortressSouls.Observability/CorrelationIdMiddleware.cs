namespace FortressSouls.Observability;

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const int MaxCorrelationIdLength = 64;

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers[FortressSoulsTelemetry.CorrelationHeaderName]);

        using var activity = FortressSoulsTelemetry.ActivitySource.StartActivity(
            "request",
            ActivityKind.Server);

        activity?.SetTag(FortressSoulsTelemetry.CorrelationIdTagName, correlationId);

        context.Response.Headers[FortressSoulsTelemetry.CorrelationHeaderName] = correlationId;
        context.Items[FortressSoulsTelemetry.CorrelationIdFieldName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            [FortressSoulsTelemetry.CorrelationIdFieldName] = correlationId
        });

        Activity.Current?.SetTag(FortressSoulsTelemetry.CorrelationIdTagName, correlationId);

        await next(context);
    }

    internal static string ResolveCorrelationId(StringValues incomingValues)
    {
        if (incomingValues.Count == 1)
        {
            var value = incomingValues[0]?.Trim() ?? string.Empty;
            if (IsValidCorrelationId(value))
            {
                return value;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    internal static bool IsValidCorrelationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxCorrelationIdLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
