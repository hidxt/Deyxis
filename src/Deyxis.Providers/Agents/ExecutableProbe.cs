using System.Diagnostics;

namespace Deyxis.Providers.Agents;

public sealed class ExecutableProbe : IExecutableProbe
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    private readonly HashSet<string> allowedExecutablePaths;
    private readonly Func<string, CancellationToken, Task<ExecutableProbeExecution>> executeAsync;
    private readonly TimeSpan timeout;

    public ExecutableProbe(IEnumerable<string> allowedExecutablePaths, TimeSpan? timeout = null)
        : this(allowedExecutablePaths, ExecuteVersionAsync, timeout)
    {
    }

    internal ExecutableProbe(
        IEnumerable<string> allowedExecutablePaths,
        Func<string, CancellationToken, Task<ExecutableProbeExecution>> executeAsync,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(allowedExecutablePaths);
        this.executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        this.timeout = timeout ?? DefaultTimeout;
        if (this.timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        this.allowedExecutablePaths = new HashSet<string>(
            allowedExecutablePaths.Select(NormalizePath),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ExecutableProbeResult> ProbeAsync(
        string executablePath,
        CancellationToken cancellationToken = default)
    {
        string normalizedPath;
        try
        {
            normalizedPath = NormalizePath(executablePath);
        }
        catch (ArgumentException)
        {
            return ExecutableProbeResult.Failure(ExecutableProbeFailureCategory.PathNotAllowed);
        }

        if (!allowedExecutablePaths.Contains(normalizedPath))
        {
            return ExecutableProbeResult.Failure(ExecutableProbeFailureCategory.PathNotAllowed);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ExecutableProbeResult.Failure(ExecutableProbeFailureCategory.Cancelled);
        }

        using var timeoutCancellation = new CancellationTokenSource(timeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellation.Token);

        try
        {
            var execution = await executeAsync(normalizedPath, linkedCancellation.Token).ConfigureAwait(false);
            return execution.ExitCode == 0
                ? ExecutableProbeResult.Success(FirstLine(execution.VersionOutput))
                : ExecutableProbeResult.Failure(ExecutableProbeFailureCategory.NonZeroExit);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ExecutableProbeResult.Failure(ExecutableProbeFailureCategory.Cancelled);
        }
        catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
        {
            return ExecutableProbeResult.Failure(ExecutableProbeFailureCategory.TimedOut);
        }
        catch (FileNotFoundException)
        {
            return ExecutableProbeResult.Failure(ExecutableProbeFailureCategory.NotFound);
        }
        catch (Exception)
        {
            return ExecutableProbeResult.Failure(ExecutableProbeFailureCategory.StartFailed);
        }
    }

    private static async Task<ExecutableProbeExecution> ExecuteVersionAsync(
        string executablePath,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add("--version");
        process.Start();
        var version = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new ExecutableProbeExecution(process.ExitCode, version);
    }

    private static string NormalizePath(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new ArgumentException("Executable paths must be fully qualified.", nameof(executablePath));
        }

        return Path.GetFullPath(executablePath);
    }

    private static string FirstLine(string output) =>
        (output.Split(['\r', '\n'], 2)[0]).Trim();
}

internal sealed record ExecutableProbeExecution(int ExitCode, string VersionOutput)
{
    public static ExecutableProbeExecution Succeeded(string versionOutput) => new(0, versionOutput);

    public static ExecutableProbeExecution Failed(int exitCode) => new(exitCode, string.Empty);
}
