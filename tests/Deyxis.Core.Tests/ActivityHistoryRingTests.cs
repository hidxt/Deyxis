using System.Text.Json;
using Deyxis.Core.Activities;
using Deyxis.Core.History;
using Xunit;

namespace Deyxis.Core.Tests;

public sealed class ActivityHistoryRingTests
{
    [Fact]
    public void Add_retains_only_the_twenty_newest_summaries()
    {
        var history = new ActivityHistoryRing();

        for (var index = 0; index < 25; index++)
        {
            history.Add(CreateActivity(index));
        }

        Assert.Equal(20, history.Entries.Count);
        Assert.Equal("Activity 24", history.Entries[0].Title);
        Assert.Equal("Activity 5", history.Entries[^1].Title);
    }

    [Fact]
    public void Add_projects_only_the_approved_sanitized_fields()
    {
        var activity = new Activity(
            Guid.NewGuid(),
            "agent.codex",
            ActivityCategory.Agent,
            ActivityState.Running,
            Priority: 999,
            "Safe title",
            "secret prompt, output, credential, and C:\\private\\drop.png",
            Progress: 0.75,
            DateTimeOffset.UnixEpoch);
        var history = new ActivityHistoryRing();

        history.Add(activity);

        var summary = Assert.Single(history.Entries);
        Assert.Equal("agent.codex", summary.ProviderId);
        Assert.Equal(ActivityCategory.Agent, summary.Category);
        Assert.Equal(ActivityState.Running, summary.State);
        Assert.Equal("Safe title", summary.Title);
        Assert.Equal(DateTimeOffset.UnixEpoch, summary.Timestamp);
        var json = JsonSerializer.Serialize(summary);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Progress", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Priority", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Clear_removes_all_summaries()
    {
        var history = new ActivityHistoryRing();
        history.Add(CreateActivity(1));

        history.Clear();

        Assert.Empty(history.Entries);
    }

    [Fact]
    public void Initialization_bounds_already_sanitized_summaries()
    {
        var summaries = Enumerable.Range(0, 25)
            .Select(index => ActivityHistorySummary.FromActivity(CreateActivity(index)));

        var history = new ActivityHistoryRing(summaries);

        Assert.Equal(20, history.Entries.Count);
        Assert.Equal("Activity 0", history.Entries[0].Title);
        Assert.Equal("Activity 19", history.Entries[^1].Title);
    }

    private static Activity CreateActivity(int index) => new(
        Guid.NewGuid(),
        "test-provider",
        ActivityCategory.Media,
        ActivityState.Completed,
        Priority: 0,
        $"Activity {index}",
        "Not retained",
        Progress: null,
        DateTimeOffset.UnixEpoch.AddSeconds(index));
}
