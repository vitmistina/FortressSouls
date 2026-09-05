namespace FortressSouls.Tests;

using System.Text.Json;
using FortressSouls.Application;
using FortressSouls.Domain;
using FortressSouls.DwarfFortress;

public sealed class SurroundingsInspectionServiceTests
{
    [Fact]
    public async Task FixtureSurroundingsInspectionService_ReturnsDeterministicFixture()
    {
        var service = new FixtureSurroundingsInspectionService(FakePerceptionFixtureSet.Default.LookAround);

        var result = await service.InspectAroundAsync(DwarfId.Parse("4101"), 1, CancellationToken.None);

        Assert.Equal("fortress-souls.look-around-result.v0.2", result.SchemaVersion);
        Assert.Equal(1, result.Bounds.Radius);
        Assert.Equal(9, result.Cells.Count);
        Assert.Equal(["building", "floor", "ramp", "wall"], result.Legend);
    }

    [Fact]
    public async Task FixtureSurroundingsInspectionService_ReturnsMaximumBoundedRadius()
    {
        var service = new FixtureSurroundingsInspectionService(FakePerceptionFixtureSet.Default.LookAround);

        var result = await service.InspectAroundAsync(DwarfId.Parse("4101"), 2, CancellationToken.None);

        Assert.Equal("fortress-souls.look-around-result.v0.2", result.SchemaVersion);
        Assert.Equal(2, result.Bounds.Radius);
        Assert.Equal(25, result.Cells.Count);
        Assert.True(result.Cells.Count(cell => string.Equals(cell.Visibility, "hidden", StringComparison.Ordinal)) >= 2);
        Assert.Equal(["building", "floor", "ramp", "wall"], result.Legend);
    }

    [Fact]
    public async Task DfHackSurroundingsInspectionService_MapsCurrentSceneAndUsesOnlyObserverId()
    {
        var runner = new StubRunner(
            DfHackProcessCommandResult.Success(
                DfHackCommand.GetDwarfSurroundings,
                CreateCurrentSceneJson(),
                string.Empty,
                0,
                TimeSpan.FromMilliseconds(5)));
        var service = new DfHackSurroundingsInspectionService(
            runner,
            new DfHackProcessAdapterOptions
            {
                Enabled = true,
                RunPath = "C:\\dfhack\\hack\\dfhack-run.exe",
                WorkingDirectory = "C:\\dfhack\\hack",
                Host = "127.0.0.1"
            }.Validate());

        var result = await service.ObserveCurrentSceneAsync(DwarfId.Parse("4101"), CancellationToken.None);

        Assert.Equal(CurrentSceneSchema.Version, result.SchemaVersion);
        Assert.Equal(ObserverEnvironment.AboveGroundOutdoors, result.Observer.Environment);
        Assert.Equal(33, result.LocalMap.Width);
        Assert.Equal(24, result.SiteOverview.Width);
        Assert.Equal("wood", result.Details[0].Items!.Categories[0].Category);
        Assert.Equal(["4101"], runner.Arguments);
    }

    [Fact]
    public async Task DfHackSurroundingsInspectionService_MapsRetainedLiveResearchSampleToProductResult()
    {
        var service = new DfHackSurroundingsInspectionService(
            new StubRunner(
                DfHackProcessCommandResult.Success(
                    DfHackCommand.GetDwarfSurroundings,
                    await File.ReadAllTextAsync(GetRetainedLiveSpatialSamplePath()),
                    string.Empty,
                    0,
                    TimeSpan.FromMilliseconds(5))),
            new DfHackProcessAdapterOptions
            {
                Enabled = true,
                RunPath = "C:\\dfhack\\hack\\dfhack-run.exe",
                WorkingDirectory = "C:\\dfhack\\hack",
                Host = "127.0.0.1"
            }.Validate());

        var result = await service.InspectAroundAsync(DwarfId.Parse("6597"), 1, CancellationToken.None);

        Assert.Equal("fortress-souls.look-around-result.v0.2", result.SchemaVersion);
        Assert.Equal("100:16801", result.GameTime);
        Assert.Equal(1, result.Bounds.Radius);
        Assert.Equal(9, result.Cells.Count);
        Assert.Equal(["building", "floor", "ramp", "wall"], result.Legend);

        var centerCell = Assert.Single(result.Cells, cell => cell.Dx == 0 && cell.Dy == 0);
        Assert.Equal("visible", centerCell.Visibility);
        Assert.Equal("ramp", centerCell.TerrainClass);
        Assert.True(centerCell.Walkable);
        Assert.Equal(2, centerCell.UnitCount);

        var buildingCell = Assert.Single(result.Cells, cell => cell.Dx == 0 && cell.Dy == 1);
        Assert.Equal("building", buildingCell.TerrainClass);
        Assert.Equal("building", buildingCell.FeatureClass);
    }

    [Fact]
    public async Task DfHackSurroundingsInspectionService_MapsRetainedProductShapedLiveSample()
    {
        var service = new DfHackSurroundingsInspectionService(
            new StubRunner(
                DfHackProcessCommandResult.Success(
                    DfHackCommand.GetDwarfSurroundings,
                    await File.ReadAllTextAsync(GetRetainedProductSpatialSamplePath()),
                    string.Empty,
                    0,
                    TimeSpan.FromMilliseconds(5))),
            new DfHackProcessAdapterOptions
            {
                Enabled = true,
                RunPath = "C:\\dfhack\\hack\\dfhack-run.exe",
                WorkingDirectory = "C:\\dfhack\\hack",
                Host = "127.0.0.1"
            }.Validate());

        var result = await service.InspectAroundAsync(DwarfId.Parse("6603"), 2, CancellationToken.None);

        Assert.Equal("fortress-souls.look-around-result.v0.2", result.SchemaVersion);
        Assert.Equal("100:16801", result.GameTime);
        Assert.Equal(2, result.Bounds.Radius);
        Assert.Equal(25, result.Cells.Count);
        Assert.Equal(["floor", "ramp", "wall"], result.Legend);

        var centerCell = Assert.Single(result.Cells, cell => cell.Dx == 0 && cell.Dy == 0);
        Assert.Equal("visible", centerCell.Visibility);
        Assert.Equal("ramp", centerCell.TerrainClass);
        Assert.True(centerCell.Walkable);
        Assert.Equal(1, centerCell.UnitCount);

        var hiddenSouthCell = Assert.Single(result.Cells, cell => cell.Dx == 0 && cell.Dy == 2);
        Assert.Equal("hidden", hiddenSouthCell.Visibility);
        Assert.Null(hiddenSouthCell.TerrainClass);
        Assert.Null(hiddenSouthCell.Walkable);
        Assert.Null(hiddenSouthCell.FeatureClass);
        Assert.Null(hiddenSouthCell.UnitCount);
    }

    [Fact]
    public async Task DfHackSurroundingsInspectionService_StripsHiddenCellDetailsFromResearchSample()
    {
        var service = new DfHackSurroundingsInspectionService(
            new StubRunner(
                DfHackProcessCommandResult.Success(
                    DfHackCommand.GetDwarfSurroundings,
                    """
                    {"schemaVersion":"fortress-souls-spatial-vision-research.v0.1","query":{"mode":"unit","unitId":6597,"radius":1,"unitPosition":{"x":10,"y":20,"z":5}},"bounds":{"x1":9,"y1":19,"z":5,"x2":11,"y2":21,"width":3,"height":3},"gameTime":{"year":250,"tick":12345},"cells":[{"x":9,"y":19,"z":5,"hidden":true,"visible":false,"terrain":{"shape":"WALL"},"walkable":0,"building":{"id":1},"units":[{"id":7}]},{"x":10,"y":19,"z":5,"hidden":false,"visible":true,"terrain":{"shape":"FLOOR"},"walkable":4,"units":[]},{"x":11,"y":19,"z":5,"hidden":false,"visible":true,"terrain":{"shape":"FLOOR"},"walkable":4,"units":[]},{"x":9,"y":20,"z":5,"hidden":false,"visible":true,"terrain":{"shape":"WALL"},"walkable":0,"units":[]},{"x":10,"y":20,"z":5,"hidden":false,"visible":true,"terrain":{"shape":"FLOOR"},"walkable":4,"units":[{"id":8}]},{"x":11,"y":20,"z":5,"hidden":false,"visible":true,"terrain":{"shape":"RAMP"},"walkable":4,"units":[]},{"x":9,"y":21,"z":5,"hidden":false,"visible":true,"terrain":{"shape":"FLOOR"},"walkable":4,"zones":[{"id":2}]},{"x":10,"y":21,"z":5,"hidden":false,"visible":true,"terrain":{"shape":"FLOOR"},"walkable":4,"units":[]},{"x":11,"y":21,"z":5,"hidden":false,"visible":true,"terrain":{"shape":"FLOOR"},"walkable":4,"units":[]}],"warnings":[]}
                    """,
                    string.Empty,
                    0,
                    TimeSpan.FromMilliseconds(5))),
            new DfHackProcessAdapterOptions
            {
                Enabled = true,
                RunPath = "C:\\dfhack\\hack\\dfhack-run.exe",
                WorkingDirectory = "C:\\dfhack\\hack",
                Host = "127.0.0.1"
            }.Validate());

        var result = await service.InspectAroundAsync(DwarfId.Parse("6597"), 1, CancellationToken.None);

        var hiddenCell = Assert.Single(result.Cells, cell => cell.Dx == -1 && cell.Dy == -1);
        Assert.Equal("hidden", hiddenCell.Visibility);
        Assert.Null(hiddenCell.TerrainClass);
        Assert.Null(hiddenCell.Walkable);
        Assert.Null(hiddenCell.FeatureClass);
        Assert.Null(hiddenCell.UnitCount);
    }

    [Fact]
    public async Task DfHackSurroundingsInspectionService_MapsProcessFailureToStableDataException()
    {
        var service = new DfHackSurroundingsInspectionService(
            new StubRunner(
                DfHackProcessCommandResult.Failure(
                    DfHackCommand.GetDwarfSurroundings,
                    DfHackProcessFailureCategory.Unavailable,
                    stdout: null,
                    stderr: null,
                    exitCode: null,
                    duration: TimeSpan.FromMilliseconds(10))),
            new DfHackProcessAdapterOptions
            {
                Enabled = true,
                RunPath = "C:\\dfhack\\hack\\dfhack-run.exe",
                WorkingDirectory = "C:\\dfhack\\hack",
                Host = "127.0.0.1"
            }.Validate());

        var exception = await Assert.ThrowsAsync<DwarfFortressDataException>(() =>
            service.InspectAroundAsync(DwarfId.Parse("6597"), 1, CancellationToken.None));

        Assert.Equal(DwarfFortressDataErrorCode.DfHackUnavailable, exception.ErrorCode);
    }

    private static string GetRetainedLiveSpatialSamplePath([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
    {
        var testDirectory = Path.GetDirectoryName(sourceFilePath)
            ?? throw new InvalidOperationException("Unable to determine the test source directory.");
        var repoRoot = Path.GetFullPath(Path.Combine(testDirectory, "..", "..", ".."));
        return Path.Combine(repoRoot, "dfhack", "samples", "research", "spatial-vision.live-2026-06-21.json");
    }

    private static string GetRetainedProductSpatialSamplePath([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
    {
        var testDirectory = Path.GetDirectoryName(sourceFilePath)
            ?? throw new InvalidOperationException("Unable to determine the test source directory.");
        var repoRoot = Path.GetFullPath(Path.Combine(testDirectory, "..", "..", ".."));
        return Path.Combine(repoRoot, "dfhack", "samples", "perception", "look-around.hidden.live-2026-06-23.json");
    }

    private static string CreateCurrentSceneJson()
    {
        var siteUnits = CreateRows(24, 12, ' ');
        siteUnits[6] = SetCharacter(siteUnits[6], 12, '@');
        var localUnits = CreateRows(33, 33, ' ');
        localUnits[16] = SetCharacter(localUnits[16], 16, '@');
        return JsonSerializer.Serialize(new
        {
            schemaVersion = "fortress-souls-dwarf-surroundings.v0.2.1",
            gameTime = new { year = 357, tick = 0 },
            observer = new
            {
                environment = "above_ground_outdoors",
                flags = new { outside = true, light = true, subterranean = false, hidden = false },
                terrain = new { shape = "floor", material = "grass" },
                structureClass = (string?)null
            },
            siteOverview = new
            {
                projection = "surface_overview",
                width = 24,
                height = 12,
                sampled = true,
                terrainRows = CreateRows(24, 12, '.'),
                featureRows = CreateRows(24, 12, ' '),
                materialRows = CreateRows(24, 12, 'g'),
                unitRows = siteUnits
            },
            localMap = new
            {
                projection = "current_level",
                width = 33,
                height = 33,
                sampled = false,
                terrainRows = CreateRows(33, 33, '.'),
                featureRows = CreateRows(33, 33, ' '),
                materialRows = CreateRows(33, 33, 'g'),
                unitRows = localUnits
            },
            details = new[]
            {
                new
                {
                    dx = 1, dy = 1, citizenCount = 0, otherUnitCount = 0, invaderCount = 0, dangerousUnitCount = 0,
                    items = new
                    {
                        objectCount = 2, stackQuantity = 3,
                        categories = new[] { new { category = "wood", objectCount = 2, stackQuantity = 3 } }
                    }
                }
            },
            warnings = Array.Empty<string>()
        });
    }

    private static string[] CreateRows(int width, int height, char value) =>
        Enumerable.Repeat(new string(value, width), height).ToArray();

    private static string SetCharacter(string value, int index, char character)
    {
        var chars = value.ToCharArray();
        chars[index] = character;
        return new string(chars);
    }

    private sealed class StubRunner(DfHackProcessCommandResult result) : IDfHackProcessRunner
    {
        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public Task<DfHackProcessCommandResult> RunCommandAsync(
            DfHackCommand command,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            Arguments = arguments.ToArray();
            return Task.FromResult(result);
        }
    }
}
