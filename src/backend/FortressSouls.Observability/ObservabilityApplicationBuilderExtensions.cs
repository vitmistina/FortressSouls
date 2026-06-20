namespace FortressSouls.Observability;

using Microsoft.AspNetCore.Builder;

public static class ObservabilityApplicationBuilderExtensions
{
    public static IApplicationBuilder UseFortressSoulsCorrelationId(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
