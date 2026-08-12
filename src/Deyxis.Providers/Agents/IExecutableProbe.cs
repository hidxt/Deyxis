namespace Deyxis.Providers.Agents;

public interface IExecutableProbe
{
    Task<ExecutableProbeResult> ProbeAsync(
        string executablePath,
        CancellationToken cancellationToken = default);
}

public enum ExecutableProbeFailureCategory
{
    None,
    PathNotAllowed,
    NotFound,
    NonZeroExit,
    TimedOut,
    Cancelled,
    StartFailed,
}

public sealed record ExecutableProbeResult(
    string? Version,
    ExecutableProbeFailureCategory FailureCategory)
{
    public bool Succeeded => FailureCategory == ExecutableProbeFailureCategory.None;

    public static ExecutableProbeResult Success(string version) =>
        new(version, ExecutableProbeFailureCategory.None);

    public static ExecutableProbeResult Failure(ExecutableProbeFailureCategory failureCategory) =>
        new(null, failureCategory);
}
