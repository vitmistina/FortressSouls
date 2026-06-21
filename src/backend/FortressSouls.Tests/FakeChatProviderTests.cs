namespace FortressSouls.Tests;

using System.Diagnostics;
using FortressSouls.Application;
using FortressSouls.Llm;
using FortressSouls.Observability;

public sealed class FakeChatProviderTests
{
    [Fact]
    public async Task SendAsync_IsDeterministic_ForIdenticalPrompt()
    {
        var provider = new FakeChatProvider();
        var request = new ChatProviderRequest("prompt-text", 200);

        var first = await provider.SendAsync(request, CancellationToken.None);
        var second = await provider.SendAsync(request, CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Equal("Fake", first.ProviderType);
        Assert.Equal("fake-dwarf", first.Model);
    }

    [Fact]
    public async Task SendAsync_HonorsResponseLengthBound()
    {
        var provider = new FakeChatProvider();

        var response = await provider.SendAsync(new ChatProviderRequest("prompt-text", 12), CancellationToken.None);

        Assert.Equal(12, response.MessageText.Length);
    }

    [Fact]
    public async Task SendAsync_ThrowsWhenCancelled()
    {
        var provider = new FakeChatProvider();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.SendAsync(new ChatProviderRequest("prompt-text", 200), cancellationTokenSource.Token));
    }

    [Fact]
    public async Task SendAsync_EmitsLlmSpanWithoutPromptContent()
    {
        var observed = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FortressSoulsTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => observed.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        var provider = new FakeChatProvider();
        var response = await provider.SendAsync(new ChatProviderRequest("SENTINEL-PROMPT-TEXT", 200), CancellationToken.None);

        Assert.NotNull(response);
        var llmActivity = Assert.Single(observed, activity => activity.DisplayName == FortressSoulsTelemetry.LlmChatActivityName);
        Assert.Equal("Fake", llmActivity.GetTagItem(FortressSoulsTelemetry.ProviderTypeTagName));
        Assert.Equal("fake-dwarf", llmActivity.GetTagItem(FortressSoulsTelemetry.LlmModelTagName));
        Assert.Equal("success", llmActivity.GetTagItem(FortressSoulsTelemetry.OperationOutcomeTagName));
        Assert.DoesNotContain(llmActivity.Tags, tag =>
            (tag.Value?.ToString() ?? string.Empty).Contains("SENTINEL-PROMPT-TEXT", StringComparison.Ordinal));
    }
}
