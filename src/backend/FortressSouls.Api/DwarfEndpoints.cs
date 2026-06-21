namespace FortressSouls.Api;

using FortressSouls.Application;
using FortressSouls.Domain;
using FortressSouls.Observability;
using Microsoft.AspNetCore.Http.HttpResults;

internal static class DwarfEndpoints
{
    private const string ApiListSchemaVersion = "dwarf-list.v0.1";
    private const string ApiSnapshotSchemaVersion = "dwarf-snapshot.v0.1";
    private const long SyntheticGameTick = 123456;
    private const string SyntheticExtractedAt = "2026-06-18T00:00:00Z";

    public static IEndpointRouteBuilder MapDwarfEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dwarves");

        group.MapGet("/", ListDwarvesAsync)
            .WithName("ListDwarves")
            .Produces<DwarfListResponse>()
            .Produces<ApiErrorResponse>(StatusCodes.Status408RequestTimeout)
            .Produces<ApiErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{dwarfId}/snapshot", GetDwarfSnapshotAsync)
            .WithName("GetDwarfSnapshot")
            .Produces<DwarfSnapshotResponse>()
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status408RequestTimeout)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> ListDwarvesAsync(
        DwarfQueryService queryService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("FortressSouls.Api.Dwarves");

        try
        {
            var result = await queryService.ListDwarvesAsync(cancellationToken);
            logger.LogInformation(
                "Dwarf list completed with {Operation} for {AdapterType} in {DurationMs} ms",
                "ListDwarves",
                result.AdapterType,
                result.Duration.TotalMilliseconds);

            return TypedResults.Ok(MapList(result));
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Dwarf list failed with {Operation} and {ErrorCode}",
                "ListDwarves",
                "request_cancelled");

            return TypedResults.Json(
                new ApiErrorResponse("request_cancelled", "The request was cancelled."),
                statusCode: StatusCodes.Status408RequestTimeout);
        }
        catch (DwarfFortressDataException exception)
        {
            var (statusCode, errorCode, message) = MapDataException(exception);

            logger.LogWarning(
                "Dwarf list failed with {Operation} and {ErrorCode}",
                "ListDwarves",
                errorCode);

            return TypedResults.Json(new ApiErrorResponse(errorCode, message), statusCode: statusCode);
        }
    }

    private static async Task<IResult> GetDwarfSnapshotAsync(
        string dwarfId,
        DwarfQueryService queryService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("FortressSouls.Api.Dwarves");

        if (!TryParseDwarfId(dwarfId, out var parsedDwarfId))
        {
            return TypedResults.BadRequest(new ApiErrorResponse("invalid_dwarf_id", "The provided dwarf ID is invalid."));
        }

        try
        {
            var result = await queryService.GetDwarfSnapshotAsync(parsedDwarfId, cancellationToken);
            logger.LogInformation(
                "Dwarf snapshot completed with {Operation} for {DwarfId} via {AdapterType} in {DurationMs} ms",
                "GetDwarfSnapshot",
                parsedDwarfId.ToString(),
                result.AdapterType,
                result.Duration.TotalMilliseconds);

            return TypedResults.Ok(MapSnapshot(result));
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Dwarf snapshot failed with {Operation} and {ErrorCode}",
                "GetDwarfSnapshot",
                "request_cancelled");

            return TypedResults.Json(
                new ApiErrorResponse("request_cancelled", "The request was cancelled."),
                statusCode: StatusCodes.Status408RequestTimeout);
        }
        catch (DwarfNotFoundException)
        {
            logger.LogWarning(
                "Dwarf snapshot failed with {Operation} and {ErrorCode}",
                "GetDwarfSnapshot",
                "dwarf_not_found");

            return TypedResults.NotFound(new ApiErrorResponse("dwarf_not_found", "The requested dwarf was not found."));
        }
        catch (DwarfFortressDataException exception)
        {
            var (statusCode, errorCode, message) = MapDataException(exception);

            logger.LogWarning(
                "Dwarf snapshot failed with {Operation} and {ErrorCode}",
                "GetDwarfSnapshot",
                errorCode);

            return TypedResults.Json(new ApiErrorResponse(errorCode, message), statusCode: statusCode);
        }
    }

    private static bool TryParseDwarfId(string value, out DwarfId dwarfId)
    {
        try
        {
            dwarfId = DwarfId.Parse(value);
            return true;
        }
        catch (ArgumentException)
        {
            dwarfId = default;
            return false;
        }
    }

    private static (int StatusCode, string ErrorCode, string Message) MapDataException(DwarfFortressDataException exception) =>
        exception.ErrorCode switch
        {
            DwarfFortressDataErrorCode.MissingSource or DwarfFortressDataErrorCode.SourceUnavailable =>
                (StatusCodes.Status503ServiceUnavailable, "dwarf_source_unavailable", "The dwarf data source is unavailable."),
            DwarfFortressDataErrorCode.DfHackUnavailable =>
                (StatusCodes.Status503ServiceUnavailable, "dfhack_unavailable", "DFHack is unavailable."),
            DwarfFortressDataErrorCode.DfHackExecutableUnavailable =>
                (StatusCodes.Status503ServiceUnavailable, "dfhack_executable_unavailable", "DFHack executable is unavailable."),
            DwarfFortressDataErrorCode.DfHackInvocationTimedOut =>
                (StatusCodes.Status503ServiceUnavailable, "dfhack_timeout", "DFHack invocation timed out."),
            DwarfFortressDataErrorCode.DfHackProcessCrashed =>
                (StatusCodes.Status503ServiceUnavailable, "dfhack_crashed", "DFHack invocation crashed."),
            DwarfFortressDataErrorCode.DfHackInvocationFailed =>
                (StatusCodes.Status503ServiceUnavailable, "dfhack_invocation_failed", "DFHack invocation failed."),
            DwarfFortressDataErrorCode.DfHackOutputTooLarge =>
                (StatusCodes.Status503ServiceUnavailable, "dfhack_output_too_large", "DFHack output exceeded configured limits."),
            DwarfFortressDataErrorCode.InvalidConfiguration =>
                (StatusCodes.Status500InternalServerError, "dwarf_configuration_invalid", "The dwarf data configuration is invalid."),
            _ => (StatusCodes.Status500InternalServerError, "dwarf_data_invalid", "The dwarf data source returned invalid data.")
        };

    private static DwarfListResponse MapList(DwarfListQueryResult result) =>
        new(
            Items: result.List.Items
                .Select(item => new DwarfListItemResponse(
                    Id: item.Id.ToString(),
                    DisplayName: item.DisplayName,
                    Profession: item.ProfessionName,
                    CurrentJob: item.CurrentJobType,
                    StressLevel: item.StressCategoryScale))
                .ToArray(),
            Source: new DwarfListSourceResponse(
                Adapter: result.AdapterType,
                SnapshotTick: SyntheticGameTick,
                SchemaVersion: ApiListSchemaVersion));

    private static DwarfSnapshotResponse MapSnapshot(DwarfSnapshotQueryResult result) =>
        new(
            SchemaVersion: ApiSnapshotSchemaVersion,
            DwarfId: result.Snapshot.RequestedDwarfId.ToString(),
            ExtractedAt: SyntheticExtractedAt,
            GameTick: SyntheticGameTick,
            Identity: new DwarfIdentityResponse(
                DisplayName: result.Snapshot.Identity.ReadableName,
                Profession: result.Snapshot.Identity.ProfessionName),
            Work: new DwarfWorkResponse(
                CurrentJob: result.Snapshot.Work.CurrentJobType,
                Labors: []),
            Skills: result.Snapshot.Skills.Items.Select(MapSkill).ToArray(),
            Personality: new DwarfPersonalityResponse(
                Traits: result.Snapshot.Personality.Traits.Items.Select(MapTrait).ToArray(),
                Values: result.Snapshot.Personality.Values.Items.Select(MapValue).ToArray()),
            Needs: [],
            Relationships: [],
            Health: new DwarfHealthResponse("No known injuries."),
            Debug: new DwarfSnapshotDebugResponse(result.AdapterType, RawAvailable: false));

    private static DwarfSkillResponse MapSkill(DwarfSkill skill) =>
        new(skill.Token, skill.Rating, DescribeSkillLevel(skill.Rating));

    private static DwarfPersonalityTraitResponse MapTrait(DwarfPersonalityTrait trait) =>
        new(trait.Token, trait.Value, InterpretPersonalityValue(trait.Value));

    private static DwarfValueResponse MapValue(DwarfValue value) =>
        new(value.Token, value.Strength, InterpretPersonalityValue(value.Strength));

    private static string DescribeSkillLevel(int level) =>
        level switch
        {
            <= 0 => "Untrained",
            <= 3 => "Novice",
            <= 6 => "Competent",
            <= 9 => "Skilled",
            <= 12 => "Expert",
            _ => "Legendary"
        };

    private static string InterpretPersonalityValue(int rawValue) =>
        rawValue switch
        {
            <= 24 => "Strongly below neutral.",
            <= 39 => "Below neutral.",
            <= 60 => "Near neutral.",
            <= 75 => "Above neutral.",
            _ => "Strongly above neutral."
        };
}
