using Deyxis.Core.Activities;
using Deyxis.Core.History;
using Deyxis.UI.History;
using Xunit;

namespace Deyxis.UI.Tests;

public sealed class ActivityHistoryViewModelTests
{
    [Fact]
    public void Refresh_maps_only_safe_summary_fields_to_compact_rows()
    {
        var summary = new ActivityHistorySummary(
            "agent.codex",
            ActivityCategory.Agent,
            ActivityState.Completed,
            "Finished task",
            DateTimeOffset.Parse("2026-08-12T04:30:00+00:00"));
        var viewModel = new ActivityHistoryViewModel();

        viewModel.Refresh([summary]);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("agent.codex", row.ProviderId);
        Assert.Equal("Agent", row.Category);
        Assert.Equal("Completed", row.State);
        Assert.Equal("Finished task", row.Title);
        Assert.Equal("2026-08-12 04:30 UTC", row.Timestamp);
        Assert.Equal(
            ["Category", "ProviderId", "State", "Timestamp", "Title"],
            row.GetType().GetProperties().Select(property => property.Name).Order().ToArray());
    }

    [Fact]
    public void Clear_is_an_explicit_request_and_removes_visible_rows()
    {
        var viewModel = new ActivityHistoryViewModel();
        viewModel.Refresh([
            new ActivityHistorySummary(
                "media",
                ActivityCategory.Media,
                ActivityState.Running,
                "Playing",
                DateTimeOffset.UnixEpoch),
        ]);
        var clearRequests = 0;
        viewModel.ClearRequested += (_, _) => clearRequests++;

        viewModel.Clear();

        Assert.Equal(1, clearRequests);
        Assert.Empty(viewModel.Rows);
    }
}
