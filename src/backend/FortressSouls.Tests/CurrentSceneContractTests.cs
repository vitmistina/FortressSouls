namespace FortressSouls.Tests;

using FortressSouls.Domain;
using FortressSouls.Application;
using FortressSouls.Prompting;

public sealed class CurrentSceneContractTests
{
    [Fact]
    public void FakeScene_IsValidatedAndFormatsDeterministically()
    {
        var scene = FakeCurrentSceneFixture.Default.Validate();

        var first = CurrentSceneFormatter.Format(scene);
        var second = CurrentSceneFormatter.Format(scene);

        Assert.Equal(first.Text, second.Text);
        Assert.Equal(33, scene.LocalMap.Width);
        Assert.Equal(33, scene.LocalMap.Height);
        Assert.Equal('@', scene.LocalMap.UnitRows[16][16]);
        Assert.Contains("CURRENT_PERCEPTION format=FSMP/1", first.Text, StringComparison.Ordinal);
        Assert.Contains("SELF environment=above_ground_outdoors outside=yes light=yes subterranean=no", first.Text, StringComparison.Ordinal);
        Assert.Contains("W", first.Text, StringComparison.Ordinal);
        Assert.Contains("d", first.Text, StringComparison.Ordinal);
        Assert.Contains("?", first.Text, StringComparison.Ordinal);
        Assert.True(first.Text.Length <= CurrentSceneFormatter.MaximumFormattedCharacters);
    }

    [Fact]
    public void SafePreview_RedactsSceneRows()
    {
        var preview = CurrentSceneFormatter.CreateSafePreview(FakeCurrentSceneFixture.Default);

        Assert.Contains("outcome=success", preview, StringComparison.Ordinal);
        Assert.Contains("content=[redacted]", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("[SITE projection=", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("[LOCAL projection=", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("@", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void HiddenCellLeakage_IsRejected()
    {
        var leakedFeatures = FakeCurrentSceneFixture.Default.LocalMap.FeatureRows.ToArray();
        leakedFeatures[0] = "*" + leakedFeatures[0][1..];
        var invalid = FakeCurrentSceneFixture.Default with
        {
            LocalMap = FakeCurrentSceneFixture.Default.LocalMap with { FeatureRows = leakedFeatures }
        };

        Assert.Throws<CurrentSceneValidationException>(() => { invalid.Validate(); });
    }

    [Fact]
    public void ContradictoryEnvironmentFlags_RequireUnknownAndWarning()
    {
        var invalid = FakeCurrentSceneFixture.Default with
        {
            Observer = FakeCurrentSceneFixture.Default.Observer with
            {
                Environment = ObserverEnvironment.AboveGroundOutdoors,
                Outside = true,
                Subterranean = true
            }
        };

        Assert.Throws<CurrentSceneValidationException>(() => { invalid.Validate(); });

        var corrected = invalid with
        {
            Observer = invalid.Observer with { Environment = ObserverEnvironment.Unknown },
            Warnings = ["ENVIRONMENT_FLAGS_CONTRADICTORY"]
        };
        corrected.Validate();
    }
}
