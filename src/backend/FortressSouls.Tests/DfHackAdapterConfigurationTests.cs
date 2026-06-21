namespace FortressSouls.Tests;

using System.Net;
using System.Net.Http.Json;
using FortressSouls.Api;
using FortressSouls.Application;
using FortressSouls.Domain;
using FortressSouls.DwarfFortress;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[Collection("DfHackProcessSerial")]
public sealed class DfHackAdapterConfigurationTests
{
    [Fact]
    public void DfHackOptions_RejectsNonLoopbackAndInvalidTimeouts()
    {
        var invalidHost = new DfHackProcessAdapterOptions
        {
            Enabled = true,
            RunPath = "C:\\dfhack\\hack\\dfhack-run.exe",
            WorkingDirectory = "C:\\dfhack\\hack",
            Host = "10.0.0.5"
        };
        Assert.Throws<ArgumentException>(() => invalidHost.Validate());

        var invalidTimeout = new DfHackProcessAdapterOptions
        {
            Enabled = true,
            RunPath = "C:\\dfhack\\hack\\dfhack-run.exe",
            WorkingDirectory = "C:\\dfhack\\hack",
            Host = "127.0.0.1",
            TimeoutMs = 100
        };
        Assert.Throws<ArgumentException>(() => invalidTimeout.Validate());
    }

    [Fact]
    public async Task DfHackStatusEndpoint_ReadsProjectionWithoutProcessOrNetworkCalls()
    {
        var countingRunner = new CountingRunner();
        var countingPreflight = new CountingPreflight();
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IDwarfFortressAdapter>();
            services.RemoveAll<IDfHackProcessRunner>();
            services.RemoveAll<IDfHackTcpPreflight>();
            services.RemoveAll<IDfHackAdapterStatusRecorder>();
            services.RemoveAll<IDwarfAdapterStatusReader>();
            services.RemoveAll<DwarfAdapterDescriptor>();

            var statusTracker = new DfHackAdapterStatusTracker(enabled: true);
            services.AddSingleton<IDfHackProcessRunner>(countingRunner);
            services.AddSingleton<IDfHackTcpPreflight>(countingPreflight);
            services.AddSingleton(statusTracker);
            services.AddSingleton<IDfHackAdapterStatusRecorder>(statusTracker);
            services.AddSingleton<IDwarfAdapterStatusReader>(statusTracker);
            services.AddSingleton<IDwarfFortressAdapter, DfHackDwarfFortressAdapter>();
            services.AddSingleton(new DwarfAdapterDescriptor("DfHackProcess"));
            services.AddSingleton(new DfHackProcessAdapterOptions
            {
                Enabled = true,
                RunPath = "C:\\dfhack\\hack\\dfhack-run.exe",
                WorkingDirectory = "C:\\dfhack\\hack",
                Host = "127.0.0.1"
            }.Validate());
        });

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/dwarves/adapter-status");
        var status = await response.Content.ReadFromJsonAsync<DwarfAdapterStatusResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(status);
        Assert.Equal("DfHackProcess", status!.AdapterType);
        Assert.Equal(0, countingRunner.InvocationCount);
        Assert.Equal(0, countingPreflight.InvocationCount);
    }

    private static WebApplicationFactory<Program> CreateFactory(Action<IServiceCollection> configureServices) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(configureServices);
            });

    private sealed class CountingPreflight : IDfHackTcpPreflight
    {
        public int InvocationCount { get; private set; }

        public Task<bool> IsReachableAsync(CancellationToken cancellationToken)
        {
            InvocationCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class CountingRunner : IDfHackProcessRunner
    {
        public int InvocationCount { get; private set; }

        public Task<DfHackProcessCommandResult> RunCommandAsync(
            DfHackCommand command,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            return Task.FromResult(
                DfHackProcessCommandResult.Success(
                    command,
                    """{"schemaVersion":"fortress-souls-dwarf-list.v0.1","worldLoaded":true,"siteLoaded":true,"mapLoaded":true,"count":0,"items":[]}""",
                    string.Empty,
                    0,
                    TimeSpan.Zero));
        }
    }
}
