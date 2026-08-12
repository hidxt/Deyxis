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
    private Guid? validatedFileDropActivityId;
    private Guid? fileDropConfirmationToken;
    private string? fileDropPreviewSource;

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

    public bool HasFileDropPreview =>
        validatedFileDropActivityId is Guid activityId &&
        IsVisibleFileDropActivity(activityId) &&
        fileDropPreviewSource is not null &&
        fileDropConfirmationToken is not null;

    public Visibility FileDropPreviewVisibility => HasFileDropPreview
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility FileDropActionsVisibility => FileDropPreviewVisibility;

    public string? FileDropPreviewSource => HasFileDropPreview ? fileDropPreviewSource : null;

    public Guid? FileDropConfirmationToken => HasFileDropPreview ? fileDropConfirmationToken : null;

    public void Refresh(
        ActivitySnapshot snapshot,
        IslandPresentationState state,
        LyricsSnapshot? lyrics = null)
    {
        PrimaryActivity = snapshot.OrderedActivities.FirstOrDefault();
        Queue = snapshot.OrderedActivities.Skip(1).ToArray();
        PresentationState = state;

        if (validatedFileDropActivityId is Guid activityId &&
            snapshot.OrderedActivities.All(activity => activity.Id != activityId))
        {
            ClearValidatedFileDrop();
        }
        else
        {
            NotifyFileDropPropertiesChanged();
        }

        var visibleLyrics = PrimaryActivity?.Category == ActivityCategory.Media
            ? lyrics ?? LyricsSnapshot.Empty
            : LyricsSnapshot.Empty;
        PreviousLyric = visibleLyrics.PreviousLine;
        CurrentLyric = visibleLyrics.CurrentLine;
        NextLyric = visibleLyrics.NextLine;
    }

    public void SetValidatedFileDrop(
        Guid activityId,
        Guid confirmationToken,
        string canonicalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);

        var activity = PrimaryActivity?.Id == activityId
            ? PrimaryActivity
            : Queue.FirstOrDefault(candidate => candidate.Id == activityId);
        if (activity?.Category != ActivityCategory.FileDrop)
        {
            return;
        }

        validatedFileDropActivityId = activityId;
        fileDropConfirmationToken = confirmationToken;
        fileDropPreviewSource = new Uri(canonicalPath, UriKind.Absolute).AbsoluteUri;
        NotifyFileDropPropertiesChanged();
    }

    public void ClearValidatedFileDrop()
    {
        if (validatedFileDropActivityId is null &&
            fileDropConfirmationToken is null &&
            fileDropPreviewSource is null)
        {
            return;
        }

        validatedFileDropActivityId = null;
        fileDropConfirmationToken = null;
        fileDropPreviewSource = null;
        NotifyFileDropPropertiesChanged();
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

        else if (propertyName == nameof(PrimaryActivity))
        {
            NotifyFileDropPropertiesChanged();
        }
    }

    private void NotifyFileDropPropertiesChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasFileDropPreview)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileDropPreviewVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileDropActionsVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileDropPreviewSource)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileDropConfirmationToken)));
    }

    private bool IsVisibleFileDropActivity(Guid activityId) =>
        PrimaryActivity is { Category: ActivityCategory.FileDrop } primary && primary.Id == activityId ||
        Queue.Any(activity => activity.Id == activityId && activity.Category == ActivityCategory.FileDrop);
}
