using Deyxis.Core.Events;

namespace Deyxis.Providers.Agents;

public sealed class AgentProviderComposition : IDisposable
{
    private readonly AgentProviderBase[] providers;

    private AgentProviderComposition(AgentProviderBase[] providers)
    {
        this.providers = providers;
        Providers = Array.AsReadOnly(providers);
    }

    public IReadOnlyList<AgentProviderBase> Providers { get; }

    public static AgentProviderComposition CreateDisabled(IEventBus eventBus)
    {
        ArgumentNullException.ThrowIfNull(eventBus);

        return CreateDisabled(
            eventBus,
            DisabledExecutableProbe.Instance,
            Path.Combine(AppContext.BaseDirectory, "codex.exe"));
    }

    internal static AgentProviderComposition CreateDisabled(
        IEventBus eventBus,
        IExecutableProbe executableProbe,
        string codexExecutablePath)
    {
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(executableProbe);

        return new AgentProviderComposition(
        [
            new CodexProvider(eventBus, executableProbe, codexExecutablePath),
            new ClaudeCodeProvider(eventBus),
            new OpenCodeProvider(eventBus),
        ]);
    }

    public void Dispose()
    {
        foreach (var provider in providers)
        {
            provider.Dispose();
        }
    }

    private sealed class DisabledExecutableProbe : IExecutableProbe
    {
        public static DisabledExecutableProbe Instance { get; } = new();

        public Task<ExecutableProbeResult> ProbeAsync(
            string executablePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ExecutableProbeResult.Failure(ExecutableProbeFailureCategory.PathNotAllowed));
    }
}
