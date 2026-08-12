using Deyxis.Core.Events;

namespace Deyxis.Providers.Agents;

public sealed class CodexProvider : AgentProviderBase
{
    private readonly IExecutableProbe executableProbe;
    private readonly string executablePath;

    public CodexProvider(
        IEventBus eventBus,
        IExecutableProbe executableProbe,
        string executablePath)
        : base(eventBus, AgentProviderKind.Codex)
    {
        this.executableProbe = executableProbe ?? throw new ArgumentNullException(nameof(executableProbe));
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new ArgumentException("Executable paths must be fully qualified.", nameof(executablePath));
        }

        this.executablePath = Path.GetFullPath(executablePath);
    }

    public async Task ProbeAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ExecutableProbeResult result;
        try
        {
            result = await executableProbe.ProbeAsync(executablePath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result = ExecutableProbeResult.Failure(ExecutableProbeFailureCategory.Cancelled);
        }
        catch (Exception)
        {
            result = ExecutableProbeResult.Failure(ExecutableProbeFailureCategory.StartFailed);
        }

        if (result.Succeeded)
        {
            Publish(AgentActivityState.Stopped, "Codex");
            return;
        }

        Publish(AgentActivityState.Failed, "Codex", failureCategory: result.FailureCategory);
    }
}
