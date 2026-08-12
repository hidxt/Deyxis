using System.Diagnostics;
using Deyxis.Providers.Agents;
using Xunit;

namespace Deyxis.Providers.Tests;

public sealed class ExecutableProbeTests
{
    [Fact]
    public async Task Path_outside_the_explicit_allowlist_is_rejected_without_starting_a_process()
    {
        var wasStarted = false;
        var allowedPath = Path.GetFullPath("allowed-tool.exe");
        var rejectedPath = Path.GetFullPath("rejected-tool.exe");
        var probe = new ExecutableProbe(
            new[] { allowedPath },
            (_, _) =>
            {
                wasStarted = true;
                return Task.FromResult(ExecutableProbeExecution.Succeeded("1.2.3"));
            });

        var result = await probe.ProbeAsync(rejectedPath);

        Assert.False(wasStarted);
        Assert.False(result.Succeeded);
        Assert.Equal(ExecutableProbeFailureCategory.PathNotAllowed, result.FailureCategory);
        Assert.Null(result.Version);
    }

    [Fact]
    public async Task Successful_probe_retains_only_the_trimmed_version()
    {
        var executablePath = Path.GetFullPath("allowed-tool.exe");
        var probe = new ExecutableProbe(
            new[] { executablePath },
            (_, _) => Task.FromResult(ExecutableProbeExecution.Succeeded("  1.2.3  \r\nadditional output")));

        var result = await probe.ProbeAsync(executablePath);

        Assert.True(result.Succeeded);
        Assert.Equal("1.2.3", result.Version);
        Assert.Equal(ExecutableProbeFailureCategory.None, result.FailureCategory);
    }

    [Fact]
    public async Task Nonzero_exit_is_reported_without_command_output()
    {
        var executablePath = Path.GetFullPath("allowed-tool.exe");
        var probe = new ExecutableProbe(
            new[] { executablePath },
            (_, _) => Task.FromResult(ExecutableProbeExecution.Failed(17)));

        var result = await probe.ProbeAsync(executablePath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Version);
        Assert.Equal(ExecutableProbeFailureCategory.NonZeroExit, result.FailureCategory);
    }

    [Fact]
    public async Task Timed_out_execution_returns_a_timeout_category()
    {
        var executablePath = Path.GetFullPath("allowed-tool.exe");
        var probe = new ExecutableProbe(
            new[] { executablePath },
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return ExecutableProbeExecution.Succeeded("never");
            },
            TimeSpan.FromMilliseconds(20));

        var result = await probe.ProbeAsync(executablePath);

        Assert.False(result.Succeeded);
        Assert.Equal(ExecutableProbeFailureCategory.TimedOut, result.FailureCategory);
    }

    [Fact]
    public async Task Caller_cancellation_returns_a_cancelled_category()
    {
        var executablePath = Path.GetFullPath("allowed-tool.exe");
        var probe = new ExecutableProbe(
            new[] { executablePath },
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return ExecutableProbeExecution.Succeeded("never");
            });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await probe.ProbeAsync(executablePath, cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.Equal(ExecutableProbeFailureCategory.Cancelled, result.FailureCategory);
    }
}
