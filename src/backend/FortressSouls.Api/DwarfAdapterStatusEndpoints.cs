namespace FortressSouls.Api;

using FortressSouls.Application;

internal static class DwarfAdapterStatusEndpoints
{
    public static IEndpointRouteBuilder MapDwarfAdapterStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/dwarves/adapter-status", GetStatus)
            .WithName("GetDwarfAdapterStatus")
            .Produces<DwarfAdapterStatusResponse>();

        return endpoints;
    }

    private static DwarfAdapterStatusResponse GetStatus(IDwarfAdapterStatusReader statusReader)
    {
        var status = statusReader.GetCurrentStatus();
        return new DwarfAdapterStatusResponse(
            status.AdapterType,
            status.IsConfigured,
            status.IsReady,
            status.LastOutcome,
            status.LastErrorCategory,
            status.LastDurationMs,
            status.LastUpdatedAtUtc?.ToString("O"));
    }
}
