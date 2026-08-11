using Deyxis.Core.Activities;
using Deyxis.Core.Island;
using Deyxis.UI;
using Xunit;

namespace Deyxis.UI.Tests;

public sealed class IslandViewModelTests
{
    [Fact]
    public void Refresh_exposes_the_first_ordered_item_as_primary()
    {
        var viewModel = new IslandViewModel();

        viewModel.Refresh(
            TestSnapshot.With("Claude", "Codex"),
            IslandPresentationState.Expanded);

        Assert.Equal("Claude", viewModel.PrimaryActivity?.Title);
        Assert.Collection(viewModel.Queue, activity => Assert.Equal("Codex", activity.Title));
        Assert.Equal(IslandPresentationState.Expanded, viewModel.PresentationState);
    }

    private static class TestSnapshot
    {
        public static ActivitySnapshot With(params string[] titles) => new(
            titles.Select((title, index) => new Activity(
                Guid.Parse($"00000000-0000-0000-0000-{index + 1:D12}"),
                $"provider-{index + 1}",
                (ActivityCategory)0,
                ActivityState.Running,
                0,
                title,
                $"{title} activity",
                null,
                DateTimeOffset.UnixEpoch.AddMinutes(index)))
            .ToArray());
    }
}
