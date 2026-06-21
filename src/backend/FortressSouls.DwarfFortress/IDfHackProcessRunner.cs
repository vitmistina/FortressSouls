namespace FortressSouls.DwarfFortress;

public interface IDfHackProcessRunner
{
    Task<DfHackProcessCommandResult> RunCommandAsync(
        DfHackCommand command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}

public enum DfHackProcessFailureCategory
{
    Unavailable,
    ExecutableUnavailable,
    Timeout,
    Cancelled,
    Crashed,
    Failed,
    OutputTooLarge
}

public sealed record DfHackProcessCommandResult(
    bool IsSuccess,
    string? Stdout,
    string? Stderr,
    int? ExitCode,
    TimeSpan Duration,
    DfHackCommand Command,
    DfHackProcessFailureCategory? FailureCategory)
{
    public static DfHackProcessCommandResult Success(
        DfHackCommand command,
        string stdout,
        string stderr,
        int exitCode,
        TimeSpan duration) =>
        new(
            IsSuccess: true,
            Stdout: stdout,
            Stderr: stderr,
            ExitCode: exitCode,
            Duration: duration,
            Command: command,
            FailureCategory: null);

    public static DfHackProcessCommandResult Failure(
        DfHackCommand command,
        DfHackProcessFailureCategory category,
        string? stdout,
        string? stderr,
        int? exitCode,
        TimeSpan duration) =>
        new(
            IsSuccess: false,
            Stdout: stdout,
            Stderr: stderr,
            ExitCode: exitCode,
            Duration: duration,
            Command: command,
            FailureCategory: category);
}
