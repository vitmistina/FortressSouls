namespace FortressSouls.Api;

public sealed record ApiErrorResponse(string ErrorCode, string Message);

public sealed record DwarfListResponse(
    IReadOnlyList<DwarfListItemResponse> Items,
    DwarfListSourceResponse Source);

public sealed record DwarfListItemResponse(
    string Id,
    string DisplayName,
    string Profession,
    string? CurrentJob,
    string StressLevel);

public sealed record DwarfListSourceResponse(
    string Adapter,
    long SnapshotTick,
    string SchemaVersion);

public sealed record DwarfSnapshotResponse(
    string SchemaVersion,
    string DwarfId,
    string ExtractedAt,
    long GameTick,
    DwarfIdentityResponse Identity,
    DwarfWorkResponse Work,
    IReadOnlyList<DwarfSkillResponse> Skills,
    DwarfPersonalityResponse Personality,
    IReadOnlyList<DwarfNeedSummaryResponse> Needs,
    IReadOnlyList<DwarfRelationshipResponse> Relationships,
    DwarfHealthResponse Health,
    DwarfSnapshotDebugResponse Debug);

public sealed record DwarfIdentityResponse(
    string DisplayName,
    string Profession);

public sealed record DwarfWorkResponse(
    string? CurrentJob,
    IReadOnlyList<string> Labors);

public sealed record DwarfSkillResponse(
    string Name,
    int Level,
    string Description);

public sealed record DwarfPersonalityTraitResponse(
    string Name,
    int RawValue,
    string Interpretation);

public sealed record DwarfValueResponse(
    string Name,
    int RawValue,
    string Interpretation);

public sealed record DwarfPersonalityResponse(
    IReadOnlyList<DwarfPersonalityTraitResponse> Traits,
    IReadOnlyList<DwarfValueResponse> Values);

public sealed record DwarfNeedSummaryResponse(string Name, string Summary);

public sealed record DwarfRelationshipResponse(string Type, string DisplayName);

public sealed record DwarfHealthResponse(string Summary);

public sealed record DwarfSnapshotDebugResponse(string Adapter, bool RawAvailable);

public sealed record DwarfAdapterStatusResponse(
    string AdapterType,
    bool IsConfigured,
    bool IsReady,
    string LastOutcome,
    string? LastErrorCategory,
    int? LastDurationMs,
    string? LastUpdatedAtUtc);
