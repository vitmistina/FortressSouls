namespace FortressSouls.DwarfFortress;

public enum DfHackCommand
{
    Diagnose,
    ListDwarves,
    GetDwarfSnapshot
}

internal static class DfHackCommandExtensions
{
    public static string ToCommandName(this DfHackCommand command) =>
        command switch
        {
            DfHackCommand.Diagnose => "fortress-souls/diagnose",
            DfHackCommand.ListDwarves => "fortress-souls/list-dwarves",
            DfHackCommand.GetDwarfSnapshot => "fortress-souls/get-dwarf-snapshot",
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported DFHack command.")
        };

    public static bool RequiresPreflight(this DfHackCommand command) =>
        command is DfHackCommand.ListDwarves or DfHackCommand.GetDwarfSnapshot;
}
