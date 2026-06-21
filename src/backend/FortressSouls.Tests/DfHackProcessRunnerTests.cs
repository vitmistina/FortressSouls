namespace FortressSouls.Tests;

using FortressSouls.DwarfFortress;

[Collection("DfHackProcessSerial")]
public sealed class DfHackProcessRunnerTests
{
    [Fact]
    public async Task DfHackProcessRunner_ReturnsUnavailable_WhenPreflightFails()
    {
        var runner = CreateRunner(
            mode: "success",
            preflight: new StubPreflight(isReachable: false));

        var result = await runner.RunCommandAsync(DfHackCommand.ListDwarves, [], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DfHackProcessFailureCategory.Unavailable, result.FailureCategory);
    }

    [Fact]
    public async Task DfHackProcessRunner_ReturnsExecutableUnavailable_WhenExecutableMissing()
    {
        var options = new DfHackProcessAdapterOptions
        {
            Enabled = true,
            RunPath = Path.Combine(GetRepoRoot(), "missing", "dfhack-run.exe"),
            WorkingDirectory = GetRepoRoot()
        }.Validate();
        var runner = new DfHackProcessRunner(options, new StubPreflight(isReachable: true));

        var result = await runner.RunCommandAsync(DfHackCommand.ListDwarves, [], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DfHackProcessFailureCategory.ExecutableUnavailable, result.FailureCategory);
    }

    [Fact]
    public async Task DfHackProcessRunner_ReturnsTimeout_WhenProcessExceedsLimit()
    {
        var runner = CreateRunner(
            mode: "hang",
            configure: options => options.TimeoutMs = 300);

        var result = await runner.RunCommandAsync(DfHackCommand.ListDwarves, [], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DfHackProcessFailureCategory.Timeout, result.FailureCategory);
    }

    [Fact]
    public async Task DfHackProcessRunner_ReturnsCancelled_WhenRequestIsCancelled()
    {
        var runner = CreateRunner(mode: "hang", configure: options => options.TimeoutMs = 10_000);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var result = await runner.RunCommandAsync(DfHackCommand.ListDwarves, [], cancellationTokenSource.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(DfHackProcessFailureCategory.Cancelled, result.FailureCategory);
    }

    [Fact]
    public async Task DfHackProcessRunner_ClassifiesCrashAndFailureAndOutputLimit()
    {
        var crashed = await CreateRunner(mode: "crashed").RunCommandAsync(DfHackCommand.ListDwarves, [], CancellationToken.None);
        Assert.Equal(DfHackProcessFailureCategory.Crashed, crashed.FailureCategory);

        var failed = await CreateRunner(mode: "failed").RunCommandAsync(DfHackCommand.ListDwarves, [], CancellationToken.None);
        Assert.Equal(DfHackProcessFailureCategory.Failed, failed.FailureCategory);

        var oversized = await CreateRunner(
            mode: "oversize",
            configure: options => options.MaxStdoutBytes = 2048).RunCommandAsync(DfHackCommand.ListDwarves, [], CancellationToken.None);
        Assert.Equal(DfHackProcessFailureCategory.OutputTooLarge, oversized.FailureCategory);
    }

    [Fact]
    public async Task DfHackProcessRunner_UsesClosedAllowlistCommandNames()
    {
        var runner = CreateRunner(mode: "success");

        var list = await runner.RunCommandAsync(DfHackCommand.ListDwarves, [], CancellationToken.None);
        var snapshot = await runner.RunCommandAsync(DfHackCommand.GetDwarfSnapshot, ["6597"], CancellationToken.None);
        var diagnose = await runner.RunCommandAsync(DfHackCommand.Diagnose, [], CancellationToken.None);

        Assert.True(list.IsSuccess);
        Assert.True(snapshot.IsSuccess);
        Assert.True(diagnose.IsSuccess);
    }

    private static DfHackProcessRunner CreateRunner(
        string mode,
        StubPreflight? preflight = null,
        Action<DfHackProcessAdapterOptions>? configure = null)
    {
        var options = new DfHackProcessAdapterOptions
        {
            Enabled = true,
            RunPath = GetHostExecutablePath(),
            WorkingDirectory = Path.GetDirectoryName(GetHostExecutablePath()) ?? GetRepoRoot(),
            TimeoutMs = 2000,
            MaxStdoutBytes = 32 * 1024,
            MaxStderrBytes = 8 * 1024
        };
        configure?.Invoke(options);
        options.Validate();

        Environment.SetEnvironmentVariable("FORTRESS_SOULS_DFHACK_TEST_MODE", mode);
        return new DfHackProcessRunner(options, preflight ?? new StubPreflight(isReachable: true));
    }

    private static string GetHostExecutablePath()
    {
        var assemblyLocation = typeof(FortressSouls.DfHackProcessTestHost.Marker).Assembly.Location;
        var exePath = Path.ChangeExtension(assemblyLocation, ".exe");
        if (exePath is null || !File.Exists(exePath))
        {
            throw new InvalidOperationException("Unable to locate DFHack process test host executable.");
        }

        return exePath;
    }

    private static string GetRepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
    {
        var testDirectory = Path.GetDirectoryName(sourceFilePath)
            ?? throw new InvalidOperationException("Unable to determine the test source directory.");
        return Path.GetFullPath(Path.Combine(testDirectory, "..", "..", ".."));
    }

    private sealed class StubPreflight(bool isReachable) : IDfHackTcpPreflight
    {
        public Task<bool> IsReachableAsync(CancellationToken cancellationToken) => Task.FromResult(isReachable);
    }
}
