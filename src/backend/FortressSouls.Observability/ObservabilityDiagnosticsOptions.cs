namespace FortressSouls.Observability;

public sealed class ObservabilityDiagnosticsOptions
{
    public const string ConfigurationSectionPath = "FortressSouls:Observability";

    public bool DiagnosticsEnabled { get; set; }
}
