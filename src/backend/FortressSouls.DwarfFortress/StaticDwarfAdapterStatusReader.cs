namespace FortressSouls.DwarfFortress;

using FortressSouls.Application;

public sealed class StaticDwarfAdapterStatusReader(string adapterType) : IDwarfAdapterStatusReader
{
    private readonly DwarfAdapterStatus _status = new(
        AdapterType: adapterType,
        IsConfigured: true,
        IsReady: true,
        LastOutcome: "not_started",
        LastErrorCategory: null,
        LastDurationMs: null,
        LastUpdatedAtUtc: null);

    public DwarfAdapterStatus GetCurrentStatus() => _status;
}
