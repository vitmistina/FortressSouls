namespace FortressSouls.Domain;

public static class DwarfSchemaVersions
{
    public const string List = "fortress-souls-dwarf-list.v0.1";
    public const string Snapshot = "fortress-souls-dwarf-snapshot.v0.1";
}

public sealed record DwarfStatusFlags(
    bool IsActive,
    bool IsAlive,
    bool IsCitizen,
    bool IsResident,
    bool IsSane);

public sealed record DwarfSummary(
    DwarfId Id,
    string DisplayName,
    string ProfessionName,
    string ProfessionToken,
    string? CurrentJobType,
    int StressCategory,
    string StressCategoryScale,
    bool SoulPresent,
    DwarfStatusFlags Flags);

public sealed record DwarfListSourceMetadata(
    bool WorldLoaded,
    bool SiteLoaded,
    bool MapLoaded);

public sealed record DwarfListResult(
    string SchemaVersion,
    DwarfListSourceMetadata Source,
    IReadOnlyList<DwarfSummary> Items);

public sealed record DwarfSnapshotSourceMetadata(
    bool WorldLoaded,
    bool SiteLoaded,
    bool MapLoaded,
    bool SoulPresent);

public sealed record DwarfIdentity(
    DwarfId Id,
    string ReadableName,
    string ProfessionName,
    string ProfessionToken,
    string CreatureId,
    string CasteId);

public sealed record DwarfWork(string? CurrentJobType);

public sealed record DwarfStress(
    int Raw,
    int Longterm,
    int Category,
    string CategoryScale);

public sealed record DwarfSkill(
    string Token,
    int Rating,
    int Effective,
    int Nominal,
    int Experience,
    int TotalExperience,
    int Rust);

public sealed record DwarfSkillCollection(
    int Count,
    IReadOnlyList<DwarfSkill> Items);

public sealed record DwarfPersonalityTrait(
    string Token,
    int Value,
    int DeviationFromNeutral50,
    int AbsDeviationFromNeutral50);

public sealed record DwarfTraitCollection(
    int Count,
    IReadOnlyList<DwarfPersonalityTrait> Items);

public sealed record DwarfValue(
    string Token,
    int Type,
    int Strength);

public sealed record DwarfValueCollection(
    int Count,
    IReadOnlyList<DwarfValue> Items);

public sealed record DwarfNeed(
    string Token,
    int Id,
    int DeityId,
    int FocusLevel,
    int NeedLevel,
    bool IsUnmet,
    bool IsDeeplyUnmet);

public sealed record DwarfNeedCollection(
    int Count,
    IReadOnlyList<DwarfNeed> Items);

public sealed record DwarfMannerism(
    string Token,
    string SituationToken);

public sealed record DwarfMannerismCollection(
    int Count,
    IReadOnlyList<DwarfMannerism> Items);

public sealed record DwarfPersonality(
    bool Present,
    DwarfTraitCollection Traits,
    DwarfValueCollection Values,
    DwarfNeedCollection Needs,
    DwarfMannerismCollection Mannerisms);

public sealed record DwarfPromptCandidates(
    IReadOnlyList<DwarfSkill> TopSkills,
    IReadOnlyList<DwarfPersonalityTrait> ExtremeTraits,
    IReadOnlyList<DwarfValue> StrongValues,
    IReadOnlyList<DwarfNeed> StrongNeeds,
    IReadOnlyList<DwarfMannerism> Mannerisms);

public sealed record DwarfSnapshot(
    string SchemaVersion,
    DwarfSnapshotSourceMetadata Source,
    DwarfId RequestedDwarfId,
    DwarfIdentity Identity,
    DwarfWork Work,
    DwarfStress Stress,
    DwarfSkillCollection Skills,
    DwarfPersonality Personality,
    DwarfPromptCandidates PromptCandidates);
