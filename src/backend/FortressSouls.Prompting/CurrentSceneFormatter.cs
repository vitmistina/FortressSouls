namespace FortressSouls.Prompting;

using System.Globalization;
using System.Text;
using FortressSouls.Domain;

public sealed record CurrentSceneFormatResult(
    string Text,
    int SiteWidth,
    int SiteHeight,
    int LocalWidth,
    int LocalHeight,
    int WarningCount);

public static class CurrentSceneFormatter
{
    public const int MaximumFormattedCharacters = 8_192;

    public static CurrentSceneFormatResult Format(CurrentSceneObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        observation.Validate();

        var builder = new StringBuilder();
        builder.Append("CURRENT_PERCEPTION format=")
            .Append(CurrentSceneSchema.FormatVersion)
            .Append(" schema=")
            .Append(observation.SchemaVersion)
            .Append("\n")
            .Append("time=")
            .Append(FormatTime(observation.GameTime))
            .Append("\norientation=north_up\ncoverage=revealed_fortress_state\ncolour_semantics=canonical16\n\n")
            .Append("SELF environment=")
            .Append(observation.Observer.ModelName)
            .Append(" outside=")
            .Append(YesNo(observation.Observer.Outside))
            .Append(" light=")
            .Append(YesNo(observation.Observer.Light))
            .Append(" subterranean=")
            .Append(YesNo(observation.Observer.Subterranean))
            .Append("\nSELF_TILE shape=")
            .Append(observation.Observer.TerrainShape)
            .Append(" material=")
            .Append(observation.Observer.TerrainMaterial)
            .Append(" structure=")
            .Append(observation.Observer.StructureClass ?? "none")
            .Append("\n\n");

        AppendMap(builder, "SITE", observation.SiteOverview, isSiteOverview: true);
        AppendColourMap(builder, "SITE_COLOUR", observation.SiteOverview);
        AppendMap(builder, "LOCAL", observation.LocalMap, isSiteOverview: false, includeRadius: true);
        AppendColourMap(builder, "LOCAL_COLOUR", observation.LocalMap);
        AppendDetails(builder, observation.Details);
        AppendLegends(builder);
        AppendWarnings(builder, observation.Warnings);
        builder.Append("END_CURRENT_PERCEPTION\n");

        if (builder.Length > MaximumFormattedCharacters)
        {
            throw new CurrentSceneValidationException("The formatted current scene exceeds its character limit.");
        }

        return new CurrentSceneFormatResult(
            builder.ToString(),
            observation.SiteOverview.Width,
            observation.SiteOverview.Height,
            observation.LocalMap.Width,
            observation.LocalMap.Height,
            observation.Warnings.Count);
    }

    public static string CreateSafePreview(CurrentSceneObservation observation)
    {
        var formatted = Format(observation);
        return CreateSafePreview(
            observation.SchemaVersion,
            formatted.Text.Length,
            formatted.SiteWidth,
            formatted.SiteHeight,
            formatted.LocalWidth,
            formatted.LocalHeight,
            formatted.WarningCount,
            unavailable: false);
    }

    public static string CreateUnavailablePreview() => CreateSafePreview(
        schemaVersion: null,
        formattedCharacters: null,
        siteWidth: null,
        siteHeight: null,
        localWidth: null,
        localHeight: null,
        warningCount: null,
        unavailable: true);

    private static string CreateSafePreview(
        string? schemaVersion,
        int? formattedCharacters,
        int? siteWidth,
        int? siteHeight,
        int? localWidth,
        int? localHeight,
        int? warningCount,
        bool unavailable)
    {
        var builder = new StringBuilder("CURRENT_PERCEPTION:\n");
        if (unavailable)
        {
            builder.Append("outcome=unavailable\n");
            return builder.ToString();
        }

        builder.Append("outcome=success\n")
            .Append("schema=").Append(schemaVersion).Append('\n')
            .Append("format=").Append(CurrentSceneSchema.FormatVersion).Append('\n')
            .Append("formattedCharacters=").Append(formattedCharacters?.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append("site=").Append(siteWidth).Append('x').Append(siteHeight).Append('\n')
            .Append("local=").Append(localWidth).Append('x').Append(localHeight).Append('\n')
            .Append("warningCount=").Append(warningCount?.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append("content=[redacted]\n");
        return builder.ToString();
    }

    private static void AppendMap(
        StringBuilder builder,
        string name,
        PerceptionMap map,
        bool isSiteOverview,
        bool includeRadius = false)
    {
        builder.Append('[').Append(name)
            .Append(" projection=").Append(map.Projection)
            .Append(" size=").Append(map.Width).Append('x').Append(map.Height)
            .Append(" sampled=").Append(YesNo(map.Sampled));
        if (includeRadius)
        {
            builder.Append(" radius=16");
        }

        builder.Append("]\n");
        for (var row = 0; row < map.Height; row++)
        {
            for (var column = 0; column < map.Width; column++)
            {
                builder.Append(ComposeGlyph(
                    map.TerrainRows[row][column],
                    map.FeatureRows[row][column],
                    map.MaterialRows[row][column],
                    map.UnitRows[row][column]));
            }

            builder.Append('\n');
        }
        builder.Append('\n');
    }

    private static void AppendColourMap(StringBuilder builder, string name, PerceptionMap map)
    {
        builder.Append('[').Append(name).Append("]\n");
        for (var row = 0; row < map.Height; row++)
        {
            for (var column = 0; column < map.Width; column++)
            {
                builder.Append(GetColour(
                    map.TerrainRows[row][column],
                    map.FeatureRows[row][column],
                    map.MaterialRows[row][column],
                    map.UnitRows[row][column]));
            }

            builder.Append('\n');
        }

        builder.Append('\n');
    }

    private static void AppendDetails(StringBuilder builder, IReadOnlyList<PerceptionCellDetail> details)
    {
        builder.Append("[DETAILS]\n");
        if (details.Count == 0)
        {
            builder.Append("none\n\n");
            return;
        }

        foreach (var detail in details.OrderBy(detail => detail.Dy).ThenBy(detail => detail.Dx))
        {
            builder.Append("dx=").Append(FormatSigned(detail.Dx))
                .Append(" dy=").Append(FormatSigned(detail.Dy));
            if (detail.CitizenCount > 0)
            {
                builder.Append(" citizens=").Append(detail.CitizenCount);
            }
            if (detail.OtherUnitCount > 0)
            {
                builder.Append(" otherUnits=").Append(detail.OtherUnitCount);
            }
            if (detail.InvaderCount > 0)
            {
                builder.Append(" invaders=").Append(detail.InvaderCount);
            }
            if (detail.DangerousUnitCount > 0)
            {
                builder.Append(" dangerous=").Append(detail.DangerousUnitCount);
            }
            if (detail.StructureClass is not null)
            {
                builder.Append(" structure=").Append(detail.StructureClass);
            }
            if (detail.LiquidDepth is not null)
            {
                builder.Append(" liquidDepth=").Append(detail.LiquidDepth.Value);
            }
            if (detail.Items is not null)
            {
                builder.Append(" items=").Append(detail.Items.ObjectCount)
                    .Append(" quantity=").Append(detail.Items.StackQuantity)
                    .Append(" categories=")
                    .Append(string.Join(',', detail.Items.Categories.Select(category =>
                        $"{category.Category}:{category.StackQuantity}")));
            }

            builder.Append('\n');
        }

        builder.Append('\n');
    }

    private static void AppendLegends(StringBuilder builder)
    {
        builder.Append("[GLYPH_LEGEND]\n")
            .Append("@=observer !=danger I=invader d=citizen u=other_unit M=magma ~=water F=fire W=wagon B=building C=construction *=loose_items T=tree p=plant .=ground #=wall ^=ramp ?=unavailable\n\n")
            .Append("[COLOUR_LEGEND]\n")
            .Append("0=open 2=plant_or_grass 6=soil_or_wood 7=stone_or_structure 8=unavailable 9=water A=tree B=ice C=danger_or_magma E=fire_or_wagon_or_item F=observer_or_citizen\n\n");
    }

    private static void AppendWarnings(StringBuilder builder, IReadOnlyList<string> warnings)
    {
        builder.Append("[WARNINGS]\n");
        if (warnings.Count == 0)
        {
            builder.Append("none\n\n");
            return;
        }

        foreach (var warning in warnings)
        {
            builder.Append(warning).Append('\n');
        }
        builder.Append('\n');
    }

    private static char ComposeGlyph(char terrain, char feature, char material, char unit) =>
        unit != ' ' ? unit : feature != ' ' ? feature : terrain;

    private static char GetColour(char terrain, char feature, char material, char unit)
    {
        var glyph = ComposeGlyph(terrain, feature, material, unit);
        return glyph switch
        {
            '?' => '8',
            '@' or 'd' => 'F',
            'u' => '7',
            'I' or '!' => 'C',
            '~' => '9',
            'M' => 'C',
            'F' or 'W' or '*' => 'E',
            'B' or 'C' => '7',
            'T' => 'A',
            'p' => '2',
            ' ' => '0',
            '#' or '^' or '<' or '>' or 'X' => '7',
            '.' => material switch
            {
                'g' => '2',
                's' => '6',
                'r' => '7',
                'w' => '6',
                'm' => '7',
                'i' => 'B',
                'c' or 'o' => '7',
                _ => '0'
            },
            _ => '7'
        };
    }

    private static string FormatTime(PerceptionGameTime? gameTime) =>
        gameTime is null
            ? "unavailable"
            : $"{gameTime.Year.ToString(CultureInfo.InvariantCulture)}:{gameTime.Tick.ToString(CultureInfo.InvariantCulture)}";

    private static string FormatSigned(int value) =>
        value >= 0 ? $"+{value.ToString(CultureInfo.InvariantCulture)}" : value.ToString(CultureInfo.InvariantCulture);

    private static string YesNo(bool value) => value ? "yes" : "no";
}
