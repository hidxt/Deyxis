using Xunit;
using Deyxis.Core.Activities;

namespace Deyxis.Core.Tests;

public sealed class ActivityTests
{
    [Fact]
    public void Activity_preserves_the_supplied_progress_value()
    {
        var activity = TestActivity.Create(progress: 0.73);

        Assert.Equal(0.73, activity.Progress);
    }
}

internal static class TestActivity
{
    public static Activity Create(double? progress = null) => new(
        Guid.NewGuid(),
        "test-provider",
        default,
        ActivityState.Idle,
        0,
        "Test activity",
        "Test description",
        progress,
        DateTimeOffset.UnixEpoch);
}
