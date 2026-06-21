namespace FortressSouls.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FortressSouls.Application;
using FortressSouls.Domain;
using FortressSouls.Observability;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public sealed class DwarfApiTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ListEndpoint_ReturnsStableContractAndCorrelationHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dwarves");
        request.Headers.Add(FortressSoulsTelemetry.CorrelationHeaderName, "dwarves-123");

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(FortressSoulsTelemetry.CorrelationHeaderName, out var correlationValues));
        Assert.Equal("dwarves-123", Assert.Single(correlationValues));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        AssertExactPropertyNames(root, "items", "source");

        var items = root.GetProperty("items");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);

        Assert.Equal(3, items.GetArrayLength());
        AssertExactPropertyNames(items[0], "id", "displayName", "profession", "currentJob", "stressLevel");
        Assert.Equal("4101", items[0].GetProperty("id").GetString());
        Assert.Equal("Iden Torrentshade", items[0].GetProperty("displayName").GetString());
        Assert.Equal("Bookkeeper", items[2].GetProperty("profession").GetString());

        var source = root.GetProperty("source");
        AssertExactPropertyNames(source, "adapter", "snapshotTick", "schemaVersion");
        Assert.Equal("Fake", source.GetProperty("adapter").GetString());
        Assert.Equal(123456, source.GetProperty("snapshotTick").GetInt64());
        Assert.Equal("dwarf-list.v0.1", source.GetProperty("schemaVersion").GetString());
    }

    [Fact]
    public async Task ListEndpoint_ReturnsEmptyItemsWhenAdapterReturnsNoDwarves()
    {
        using var factory = CreateFactory(new EmptyDwarfFortressAdapter(), "Empty");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dwarves");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
        Assert.Equal("Empty", root.GetProperty("source").GetProperty("adapter").GetString());
    }

    [Fact]
    public async Task ListEndpoint_ReturnsSafeErrorForCancellation()
    {
        using var factory = CreateFactory(new CancellingDwarfFortressAdapter(), "Cancelling");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dwarves");

        Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("request_cancelled", error.ErrorCode);
        Assert.DoesNotContain("OperationCanceledException", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SnapshotEndpoint_ReturnsValidatedSnapshotContract()
    {
        var response = await _client!.GetAsync("/api/dwarves/4103/snapshot");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        AssertExactPropertyNames(
            root,
            "schemaVersion",
            "dwarfId",
            "extractedAt",
            "gameTick",
            "identity",
            "work",
            "skills",
            "personality",
            "needs",
            "relationships",
            "health",
            "debug");
        Assert.Equal("dwarf-snapshot.v0.1", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("4103", root.GetProperty("dwarfId").GetString());
        Assert.Equal("2026-06-18T00:00:00Z", root.GetProperty("extractedAt").GetString());
        Assert.Equal(123456, root.GetProperty("gameTick").GetInt64());

        var identity = root.GetProperty("identity");
        AssertExactPropertyNames(identity, "displayName", "profession");
        Assert.Equal("Domas Inkgranite", identity.GetProperty("displayName").GetString());
        Assert.Equal("Bookkeeper", identity.GetProperty("profession").GetString());

        var work = root.GetProperty("work");
        AssertExactPropertyNames(work, "currentJob", "labors");
        Assert.Equal("UpdateStockpileRecords", work.GetProperty("currentJob").GetString());
        Assert.Equal(0, work.GetProperty("labors").GetArrayLength());

        var skill = root.GetProperty("skills")[0];
        AssertExactPropertyNames(skill, "name", "level", "description");

        var personality = root.GetProperty("personality");
        AssertExactPropertyNames(personality, "traits", "values");
        Assert.True(personality.GetProperty("traits").EnumerateArray().Any());
        Assert.True(personality.GetProperty("values").EnumerateArray().Any());

        Assert.Equal(0, root.GetProperty("needs").GetArrayLength());
        Assert.Equal(0, root.GetProperty("relationships").GetArrayLength());
        Assert.Equal("No known injuries.", root.GetProperty("health").GetProperty("summary").GetString());

        var debug = root.GetProperty("debug");
        AssertExactPropertyNames(debug, "adapter", "rawAvailable");
        Assert.Equal("Fake", debug.GetProperty("adapter").GetString());
        Assert.False(debug.GetProperty("rawAvailable").GetBoolean());
    }

    [Fact]
    public async Task SnapshotEndpoint_ReturnsBadRequestForInvalidDwarfId()
    {
        var response = await _client!.GetAsync("/api/dwarves/not-a-dwarf/snapshot");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("invalid_dwarf_id", error.ErrorCode);
        Assert.DoesNotContain("not-a-dwarf", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SnapshotEndpoint_ReturnsNotFoundForUnknownDwarfId()
    {
        var response = await _client!.GetAsync("/api/dwarves/9999/snapshot");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("dwarf_not_found", error.ErrorCode);
        Assert.DoesNotContain("9999", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SnapshotEndpoint_PropagatesCancellation()
    {
        using var factory = CreateFactory(new BlockingDwarfFortressAdapter(), "Blocking");
        using var client = factory.CreateClient();
        using var cancellationTokenSource = new CancellationTokenSource();

        var requestTask = client.GetAsync("/api/dwarves/4101/snapshot", cancellationTokenSource.Token);
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => requestTask);
    }

    [Fact]
    public async Task SnapshotEndpoint_ReturnsSafeErrorWhenAdapterCancels()
    {
        using var factory = CreateFactory(new CancellingDwarfFortressAdapter(), "Cancelling");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dwarves/4101/snapshot");

        Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("request_cancelled", error.ErrorCode);
        Assert.DoesNotContain("OperationCanceledException", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListEndpoint_ReturnsSafeErrorForMalformedFakeConfiguration()
    {
        using var factory = CreateFactory(new MalformedFakeConfigurationAdapter(), "Fake");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dwarves");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("dwarf_configuration_invalid", error.ErrorCode);
        Assert.Equal("The dwarf data configuration is invalid.", error.Message);
        Assert.DoesNotContain("C:\\", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("samples\\snapshots", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DwarfEndpoints_EmitExpectedTelemetryWithoutDwarfNames()
    {
        var observedActivities = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FortressSoulsTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => observedActivities.Enqueue(activity),
        };

        ActivitySource.AddActivityListener(listener);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dwarves/4103/snapshot");
        request.Headers.Add(FortressSoulsTelemetry.CorrelationHeaderName, "trace-snapshot-123");

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var observedSnapshot = observedActivities.ToArray();

        var activity = Assert.Single(
            observedSnapshot,
            item => item.DisplayName == "fortresssouls.dwarves.snapshot"
                && Equals(item.GetTagItem(FortressSoulsTelemetry.CorrelationIdTagName), "trace-snapshot-123"));
        Assert.Equal("Fake", activity.GetTagItem(FortressSoulsTelemetry.AdapterTypeTagName));
        Assert.Equal("4103", activity.GetTagItem(FortressSoulsTelemetry.DwarfIdTagName));
        Assert.Equal(DwarfSchemaVersions.Snapshot, activity.GetTagItem(FortressSoulsTelemetry.SnapshotSchemaVersionTagName));
        Assert.Equal("success", activity.GetTagItem(FortressSoulsTelemetry.OperationOutcomeTagName));
        Assert.Equal("trace-snapshot-123", activity.GetTagItem(FortressSoulsTelemetry.CorrelationIdTagName));

        var tagValues = activity.Tags.Select(tag => tag.Value ?? string.Empty).ToArray();
        Assert.DoesNotContain("Domas Inkgranite", tagValues, StringComparer.Ordinal);
        Assert.DoesNotContain("Bookkeeper", tagValues, StringComparer.Ordinal);
    }

    [Fact]
    public async Task DwarfListEndpoint_EmitExpectedTelemetryWithoutRosterDetails()
    {
        var observedActivities = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FortressSoulsTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => observedActivities.Enqueue(activity),
        };

        ActivitySource.AddActivityListener(listener);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dwarves");
        request.Headers.Add(FortressSoulsTelemetry.CorrelationHeaderName, "trace-list-123");

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var observedSnapshot = observedActivities.ToArray();

        var activity = Assert.Single(
            observedSnapshot,
            item => item.DisplayName == "fortresssouls.dwarves.list"
                && Equals(item.GetTagItem(FortressSoulsTelemetry.CorrelationIdTagName), "trace-list-123"));
        Assert.Equal("Fake", activity.GetTagItem(FortressSoulsTelemetry.AdapterTypeTagName));
        Assert.Equal(DwarfSchemaVersions.List, activity.GetTagItem(FortressSoulsTelemetry.SnapshotSchemaVersionTagName));
        Assert.Equal("success", activity.GetTagItem(FortressSoulsTelemetry.OperationOutcomeTagName));
        Assert.Equal("trace-list-123", activity.GetTagItem(FortressSoulsTelemetry.CorrelationIdTagName));

        var tagValues = activity.Tags.Select(tag => tag.Value ?? string.Empty).ToArray();
        Assert.DoesNotContain("Iden Torrentshade", tagValues, StringComparer.Ordinal);
        Assert.DoesNotContain("Bookkeeper", tagValues, StringComparer.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactory(IDwarfFortressAdapter adapter, string adapterType) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IDwarfFortressAdapter>(adapter);
                    services.AddSingleton(new DwarfAdapterDescriptor(adapterType));
                });
            });

    private sealed record ApiErrorResponse(string ErrorCode, string Message);

    private sealed class EmptyDwarfFortressAdapter : IDwarfFortressAdapter
    {
        public Task<DwarfSnapshot> GetDwarfSnapshotAsync(DwarfId dwarfId, CancellationToken cancellationToken) =>
            throw new DwarfNotFoundException(dwarfId);

        public Task<DwarfListResult> ListDwarvesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(
                new DwarfListResult(
                    DwarfSchemaVersions.List,
                    new DwarfListSourceMetadata(true, true, true),
                    []));
    }

    private sealed class BlockingDwarfFortressAdapter : IDwarfFortressAdapter
    {
        public async Task<DwarfSnapshot> GetDwarfSnapshotAsync(DwarfId dwarfId, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new UnreachableException();
        }

        public async Task<DwarfListResult> ListDwarvesAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new UnreachableException();
        }
    }

    private sealed class CancellingDwarfFortressAdapter : IDwarfFortressAdapter
    {
        public Task<DwarfSnapshot> GetDwarfSnapshotAsync(DwarfId dwarfId, CancellationToken cancellationToken) =>
            Task.FromCanceled<DwarfSnapshot>(new CancellationToken(canceled: true));

        public Task<DwarfListResult> ListDwarvesAsync(CancellationToken cancellationToken) =>
            Task.FromCanceled<DwarfListResult>(new CancellationToken(canceled: true));
    }

    private sealed class MalformedFakeConfigurationAdapter : IDwarfFortressAdapter
    {
        public Task<DwarfSnapshot> GetDwarfSnapshotAsync(DwarfId dwarfId, CancellationToken cancellationToken) =>
            throw new DwarfFortressDataException(
                DwarfFortressDataErrorCode.InvalidConfiguration,
                "Malformed fake configuration at C:\\repo\\samples\\snapshots\\fake-dwarves-list.v0.1.json");

        public Task<DwarfListResult> ListDwarvesAsync(CancellationToken cancellationToken) =>
            throw new DwarfFortressDataException(
                DwarfFortressDataErrorCode.InvalidConfiguration,
                "Malformed fake configuration at C:\\repo\\samples\\snapshots\\fake-dwarves-list.v0.1.json");
    }

    private static void AssertExactPropertyNames(JsonElement element, params string[] expected)
    {
        var actual = element.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(expected, actual);
    }
}
