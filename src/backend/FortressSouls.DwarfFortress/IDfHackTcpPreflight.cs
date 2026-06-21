namespace FortressSouls.DwarfFortress;

public interface IDfHackTcpPreflight
{
    Task<bool> IsReachableAsync(CancellationToken cancellationToken);
}
