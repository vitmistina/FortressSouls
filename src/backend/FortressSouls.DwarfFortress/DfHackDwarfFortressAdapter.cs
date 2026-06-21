namespace FortressSouls.DwarfFortress;

using System.Diagnostics;
using System.Text.Json;
using FortressSouls.Application;
using FortressSouls.Domain;
using FortressSouls.Observability;

public sealed class DfHackDwarfFortressAdapter(
    IDfHackProcessRunner processRunner,
    DfHackProcessAdapterOptions options,
    IDfHackAdapterStatusRecorder statusRecorder) : IDwarfFortressAdapter
{
    private readonly IDfHackProcessRunner _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    private readonly DfHackProcessAdapterOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IDfHackAdapterStatusRecorder _statusRecorder = statusRecorder ?? throw new ArgumentNullException(nameof(statusRecorder));
    private readonly JsonFileDwarfFortressAdapter _jsonMapper = new(options.ToJsonMapperOptions());

    public async Task<DwarfListResult> ListDwarvesAsync(CancellationToken cancellationToken)
    {
        Activity.Current?.SetTag(FortressSoulsTelemetry.DfHackCommandTagName, DfHackCommand.ListDwarves.ToString());
        var result = await _processRunner.RunCommandAsync(DfHackCommand.ListDwarves, [], cancellationToken);
        return await MapResultAsync(
            DfHackCommand.ListDwarves,
            result,
            root => _jsonMapper.MapList(root),
            cancellationToken);
    }

    public async Task<DwarfSnapshot> GetDwarfSnapshotAsync(DwarfId dwarfId, CancellationToken cancellationToken)
    {
        Activity.Current?.SetTag(FortressSoulsTelemetry.DfHackCommandTagName, DfHackCommand.GetDwarfSnapshot.ToString());
        var result = await _processRunner.RunCommandAsync(
            DfHackCommand.GetDwarfSnapshot,
            [dwarfId.ToString()],
            cancellationToken);
        return await MapResultAsync(
            DfHackCommand.GetDwarfSnapshot,
            result,
            root => _jsonMapper.MapSnapshot(root, dwarfId),
            cancellationToken);
    }

    private async Task<T> MapResultAsync<T>(
        DfHackCommand command,
        DfHackProcessCommandResult result,
        Func<JsonElement, T> map,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!result.IsSuccess)
        {
            var category = result.FailureCategory ?? DfHackProcessFailureCategory.Failed;
            var errorCode = MapFailureCode(category);
            var errorCategory = MapFailureCategory(category);
            _statusRecorder.RecordFailure(
                outcome: category == DfHackProcessFailureCategory.Timeout ? "timeout" : "error",
                errorCategory: errorCategory,
                duration: result.Duration);
            TagFailure(command, errorCategory);

            if (category == DfHackProcessFailureCategory.Cancelled)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            throw new DwarfFortressDataException(
                errorCode,
                BuildSafeMessage(category));
        }

        try
        {
            using var document = JsonDocument.Parse(
                result.Stdout ?? string.Empty,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = _options.MaxJsonDepth
                });

            var mapped = map(document.RootElement);
            _statusRecorder.RecordSuccess(result.Duration);
            TagSuccess(command);
            return mapped;
        }
        catch (DwarfFortressDataException exception) when (exception.ErrorCode is DwarfFortressDataErrorCode.UnsupportedSchema)
        {
            _statusRecorder.RecordFailure("error", "invalid_schema", result.Duration);
            TagFailure(command, "invalid_schema");
            throw;
        }
        catch (DwarfFortressDataException)
        {
            _statusRecorder.RecordFailure("error", "mapping_failure", result.Duration);
            TagFailure(command, "mapping_failure");
            throw;
        }
        catch (JsonException exception)
        {
            _statusRecorder.RecordFailure("error", "invalid_json", result.Duration);
            TagFailure(command, "invalid_json");
            throw new DwarfFortressDataException(
                DwarfFortressDataErrorCode.MalformedJson,
                "DFHack returned invalid JSON output.",
                exception);
        }
    }

    private static void TagSuccess(DfHackCommand command)
    {
        Activity.Current?.SetTag(FortressSoulsTelemetry.DfHackCommandTagName, command.ToString());
        Activity.Current?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.SuccessOutcome);
    }

    private static void TagFailure(DfHackCommand command, string errorCategory)
    {
        Activity.Current?.SetTag(FortressSoulsTelemetry.DfHackCommandTagName, command.ToString());
        Activity.Current?.SetTag(FortressSoulsTelemetry.OperationOutcomeTagName, FortressSoulsTelemetry.ErrorOutcome);
        Activity.Current?.SetTag(FortressSoulsTelemetry.ErrorCategoryTagName, errorCategory);
    }

    private static string BuildSafeMessage(DfHackProcessFailureCategory category) =>
        category switch
        {
            DfHackProcessFailureCategory.Unavailable => "DFHack is unavailable.",
            DfHackProcessFailureCategory.ExecutableUnavailable => "DFHack executable is unavailable.",
            DfHackProcessFailureCategory.Timeout => "DFHack invocation timed out.",
            DfHackProcessFailureCategory.Crashed => "DFHack process crashed.",
            DfHackProcessFailureCategory.OutputTooLarge => "DFHack output exceeded limits.",
            DfHackProcessFailureCategory.Cancelled => "DFHack invocation was cancelled.",
            _ => "DFHack invocation failed."
        };

    private static DwarfFortressDataErrorCode MapFailureCode(DfHackProcessFailureCategory category) =>
        category switch
        {
            DfHackProcessFailureCategory.Unavailable => DwarfFortressDataErrorCode.DfHackUnavailable,
            DfHackProcessFailureCategory.ExecutableUnavailable => DwarfFortressDataErrorCode.DfHackExecutableUnavailable,
            DfHackProcessFailureCategory.Timeout => DwarfFortressDataErrorCode.DfHackInvocationTimedOut,
            DfHackProcessFailureCategory.Crashed => DwarfFortressDataErrorCode.DfHackProcessCrashed,
            DfHackProcessFailureCategory.OutputTooLarge => DwarfFortressDataErrorCode.DfHackOutputTooLarge,
            DfHackProcessFailureCategory.Cancelled => DwarfFortressDataErrorCode.DfHackInvocationTimedOut,
            _ => DwarfFortressDataErrorCode.DfHackInvocationFailed
        };

    private static string MapFailureCategory(DfHackProcessFailureCategory category) =>
        category switch
        {
            DfHackProcessFailureCategory.Unavailable => "unavailable",
            DfHackProcessFailureCategory.ExecutableUnavailable => "executable_unavailable",
            DfHackProcessFailureCategory.Timeout => "timeout",
            DfHackProcessFailureCategory.Cancelled => "cancelled",
            DfHackProcessFailureCategory.Crashed => "crashed",
            DfHackProcessFailureCategory.OutputTooLarge => "output_too_large",
            _ => "failed"
        };
}
