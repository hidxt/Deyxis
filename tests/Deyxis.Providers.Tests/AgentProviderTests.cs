using Deyxis.Core.Activities;
using Deyxis.Core.Events;
using Deyxis.PluginSdk;
using Deyxis.Providers.Agents;
using Xunit;

namespace Deyxis.Providers.Tests;

public sealed class AgentProviderTests
{
    [Fact]
    public void Codex_rejects_a_relative_configured_executable_path()
    {
        Assert.Throws<ArgumentException>(() =>
            new CodexProvider(new EventBus(), new FixedProbe(ExecutableProbeResult.Success("1.0")), "codex.exe"));
    }

    [Fact]
    public async Task Unavailable_codex_probe_publishes_a_failed_activity_without_diagnostic_content()
    {
        const string secret = "api-key=do-not-publish";
        var executablePath = Path.GetFullPath("codex.exe");
        var bus = new EventBus();
        ActivityUpserted? published = null;
        using var subscription = bus.Subscribe<ActivityUpserted>(message => published = message);
        using var provider = new CodexProvider(
            bus,
            new ThrowingProbe(new InvalidOperationException(secret)),
            executablePath);

        await provider.ProbeAvailabilityAsync();

        Assert.Equal(ProviderHealth.Failed, provider.Health);
        Assert.NotNull(published);
        Assert.Equal(ActivityState.Failed, published.Activity.State);
        Assert.DoesNotContain(secret, published.Activity.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(executablePath, published.Activity.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Successful_codex_probe_publishes_stopped_without_retaining_version_output()
    {
        const string sensitiveVersionOutput = "codex 1.0 api-key=do-not-publish";
        var bus = new EventBus();
        ActivityUpserted? published = null;
        using var subscription = bus.Subscribe<ActivityUpserted>(message => published = message);
        using var provider = new CodexProvider(
            bus,
            new FixedProbe(ExecutableProbeResult.Success(sensitiveVersionOutput)),
            Path.GetFullPath("codex.exe"));

        await provider.ProbeAvailabilityAsync();

        Assert.Equal(ProviderHealth.Stopped, provider.Health);
        Assert.NotNull(published);
        Assert.Equal(ActivityCategory.Agent, published.Activity.Category);
        Assert.Equal(ActivityState.Idle, published.Activity.State);
        Assert.DoesNotContain(sensitiveVersionOutput, published.Activity.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelled_codex_probe_publishes_only_the_cancelled_category()
    {
        var bus = new EventBus();
        ActivityUpserted? published = null;
        using var subscription = bus.Subscribe<ActivityUpserted>(message => published = message);
        using var cancellation = new CancellationTokenSource();
        using var provider = new CodexProvider(
            bus,
            new CancellingProbe(),
            Path.GetFullPath("codex.exe"));
        cancellation.Cancel();

        await provider.ProbeAvailabilityAsync(cancellation.Token);

        Assert.Equal(ActivityState.Failed, published?.Activity.State);
        Assert.Equal("Availability probe cancelled", published?.Activity.Description);
    }

    [Fact]
    public void Starting_codex_publishes_stopped_without_probing_the_executable()
    {
        var probe = new CountingProbe();
        var bus = new EventBus();
        ActivityUpserted? published = null;
        using var subscription = bus.Subscribe<ActivityUpserted>(message => published = message);
        using var provider = new CodexProvider(bus, probe, Path.GetFullPath("codex.exe"));

        provider.Start();

        Assert.Equal(0, probe.InvocationCount);
        Assert.Equal(ActivityState.Idle, published?.Activity.State);
        Assert.Equal(ProviderHealth.Stopped, provider.Health);
    }

    [Fact]
    public void Claude_and_opencode_publish_stopped_explicit_boundaries()
    {
        var bus = new EventBus();
        var published = new List<ActivityUpserted>();
        using var subscription = bus.Subscribe<ActivityUpserted>(published.Add);
        using var claude = new ClaudeCodeProvider(bus);
        using var openCode = new OpenCodeProvider(bus);

        claude.Start();
        openCode.Start();

        Assert.Collection(
            published,
            item => AssertStopped(item.Activity, "agent.claude-code", "Claude Code"),
            item => AssertStopped(item.Activity, "agent.opencode", "OpenCode"));
        Assert.Equal(ProviderHealth.Stopped, claude.Health);
        Assert.Equal(ProviderHealth.Stopped, openCode.Health);
    }

    [Fact]
    public void Allowed_running_state_maps_to_a_running_agent_activity()
    {
        var bus = new EventBus();
        ActivityUpserted? published = null;
        using var subscription = bus.Subscribe<ActivityUpserted>(message => published = message);
        using var provider = new TestAgentProvider(bus);

        provider.ReportRunning("Explicit run", 0.5);

        Assert.Equal(ProviderHealth.Running, provider.Health);
        Assert.Equal(ActivityState.Running, published?.Activity.State);
        Assert.Equal("Running", published?.Activity.Description);
        Assert.Equal(0.5, published?.Activity.Progress);
    }

    [Fact]
    public void Providers_explicitly_report_live_session_observation_as_unsupported()
    {
        var bus = new EventBus();
        using var codex = new CodexProvider(bus, new CountingProbe(), Path.GetFullPath("codex.exe"));
        using var claude = new ClaudeCodeProvider(bus);
        using var openCode = new OpenCodeProvider(bus);

        Assert.False(codex.SupportsLiveSessionObservation);
        Assert.False(claude.SupportsLiveSessionObservation);
        Assert.False(openCode.SupportsLiveSessionObservation);
    }

    [Fact]
    public void Disabled_composition_constructs_stopped_adapters_without_probing_or_publishing()
    {
        var bus = new EventBus();
        var publicationCount = 0;
        using var subscription = bus.Subscribe<ActivityUpserted>(_ => publicationCount++);
        var probe = new ThrowingProbe(new InvalidOperationException("A disabled adapter must not probe."));

        using var composition = AgentProviderComposition.CreateDisabled(
            bus,
            probe,
            Path.GetFullPath("codex.exe"));

        Assert.Collection(
            composition.Providers,
            provider => AssertStoppedProvider(provider, "agent.codex"),
            provider => AssertStoppedProvider(provider, "agent.claude-code"),
            provider => AssertStoppedProvider(provider, "agent.opencode"));
        Assert.Equal(0, publicationCount);
    }

    [Fact]
    public void Disposed_provider_rejects_lifecycle_calls_without_publishing()
    {
        var bus = new EventBus();
        var publicationCount = 0;
        using var subscription = bus.Subscribe<ActivityUpserted>(_ => publicationCount++);
        var provider = new ClaudeCodeProvider(bus);
        provider.Dispose();

        Assert.Throws<ObjectDisposedException>(provider.Start);
        Assert.Throws<ObjectDisposedException>(provider.Stop);
        Assert.Equal(0, publicationCount);
    }

    [Fact]
    public async Task Disposed_codex_provider_rejects_probe_without_calling_it()
    {
        var probe = new CountingProbe();
        var provider = new CodexProvider(new EventBus(), probe, Path.GetFullPath("codex.exe"));
        provider.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.ProbeAvailabilityAsync());
        Assert.Equal(0, probe.InvocationCount);
    }

    private static void AssertStopped(Activity activity, string providerId, string title)
    {
        Assert.Equal(providerId, activity.ProviderId);
        Assert.Equal(ActivityCategory.Agent, activity.Category);
        Assert.Equal(ActivityState.Idle, activity.State);
        Assert.Equal(title, activity.Title);
        Assert.Equal("Stopped", activity.Description);
    }

    private static void AssertStoppedProvider(AgentProviderBase provider, string providerId)
    {
        Assert.Equal(providerId, provider.Id);
        Assert.Equal(ProviderHealth.Stopped, provider.Health);
    }

    private sealed class ThrowingProbe(Exception exception) : IExecutableProbe
    {
        public Task<ExecutableProbeResult> ProbeAsync(
            string executablePath,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ExecutableProbeResult>(exception);
    }

    private sealed class FixedProbe(ExecutableProbeResult result) : IExecutableProbe
    {
        public Task<ExecutableProbeResult> ProbeAsync(
            string executablePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class CountingProbe : IExecutableProbe
    {
        public int InvocationCount { get; private set; }

        public Task<ExecutableProbeResult> ProbeAsync(
            string executablePath,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult(ExecutableProbeResult.Success("1.0"));
        }
    }

    private sealed class CancellingProbe : IExecutableProbe
    {
        public Task<ExecutableProbeResult> ProbeAsync(
            string executablePath,
            CancellationToken cancellationToken = default) =>
            Task.FromCanceled<ExecutableProbeResult>(cancellationToken);
    }

    private sealed class TestAgentProvider(IEventBus eventBus)
        : AgentProviderBase(eventBus, AgentProviderKind.Codex)
    {
        public void ReportRunning(string label, double? progress) =>
            Publish(AgentActivityState.Running, label, progress);
    }
}
