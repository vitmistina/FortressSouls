namespace FortressSouls.Domain;

public static class CurrentSceneSchema
{
    public const string Version = "fortress-souls.current-scene.v0.2.1";
    public const string FormatVersion = "FSMP/1";
}

public sealed record PerceptionGameTime(int Year, int Tick)
{
    public void Validate()
    {
        if (Year < 0 || Tick < 0)
        {
            throw Invalid("Perception game time is invalid.");
        }
    }

    private static CurrentSceneValidationException Invalid(string message) => new(message);
}

public enum ObserverEnvironment
{
    AboveGroundOutdoors,
    AboveGroundSheltered,
    Underground,
    Unknown
}

public sealed record ObserverEnvironmentObservation(
    ObserverEnvironment Environment,
    bool Outside,
    bool Light,
    bool Subterranean,
    string TerrainShape,
    string TerrainMaterial,
    string? StructureClass)
{
    public string ModelName => Environment switch
    {
        ObserverEnvironment.AboveGroundOutdoors => "above_ground_outdoors",
        ObserverEnvironment.AboveGroundSheltered => "above_ground_sheltered",
        ObserverEnvironment.Underground => "underground",
        ObserverEnvironment.Unknown => "unknown",
        _ => throw new CurrentSceneValidationException("Observer environment is invalid.")
    };

    public void Validate()
    {
        _ = ModelName;
        ValidateToken(TerrainShape);
        ValidateToken(TerrainMaterial);
        if (StructureClass is not null)
        {
            ValidateToken(StructureClass);
        }
    }

    private static void ValidateToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64 || value.Any(char.IsControl))
        {
            throw new CurrentSceneValidationException("Observer environment data is invalid.");
        }
    }
}

public sealed record PerceptionMap(
    string Projection,
    int Width,
    int Height,
    bool Sampled,
    IReadOnlyList<string> TerrainRows,
    IReadOnlyList<string> FeatureRows,
    IReadOnlyList<string> MaterialRows,
    IReadOnlyList<string> UnitRows)
{
    private const string TerrainVocabulary = " ?. #^<>X";
    private const string FeatureVocabulary = " ~MFWBCTp*";
    private const string MaterialVocabulary = " ?gsrwmico";
    private const string UnitVocabulary = " @duI!";

    public void Validate(bool isSiteOverview)
    {
        if (string.IsNullOrWhiteSpace(Projection)
            || Width <= 0
            || Height <= 0
            || Width > (isSiteOverview ? 48 : 33)
            || Height > (isSiteOverview ? 24 : 33)
            || TerrainRows is null
            || FeatureRows is null
            || MaterialRows is null
            || UnitRows is null
            || TerrainRows.Count != Height
            || FeatureRows.Count != Height
            || MaterialRows.Count != Height
            || UnitRows.Count != Height)
        {
            throw Invalid();
        }

        ValidateRows(TerrainRows, TerrainVocabulary);
        ValidateRows(FeatureRows, FeatureVocabulary);
        ValidateRows(MaterialRows, MaterialVocabulary);
        ValidateRows(UnitRows, UnitVocabulary);

        for (var row = 0; row < Height; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                if (TerrainRows[row][column] == '?'
                    && (FeatureRows[row][column] != ' '
                        || MaterialRows[row][column] != '?'
                        || UnitRows[row][column] != ' '))
                {
                    throw Invalid();
                }
            }
        }

        if (isSiteOverview)
        {
            if (!Sampled || Projection != "surface_overview" || CountGlyph(UnitRows, '@') != 1)
            {
                throw Invalid();
            }
        }
        else if (Projection != "current_level"
                 || Sampled
                 || Width != 33
                 || Height != 33
                 || CountGlyph(UnitRows, '@') != 1
                 || UnitRows[16][16] != '@')
        {
            throw Invalid();
        }
    }

    private void ValidateRows(IReadOnlyList<string> rows, string vocabulary)
    {
        foreach (var row in rows)
        {
            if (row is null || row.Length != Width || row.Any(character => !vocabulary.Contains(character, StringComparison.Ordinal)))
            {
                throw Invalid();
            }
        }
    }

    private static int CountGlyph(IReadOnlyList<string> rows, char glyph) =>
        rows.Sum(row => row.Count(character => character == glyph));

    private static CurrentSceneValidationException Invalid() =>
        new("Perception map data is invalid.");
}

public sealed record PerceptionItemCategoryCount(string Category, int ObjectCount, int StackQuantity);

public sealed record PerceptionItemSummary(
    int ObjectCount,
    int StackQuantity,
    IReadOnlyList<PerceptionItemCategoryCount> Categories)
{
    private static readonly IReadOnlySet<string> AllowedCategories = new HashSet<string>(StringComparer.Ordinal)
    {
        "food", "drink", "wood", "stone", "clothing", "weapon", "tool", "furniture", "corpse", "other"
    };

    public void Validate()
    {
        if (ObjectCount <= 0 || StackQuantity <= 0 || Categories is null || Categories.Count is < 1 or > 5)
        {
            throw Invalid();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var category in Categories)
        {
            if (category is null
                || !AllowedCategories.Contains(category.Category)
                || !seen.Add(category.Category)
                || category.ObjectCount <= 0
                || category.StackQuantity <= 0)
            {
                throw Invalid();
            }
        }
    }

    private static CurrentSceneValidationException Invalid() =>
        new("Perception item data is invalid.");
}

public sealed record PerceptionCellDetail(
    int Dx,
    int Dy,
    int CitizenCount,
    int OtherUnitCount,
    int InvaderCount,
    int DangerousUnitCount,
    PerceptionItemSummary? Items,
    string? StructureClass,
    int? LiquidDepth)
{
    public void Validate()
    {
        if (Math.Abs(Dx) > 16 || Math.Abs(Dy) > 16
            || CitizenCount < 0
            || OtherUnitCount < 0
            || InvaderCount < 0
            || DangerousUnitCount < 0
            || CitizenCount + OtherUnitCount + InvaderCount + DangerousUnitCount == 0 && Items is null && StructureClass is null && LiquidDepth is null)
        {
            throw Invalid();
        }

        Items?.Validate();
        if (StructureClass is not null && (string.IsNullOrWhiteSpace(StructureClass) || StructureClass.Length > 64))
        {
            throw Invalid();
        }

        if (LiquidDepth is < 0)
        {
            throw Invalid();
        }
    }

    private static CurrentSceneValidationException Invalid() =>
        new("Perception detail data is invalid.");
}

public sealed record CurrentSceneObservation(
    string SchemaVersion,
    PerceptionGameTime? GameTime,
    ObserverEnvironmentObservation Observer,
    PerceptionMap SiteOverview,
    PerceptionMap LocalMap,
    IReadOnlyList<PerceptionCellDetail> Details,
    IReadOnlyList<string> Warnings)
{
    public CurrentSceneObservation Validate()
    {
        if (SchemaVersion != CurrentSceneSchema.Version
            || Observer is null
            || SiteOverview is null
            || LocalMap is null
            || Details is null
            || Warnings is null
            || Details.Count > 24
            || Warnings.Count > 8
            || Warnings.Any(warning => string.IsNullOrWhiteSpace(warning) || warning.Length > 64))
        {
            throw Invalid();
        }

        GameTime?.Validate();
        Observer.Validate();
        var expectedEnvironment = Observer.Outside switch
        {
            true when !Observer.Subterranean => ObserverEnvironment.AboveGroundOutdoors,
            false when !Observer.Subterranean => ObserverEnvironment.AboveGroundSheltered,
            false when Observer.Subterranean => ObserverEnvironment.Underground,
            _ => ObserverEnvironment.Unknown
        };
        if (Observer.Environment != expectedEnvironment)
        {
            throw Invalid();
        }
        if (expectedEnvironment == ObserverEnvironment.Unknown
            && !Warnings.Contains("ENVIRONMENT_FLAGS_CONTRADICTORY", StringComparer.Ordinal))
        {
            throw Invalid();
        }
        SiteOverview.Validate(isSiteOverview: true);
        LocalMap.Validate(isSiteOverview: false);

        var seenDetails = new HashSet<(int Dx, int Dy)>();
        foreach (var detail in Details)
        {
            if (detail is null || !seenDetails.Add((detail.Dx, detail.Dy)))
            {
                throw Invalid();
            }

            detail.Validate();
        }

        return this;
    }

    private static CurrentSceneValidationException Invalid() =>
        new("Current scene data is invalid.");
}

public sealed class CurrentSceneValidationException(string message) : Exception(message);
