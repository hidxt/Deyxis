using System.Text.Json;
using Deyxis.Core.Activities;
using Deyxis.Core.History;

namespace Deyxis.Platform.Windows.Storage;

public sealed class ActivityHistoryStore
{
    public const int MaximumFileSizeBytes = 128 * 1024;

    private const int CurrentVersion = 1;
    private const int MaximumTextLength = 512;
    private const string FileName = "activity-history.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly LocalJsonFile file;

    public ActivityHistoryStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Deyxis"))
    {
    }

    public ActivityHistoryStore(string appDataRoot)
    {
        file = new LocalJsonFile(appDataRoot, FileName, MaximumFileSizeBytes);
    }

    public async ValueTask<ActivityHistoryRing> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await file.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (bytes is null)
            {
                return new ActivityHistoryRing();
            }

            var document = JsonSerializer.Deserialize<HistoryDocument>(bytes, SerializerOptions);
            if (document is not { Version: CurrentVersion } ||
                document.Entries is null ||
                document.Entries.Length > ActivityHistoryRing.Capacity ||
                document.Entries.Any(entry => entry is null || !entry.IsValid()))
            {
                return new ActivityHistoryRing();
            }

            return new ActivityHistoryRing(document.Entries.Select(entry => entry!.ToSummary()));
        }
        catch (Exception exception) when (LocalJsonFile.IsRecoverableReadException(exception))
        {
            return new ActivityHistoryRing();
        }
    }

    public async ValueTask SaveAsync(
        IEnumerable<ActivityHistorySummary> summaries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summaries);

        var entries = summaries
            .Take(ActivityHistoryRing.Capacity)
            .Select(HistoryEntry.FromSummary)
            .ToArray();
        if (entries.Any(entry => !entry.IsValid()))
        {
            throw new ArgumentException("History contains an invalid summary.", nameof(summaries));
        }

        var document = new HistoryDocument
        {
            Version = CurrentVersion,
            Entries = entries,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, SerializerOptions);
        await file.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        file.Delete();
        return ValueTask.CompletedTask;
    }

    private sealed record HistoryDocument
    {
        public required int Version { get; init; }

        public required HistoryEntry?[] Entries { get; init; }
    }

    private sealed record HistoryEntry
    {
        public required string ProviderId { get; init; }

        public required ActivityCategory Category { get; init; }

        public required ActivityState State { get; init; }

        public required string Title { get; init; }

        public required DateTimeOffset Timestamp { get; init; }

        public bool IsValid() =>
            IsSafeText(ProviderId) &&
            Enum.IsDefined(Category) &&
            Enum.IsDefined(State) &&
            IsSafeText(Title) &&
            Timestamp != default;

        public ActivityHistorySummary ToSummary() =>
            new(ProviderId, Category, State, Title, Timestamp);

        public static HistoryEntry FromSummary(ActivityHistorySummary summary)
        {
            ArgumentNullException.ThrowIfNull(summary);

            return new HistoryEntry
            {
                ProviderId = summary.ProviderId,
                Category = summary.Category,
                State = summary.State,
                Title = summary.Title,
                Timestamp = summary.Timestamp,
            };
        }

        private static bool IsSafeText(string? value) =>
            !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumTextLength;
    }
}
