using System.ComponentModel;
using System.Runtime.CompilerServices;
using Deyxis.Core.Activities;
using Deyxis.Core.Island;
using Deyxis.Providers.Lyrics;
using Microsoft.UI.Xaml;

namespace Deyxis.UI;

public sealed class IslandViewModel : INotifyPropertyChanged
{
    private Activity? primaryActivity;
    private IReadOnlyList<Activity> queue = [];
    private IslandPresentationState presentationState;
    private string? previousLyric;
    private string? currentLyric;
    private string? nextLyric;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Activity? PrimaryActivity
    {
        get => primaryActivity;
        private set => SetProperty(ref primaryActivity, value);
    }

    public IReadOnlyList<Activity> Queue
    {
        get => queue;
        private set => SetProperty(ref queue, value);
    }

    public IslandPresentationState PresentationState
    {
        get => presentationState;
        private set => SetProperty(ref presentationState, value);
    }

    public string? PreviousLyric
    {
        get => previousLyric;
        private set => SetProperty(ref previousLyric, value);
    }

    public string? CurrentLyric
    {
        get => currentLyric;
        private set => SetProperty(ref currentLyric, value);
    }

    public string? NextLyric
    {
        get => nextLyric;
        private set => SetProperty(ref nextLyric, value);
    }

    public bool HasCurrentLyric => CurrentLyric is not null;

    public Visibility CurrentLyricVisibility => HasCurrentLyric ? Visibility.Visible : Visibility.Collapsed;

    public void Refresh(
        ActivitySnapshot snapshot,
        IslandPresentationState state,
        LyricsSnapshot? lyrics = null)
    {
        PrimaryActivity = snapshot.OrderedActivities.FirstOrDefault();
        Queue = snapshot.OrderedActivities.Skip(1).ToArray();
        PresentationState = state;

        var visibleLyrics = PrimaryActivity?.Category == ActivityCategory.Media
            ? lyrics ?? LyricsSnapshot.Empty
            : LyricsSnapshot.Empty;
        PreviousLyric = visibleLyrics.PreviousLine;
        CurrentLyric = visibleLyrics.CurrentLine;
        NextLyric = visibleLyrics.NextLine;
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        if (propertyName == nameof(CurrentLyric))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasCurrentLyric)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLyricVisibility)));
        }
    }
}
