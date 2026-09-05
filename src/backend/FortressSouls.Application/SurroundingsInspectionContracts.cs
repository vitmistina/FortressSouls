namespace FortressSouls.Application;

using FortressSouls.Domain;

public interface ISurroundingsInspectionService
{
    Task<CurrentSceneObservation> ObserveCurrentSceneAsync(
        DwarfId observerDwarfId,
        CancellationToken cancellationToken);

    Task<LookAroundToolResult> InspectAroundAsync(
        DwarfId observerDwarfId,
        int requestedRadius,
        CancellationToken cancellationToken);
}
