namespace FortressSouls.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Runtime.CompilerServices;
using FortressSouls.Application;
using FortressSouls.Domain;
using FortressSouls.DwarfFortress;

public sealed class JsonFileDwarfFortressAdapterTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "FortressSouls.Tests",
        Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));

    public JsonFileDwarfFortressAdapterTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task ListDwarvesAsync_MapsCanonicalSample()
    {
        var adapter = CreateAdapterFromSamples();

        var result = await adapter.ListDwarvesAsync(CancellationToken.None);

        Assert.Equal(DwarfSchemaVersions.List, result.SchemaVersion);
        Assert.True(result.Source.WorldLoaded);
        Assert.True(result.Source.SiteLoaded);
        Assert.True(result.Source.MapLoaded);
        Assert.Equal(7, result.Items.Count);

        var first = result.Items[0];
        Assert.Equal(DwarfId.Parse("6597"), first.Id);
        Assert.Equal("Melbil Keskalmeden \"Shottribe\", Miner", first.DisplayName);
        Assert.Equal("Miner", first.ProfessionName);
        Assert.Equal("PickupEquipment", first.CurrentJobType);
        Assert.True(first.Flags.IsCitizen);
    }

    [Fact]
    public async Task GetDwarfSnapshotAsync_MapsCanonicalSample()
    {
        var adapter = CreateAdapterFromSamples();

        var result = await adapter.GetDwarfSnapshotAsync(DwarfId.Parse("6597"), CancellationToken.None);

        Assert.Equal(DwarfSchemaVersions.Snapshot, result.SchemaVersion);
        Assert.Equal(DwarfId.Parse("6597"), result.RequestedDwarfId);
        Assert.Equal(DwarfId.Parse("6597"), result.Identity.Id);
        Assert.Equal("Melbil Keskalmeden \"Shottribe\", Miner", result.Identity.ReadableName);
        Assert.Equal("PickupEquipment", result.Work.CurrentJobType);
        Assert.Equal(7, result.Skills.Count);
        Assert.Equal(21, result.Personality.Needs.Count);
        Assert.Equal("DrinkAlcohol", result.PromptCandidates.StrongNeeds[0].Token);
    }

    [Fact]
    public async Task CanonicalBundleSnapshots_MapAcrossAllCapturedDwarves()
    {
        using var bundle = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(GetRepoRoot(), "dfhack", "samples", "b019-dwarf-snapshots.bundle.json")));
        var snapshots = bundle.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();

        Assert.Equal(7, snapshots.Length);

        foreach (var snapshot in snapshots)
        {
            var requestedUnitId = snapshot.GetProperty("requestedUnitId").GetString();
            Assert.False(string.IsNullOrWhiteSpace(requestedUnitId));

            var snapshotPath = WriteJsonElement("bundle-snapshot.json", snapshot);
            var adapter = CreateAdapter(
                listPath: Path.Combine(GetRepoRoot(), "dfhack", "samples", "dwarves-list.sample.json"),
                snapshotPath: snapshotPath);

            var mapped = await adapter.GetDwarfSnapshotAsync(DwarfId.Parse(requestedUnitId!), CancellationToken.None);

            Assert.Equal(DwarfId.Parse(requestedUnitId!), mapped.Identity.Id);
            Assert.Equal(mapped.Skills.Count, mapped.Skills.Items.Count);
            Assert.Equal(mapped.Personality.Needs.Count, mapped.Personality.Needs.Items.Count);
        }
    }

    [Fact]
    public async Task GetDwarfSnapshotAsync_ToleratesCanonicalEmptyWorkArrays()
    {
        using var bundle = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(GetRepoRoot(), "dfhack", "samples", "b019-dwarf-snapshots.bundle.json")));
        var snapshot = bundle.RootElement
            .GetProperty("snapshots")
            .EnumerateArray()
            .First(element => element.GetProperty("requestedUnitId").GetString() == "6603");

        var adapter = CreateAdapter(
            listPath: Path.Combine(GetRepoRoot(), "dfhack", "samples", "dwarves-list.sample.json"),
            snapshotPath: WriteJsonElement("snapshot-6603.json", snapshot));

        var mapped = await adapter.GetDwarfSnapshotAsync(DwarfId.Parse("6603"), CancellationToken.None);

        Assert.Null(mapped.Work.CurrentJobType);
        Assert.Equal(2, mapped.Personality.Mannerisms.Count);
    }

    [Fact]
    public async Task GetDwarfSnapshotAsync_ThrowsInconsistentData_WhenRequestedIdDoesNotMatchSnapshot()
    {
        var adapter = CreateAdapterFromSamples();

        var exception = await Assert.ThrowsAsync<DwarfFortressDataException>(() =>
            adapter.GetDwarfSnapshotAsync(DwarfId.Parse("6603"), CancellationToken.None));

        Assert.Equal(DwarfFortressDataErrorCode.InconsistentData, exception.ErrorCode);
        Assert.DoesNotContain("dwarf-snapshot.sample.json", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListDwarvesAsync_ThrowsMissingSource_WhenConfiguredFileIsMissing()
    {
        var adapter = CreateAdapter(
            listPath: Path.Combine(_tempDirectory, "missing-list.json"),
            snapshotPath: Path.Combine(GetRepoRoot(), "dfhack", "samples", "dwarf-snapshot.sample.json"));

        var exception = await Assert.ThrowsAsync<DwarfFortressDataException>(() =>
            adapter.ListDwarvesAsync(CancellationToken.None));

        Assert.Equal(DwarfFortressDataErrorCode.MissingSource, exception.ErrorCode);
        Assert.DoesNotContain("missing-list.json", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListDwarvesAsync_ThrowsSourceUnavailable_WhenConfiguredPathIsADirectory()
    {
        var adapter = CreateAdapter(
            listPath: _tempDirectory,
            snapshotPath: Path.Combine(GetRepoRoot(), "dfhack", "samples", "dwarf-snapshot.sample.json"));

        var exception = await Assert.ThrowsAsync<DwarfFortressDataException>(() =>
            adapter.ListDwarvesAsync(CancellationToken.None));

        Assert.Equal(DwarfFortressDataErrorCode.SourceUnavailable, exception.ErrorCode);
    }

    [Fact]
    public async Task ListDwarvesAsync_ThrowsMalformedJson_WhenJsonIsInvalid()
    {
        var adapter = CreateAdapter(
            listPath: WriteText("invalid-list.json", "{ not-json"),
            snapshotPath: Path.Combine(GetRepoRoot(), "dfhack", "samples", "dwarf-snapshot.sample.json"));

        var exception = await Assert.ThrowsAsync<DwarfFortressDataException>(() =>
            adapter.ListDwarvesAsync(CancellationToken.None));

        Assert.Equal(DwarfFortressDataErrorCode.MalformedJson, exception.ErrorCode);
    }

    [Fact]
    public async Task ListDwarvesAsync_ThrowsUnsupportedSchema_WhenSchemaVersionIsUnknown()
    {
        var document = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(GetRepoRoot(), "dfhack", "samples", "dwarves-list.sample.json")))
            ?.AsObject()
            ?? throw new InvalidOperationException("Unable to parse the canonical dwarf list sample.");
        document["schemaVersion"] = "fortress-souls-dwarf-list.v9.9";

        var adapter = CreateAdapter(
            listPath: WriteText("unsupported-list.json", document.ToJsonString()),
            snapshotPath: Path.Combine(GetRepoRoot(), "dfhack", "samples", "dwarf-snapshot.sample.json"));

        var exception = await Assert.ThrowsAsync<DwarfFortressDataException>(() =>
            adapter.ListDwarvesAsync(CancellationToken.None));

        Assert.Equal(DwarfFortressDataErrorCode.UnsupportedSchema, exception.ErrorCode);
    }

    [Fact]
    public async Task GetDwarfSnapshotAsync_ThrowsInvalidData_WhenIdentityIsMissing()
    {
        var document = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(GetRepoRoot(), "dfhack", "samples", "dwarf-snapshot.sample.json")))
            ?.AsObject()
            ?? throw new InvalidOperationException("Unable to parse the canonical dwarf snapshot sample.");
        var identity = document["identity"];
        document.Remove("identity");
        document["identityMissing"] = identity;

        var adapter = CreateAdapter(
            listPath: Path.Combine(GetRepoRoot(), "dfhack", "samples", "dwarves-list.sample.json"),
            snapshotPath: WriteText("invalid-snapshot.json", document.ToJsonString()));

        var exception = await Assert.ThrowsAsync<DwarfFortressDataException>(() =>
            adapter.GetDwarfSnapshotAsync(DwarfId.Parse("6597"), CancellationToken.None));

        Assert.Equal(DwarfFortressDataErrorCode.InvalidData, exception.ErrorCode);
    }

    [Fact]
    public async Task ListDwarvesAsync_ThrowsDataTooLarge_WhenFileExceedsConfiguredLimit()
    {
        var listPath = WriteText(
            "large-list.json",
            new string('a', 512));

        var adapter = CreateAdapter(
            listPath: listPath,
            snapshotPath: Path.Combine(GetRepoRoot(), "dfhack", "samples", "dwarf-snapshot.sample.json"),
            configure: options => options.MaxListFileBytes = 128);

        var exception = await Assert.ThrowsAsync<DwarfFortressDataException>(() =>
            adapter.ListDwarvesAsync(CancellationToken.None));

        Assert.Equal(DwarfFortressDataErrorCode.DataTooLarge, exception.ErrorCode);
    }

    [Fact]
    public async Task GetDwarfSnapshotAsync_HonorsCancellation()
    {
        var adapter = CreateAdapterFromSamples();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.GetDwarfSnapshotAsync(DwarfId.Parse("6597"), cancellationTokenSource.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private JsonFileDwarfFortressAdapter CreateAdapterFromSamples() =>
        CreateAdapter(
            listPath: Path.Combine(GetRepoRoot(), "dfhack", "samples", "dwarves-list.sample.json"),
            snapshotPath: Path.Combine(GetRepoRoot(), "dfhack", "samples", "dwarf-snapshot.sample.json"));

    private JsonFileDwarfFortressAdapter CreateAdapter(
        string listPath,
        string snapshotPath,
        Action<JsonFileDwarfFortressAdapterOptions>? configure = null)
    {
        var options = new JsonFileDwarfFortressAdapterOptions
        {
            DwarfListPath = listPath,
            DwarfSnapshotPath = snapshotPath
        };

        configure?.Invoke(options);

        return new JsonFileDwarfFortressAdapter(options);
    }

    private static string GetRepoRoot([CallerFilePath] string sourceFilePath = "")
    {
        var testDirectory = Path.GetDirectoryName(sourceFilePath)
            ?? throw new InvalidOperationException("Unable to determine the test source directory.");

        return Path.GetFullPath(Path.Combine(testDirectory, "..", "..", ".."));
    }

    private string WriteText(string fileName, string content)
    {
        var path = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private string WriteJsonElement(string fileName, JsonElement element) =>
        WriteText(fileName, element.GetRawText());
}
