namespace FortressSouls.DwarfFortress;

using System.Net;

public sealed class DfHackProcessAdapterOptions
{
    public const string ConfigurationSectionPath = "FortressSouls:DfHack";

    public bool Enabled { get; set; }

    public string RunPath { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5000;

    public int TimeoutMs { get; set; } = 3000;

    public int PreflightTimeoutMs { get; set; } = 500;

    public int MaxStdoutBytes { get; set; } = 512 * 1024;

    public int MaxStderrBytes { get; set; } = 64 * 1024;

    public int MaxJsonDepth { get; set; } = 64;

    public int MaxStringLength { get; set; } = 512;

    public int MaxListItems { get; set; } = 1024;

    public int MaxSkills { get; set; } = 256;

    public int MaxTraits { get; set; } = 64;

    public int MaxValues { get; set; } = 128;

    public int MaxNeeds { get; set; } = 256;

    public int MaxMannerisms { get; set; } = 64;

    public DfHackProcessAdapterOptions Validate()
    {
        if (Port is <= 0 or > 65535)
        {
            throw new ArgumentException("DFHack port must be between 1 and 65535.", nameof(Port));
        }

        if (TimeoutMs is < 200 or > 120_000)
        {
            throw new ArgumentException("DFHack timeout must be between 200 and 120000 milliseconds.", nameof(TimeoutMs));
        }

        if (PreflightTimeoutMs is < 100 or > 10_000)
        {
            throw new ArgumentException("DFHack preflight timeout must be between 100 and 10000 milliseconds.", nameof(PreflightTimeoutMs));
        }

        if (MaxStdoutBytes is < 1024 or > 2 * 1024 * 1024)
        {
            throw new ArgumentException("DFHack stdout limit must be between 1024 and 2097152 bytes.", nameof(MaxStdoutBytes));
        }

        if (MaxStderrBytes is < 256 or > 512 * 1024)
        {
            throw new ArgumentException("DFHack stderr limit must be between 256 and 524288 bytes.", nameof(MaxStderrBytes));
        }

        if (MaxJsonDepth is < 8 or > 256
            || MaxStringLength is < 32 or > 4096
            || MaxListItems is <= 0
            || MaxSkills is <= 0
            || MaxTraits is <= 0
            || MaxValues is <= 0
            || MaxNeeds is <= 0
            || MaxMannerisms is <= 0)
        {
            throw new ArgumentException("DFHack JSON mapping limits are invalid.");
        }

        if (!IsLoopbackHost(Host))
        {
            throw new ArgumentException("DFHack host must be loopback for v0.1.", nameof(Host));
        }

        if (!Enabled)
        {
            return this;
        }

        if (string.IsNullOrWhiteSpace(RunPath) || !Path.IsPathRooted(RunPath))
        {
            throw new ArgumentException("A rooted DFHack run path is required.", nameof(RunPath));
        }

        if (string.IsNullOrWhiteSpace(WorkingDirectory) || !Path.IsPathRooted(WorkingDirectory))
        {
            throw new ArgumentException("A rooted DFHack working directory is required.", nameof(WorkingDirectory));
        }

        return this;
    }

    public JsonFileDwarfFortressAdapterOptions ToJsonMapperOptions() =>
        new()
        {
            DwarfListPath = "unused",
            DwarfSnapshotPath = "unused",
            MaxJsonDepth = MaxJsonDepth,
            MaxStringLength = MaxStringLength,
            MaxListItems = MaxListItems,
            MaxSkills = MaxSkills,
            MaxTraits = MaxTraits,
            MaxValues = MaxValues,
            MaxNeeds = MaxNeeds,
            MaxMannerisms = MaxMannerisms
        };

    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }
}
