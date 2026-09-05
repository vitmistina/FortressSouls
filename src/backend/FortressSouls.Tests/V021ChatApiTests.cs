namespace FortressSouls.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FortressSouls.Api;
using FortressSouls.Application;
using FortressSouls.DwarfFortress;
using FortressSouls.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class V021ChatApiTests
{
    [Fact]
    public async Task NormalTurn_PreloadsScene_UsesThreeOptionalTools_AndReturnsSeparateReceipt()
    {
        var agent = new CapturingAgent();
        using var factory = CreateFactory(agent);
        using var client = factory.CreateClient();

        var created = await (await client.PostAsJsonAsync(
                "/api/chat/sessions",
                new CreateChatSessionRequest("4101")))
            .Content.ReadFromJsonAsync<CreateChatSessionResponse>();
        Assert.NotNull(created);

        var response = await client.PostAsJsonAsync(
            $"/api/chat/sessions/{created!.SessionId}/messages",
            new SendChatMessageRequest("What is around us?"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var root = body.RootElement;
        var observation = Assert.Single(root.GetProperty("observationReceipts").EnumerateArray());
        Assert.Equal("current_scene", observation.GetProperty("capability").GetString());
        Assert.Equal("success", observation.GetProperty("outcome").GetString());
        Assert.Empty(root.GetProperty("toolReceipts").EnumerateArray());
        Assert.NotNull(agent.InitialPromptText);
        Assert.Contains("CURRENT_PERCEPTION format=FSMP/1", agent.InitialPromptText, StringComparison.Ordinal);
        Assert.DoesNotContain("CURRENT_PERCEPTION:\noutcome=success", agent.InitialPromptText, StringComparison.Ordinal);
        Assert.Equal(
            ["inspect_stocks", "list_dwarves", "inspect_dwarf"],
            agent.EnabledToolNames);

        var preview = await (await client.GetAsync($"/api/chat/sessions/{created.SessionId}/prompt-preview"))
            .Content.ReadFromJsonAsync<PromptPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Contains("content=[redacted]", preview!.PromptText, StringComparison.Ordinal);
        Assert.DoesNotContain("[LOCAL projection=", preview.PromptText, StringComparison.Ordinal);
        Assert.DoesNotContain("[SITE projection=", preview.PromptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnavailableScene_ContinuesWithUnavailableMarkerAndReceipt()
    {
        var agent = new CapturingAgent();
        using var factory = CreateFactory(agent, new UnavailableSurroundingsInspectionService());
        using var client = factory.CreateClient();

        var created = await (await client.PostAsJsonAsync(
                "/api/chat/sessions",
                new CreateChatSessionRequest("4101")))
            .Content.ReadFromJsonAsync<CreateChatSessionResponse>();
        Assert.NotNull(created);

        var response = await client.PostAsJsonAsync(
            $"/api/chat/sessions/{created!.SessionId}/messages",
            new SendChatMessageRequest("Tell me something about yourself."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var receipt = Assert.Single(body.RootElement.GetProperty("observationReceipts").EnumerateArray());
        Assert.Equal("unavailable", receipt.GetProperty("outcome").GetString());
        Assert.Contains("CURRENT_PERCEPTION unavailable", agent.InitialPromptText, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        CapturingAgent agent,
        ISurroundingsInspectionService? sceneService = null) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IDwarfAgent>();
                    services.AddSingleton<IDwarfAgent>(agent);
                    if (sceneService is not null)
                    {
                        services.RemoveAll<ISurroundingsInspectionService>();
                        services.AddSingleton(sceneService);
                    }
                });
            });

    private sealed class CapturingAgent : IDwarfAgent
    {
        public string? InitialPromptText { get; private set; }

        public IReadOnlyList<string>? EnabledToolNames { get; private set; }

        public Task<AgentTurnResult> RunTurnAsync(AgentTurnRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InitialPromptText = request.InitialPromptText;
            EnabledToolNames = request.EnabledToolNames;
            return Task.FromResult(new AgentTurnResult("I am here.", "Fake", "test", []));
        }
    }
}
