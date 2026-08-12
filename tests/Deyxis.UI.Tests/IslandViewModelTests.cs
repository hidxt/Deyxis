using Deyxis.Core.Activities;
using Deyxis.Core.Island;
using Deyxis.Providers.Lyrics;
using Deyxis.UI;
using Xunit;

namespace Deyxis.UI.Tests;

public sealed class IslandViewModelTests
{
    [Fact]
    public void Media_primary_exposes_its_selected_lyric_snapshot()
    {
        var viewModel = new IslandViewModel();
        var lyrics = new LyricsSnapshot("Before", "Current lyric", "After");

        viewModel.Refresh(
            TestSnapshot.WithCategory(ActivityCategory.Media, "Clair de Lune"),
            IslandPresentationState.Expanded,
            lyrics);

        Assert.Equal("Before", viewModel.PreviousLyric);
        Assert.Equal("Current lyric", viewModel.CurrentLyric);
        Assert.Equal("After", viewModel.NextLyric);
        Assert.True(viewModel.HasCurrentLyric);
    }

    [Fact]
    public void Non_media_primary_does_not_expose_a_lyric_snapshot()
    {
        var viewModel = new IslandViewModel();

        viewModel.Refresh(
            TestSnapshot.WithCategory((ActivityCategory)99, "Claude"),
            IslandPresentationState.Expanded,
            new LyricsSnapshot("Before", "Not for AI", "After"));

        Assert.Null(viewModel.PreviousLyric);
        Assert.Null(viewModel.CurrentLyric);
        Assert.Null(viewModel.NextLyric);
        Assert.False(viewModel.HasCurrentLyric);
    }

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

    [Fact]
    public void Refresh_replaces_primary_when_snapshot_order_changes()
    {
        var viewModel = new IslandViewModel();
        viewModel.Refresh(TestSnapshot.With("Codex", "Claude"), IslandPresentationState.Expanded);
        viewModel.Refresh(TestSnapshot.With("Claude", "Codex"), IslandPresentationState.Expanded);

        Assert.Equal("Claude", viewModel.PrimaryActivity!.Title);
    }

    [Fact]
    public void Refresh_notifies_snapshot_backed_properties_when_snapshot_order_changes()
    {
        var viewModel = new IslandViewModel();
        var changedProperties = new List<string?>();
        viewModel.Refresh(TestSnapshot.With("Codex", "Claude"), IslandPresentationState.Expanded);
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.Refresh(TestSnapshot.With("Claude", "Codex"), IslandPresentationState.Expanded);

        Assert.Contains(nameof(IslandViewModel.PrimaryActivity), changedProperties);
        Assert.Contains(nameof(IslandViewModel.Queue), changedProperties);
    }

    private static class TestSnapshot
    {
        public static ActivitySnapshot WithCategory(ActivityCategory category, string title) => new(
            [new Activity(
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                "provider-1",
                category,
                ActivityState.Running,
                0,
                title,
                $"{title} activity",
                null,
                DateTimeOffset.UnixEpoch)]);

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
