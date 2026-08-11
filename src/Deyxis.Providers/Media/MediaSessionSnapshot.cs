using Deyxis.Core.Activities;

namespace Deyxis.Providers.Media;

public enum MediaPlaybackState
{
    Stopped,
    Playing,
    Paused,
}

public sealed record MediaSessionControls(
    bool CanTogglePlayPause,
    bool CanSkipNext,
    bool CanSkipPrevious);

public sealed record MediaSessionSnapshot(
    string SessionId,
    string Title,
    string Artist,
    TimeSpan Position,
    TimeSpan Duration,
    MediaPlaybackState PlaybackState,
    MediaSessionControls Controls,
    string AlbumTitle = "",
    string SourceAppUserModelId = "")
{
    public Activity ToActivity(string providerId, Guid activityId, DateTimeOffset timestamp)
    {
        var title = string.IsNullOrWhiteSpace(Title) ? SourceAppUserModelId : Title;
        var mediaDescription = string.Join(" — ", new[] { Artist, AlbumTitle }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var descriptionParts = new[] { mediaDescription, SourceAppUserModelId }
            .Where(value => !string.IsNullOrWhiteSpace(value));

        return new Activity(
            activityId,
            providerId,
            ActivityCategory.Media,
            PlaybackState == MediaPlaybackState.Playing ? ActivityState.Running : ActivityState.Idle,
            0,
            title,
            string.Join(" · ", descriptionParts),
            Duration > TimeSpan.Zero ? Math.Clamp(Position.TotalSeconds / Duration.TotalSeconds, 0, 1) : null,
            timestamp);
    }
}
