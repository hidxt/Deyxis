namespace Deyxis.Providers.Agents;

public enum AgentActivityState
{
    Stopped,
    Running,
    Failed,
}

public sealed record AgentActivitySnapshot
{
    public AgentActivitySnapshot(
        AgentProviderKind provider,
        AgentActivityState state,
        string label,
        double? progress = null,
        ExecutableProbeFailureCategory failureCategory = ExecutableProbeFailureCategory.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        if (progress is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(progress));
        }

        Provider = provider;
        State = state;
        Label = label;
        Progress = progress;
        FailureCategory = failureCategory;
    }

    public AgentProviderKind Provider { get; }

    public AgentActivityState State { get; }

    public string Label { get; }

    public double? Progress { get; }

    public ExecutableProbeFailureCategory FailureCategory { get; }
}
