namespace FortressSouls.DwarfFortress;

public sealed class JsonFileDwarfFortressAdapterOptions
{
    public string DwarfListPath { get; set; } = string.Empty;

    public string DwarfSnapshotPath { get; set; } = string.Empty;

    public int MaxListFileBytes { get; set; } = 128 * 1024;

    public int MaxSnapshotFileBytes { get; set; } = 512 * 1024;

    public int MaxJsonDepth { get; set; } = 64;

    public int MaxStringLength { get; set; } = 512;

    public int MaxListItems { get; set; } = 1024;

    public int MaxSkills { get; set; } = 256;

    public int MaxTraits { get; set; } = 64;

    public int MaxValues { get; set; } = 128;

    public int MaxNeeds { get; set; } = 256;

    public int MaxMannerisms { get; set; } = 64;
}
