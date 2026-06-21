namespace FortressSouls.Api;

using FortressSouls.Application;

internal static class ProviderStatusEndpoints
{
    public static IEndpointRouteBuilder MapProviderStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/provider/status", GetStatus)
            .WithName("GetProviderStatus")
            .Produces<ProviderStatusResponse>();

        return endpoints;
    }

    private static ProviderStatusResponse GetStatus(IChatProviderStatusReader statusReader)
    {
        var status = statusReader.GetCurrentStatus();
        return new ProviderStatusResponse(
            status.ProviderType,
            status.Model,
            status.IsConfigured,
            status.IsReady,
            status.LastOutcome,
            status.LastErrorCategory,
            status.LastDurationMs,
            status.LastUpdatedAtUtc?.ToString("O"));
    }
}
