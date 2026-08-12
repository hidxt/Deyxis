using Deyxis.Core.Activities;
using Deyxis.Core.Events;
using Deyxis.PluginSdk;
using System.Security.Cryptography;

namespace Deyxis.Providers.FileDrop;

public sealed class FileDropProvider : IActivityProvider
{
    public const long DefaultMaximumFileSize = 25 * 1024 * 1024;

    private readonly object gate = new();
    private readonly IEventBus eventBus;
    private readonly IFileDropFileSystem fileSystem;
    private readonly ICurrentUserWallpaper wallpaper;
    private readonly TimeProvider timeProvider;
    private readonly long maximumFileSize;
    private readonly Dictionary<Guid, PendingDrop> pendingDrops = [];

    public FileDropProvider(
        IEventBus eventBus,
        TimeProvider? timeProvider = null,
        long maximumFileSize = DefaultMaximumFileSize)
        : this(
            eventBus,
            new LocalFileDropFileSystem(),
            UnavailableCurrentUserWallpaper.Instance,
            timeProvider,
            maximumFileSize)
    {
    }

    public FileDropProvider(
        IEventBus eventBus,
        ICurrentUserWallpaper wallpaper,
        TimeProvider? timeProvider = null,
        long maximumFileSize = DefaultMaximumFileSize)
        : this(eventBus, new LocalFileDropFileSystem(), wallpaper, timeProvider, maximumFileSize)
    {
    }

    internal FileDropProvider(
        IEventBus eventBus,
        IFileDropFileSystem fileSystem,
        TimeProvider? timeProvider = null,
        long maximumFileSize = DefaultMaximumFileSize)
        : this(
            eventBus,
            fileSystem,
            UnavailableCurrentUserWallpaper.Instance,
            timeProvider,
            maximumFileSize)
    {
    }

    internal FileDropProvider(
        IEventBus eventBus,
        IFileDropFileSystem fileSystem,
        ICurrentUserWallpaper wallpaper,
        TimeProvider? timeProvider = null,
        long maximumFileSize = DefaultMaximumFileSize)
    {
        this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.wallpaper = wallpaper ?? throw new ArgumentNullException(nameof(wallpaper));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.maximumFileSize = maximumFileSize > 0
            ? maximumFileSize
            : throw new ArgumentOutOfRangeException(nameof(maximumFileSize));
    }

    public string Id => "file-drop";

    public ProviderHealth Health { get; private set; } = ProviderHealth.Stopped;

    public void Start() => Health = ProviderHealth.Running;

    public void Stop()
    {
        PendingDrop[] drops;
        lock (gate)
        {
            drops = [.. pendingDrops.Values];
            pendingDrops.Clear();
            Health = ProviderHealth.Stopped;
        }

        foreach (var drop in drops)
        {
            eventBus.Publish(new ActivityRemoved(drop.ActivityId));
        }
    }

    public async Task<FileDropResult> HandleDropAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        cancellationToken.ThrowIfCancellationRequested();

        if (paths.Count == 0)
        {
            return PublishRejection(FileDropRejection.NoFile);
        }

        if (paths.Count != 1)
        {
            return PublishRejection(FileDropRejection.MultipleFiles);
        }

        var suppliedPath = paths[0];
        if (string.IsNullOrWhiteSpace(suppliedPath))
        {
            return PublishRejection(FileDropRejection.InvalidPath);
        }

        if (ContainsTraversalSegment(suppliedPath))
        {
            return PublishRejection(FileDropRejection.PathTraversal);
        }

        if (!Path.IsPathFullyQualified(suppliedPath) || IsUncPath(suppliedPath))
        {
            return PublishRejection(FileDropRejection.NonLocalPath);
        }

        string canonicalPath;
        try
        {
            canonicalPath = fileSystem.GetFullPath(suppliedPath);
        }
        catch (Exception exception) when (IsInvalidPathException(exception))
        {
            return PublishRejection(FileDropRejection.InvalidPath);
        }

        FileDropFileMetadata metadata;
        try
        {
            metadata = fileSystem.GetMetadata(canonicalPath);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return PublishRejection(FileDropRejection.FileNotFound);
        }

        if ((metadata.Attributes & FileAttributes.Directory) != 0)
        {
            return PublishRejection(FileDropRejection.Directory);
        }

        if ((metadata.Attributes & FileAttributes.ReparsePoint) != 0 ||
            fileSystem.HasReparsePointInPath(canonicalPath))
        {
            return PublishRejection(FileDropRejection.ReparsePoint);
        }

        var expectedImageType = GetImageType(Path.GetExtension(canonicalPath));
        if (expectedImageType is null)
        {
            return PublishRejection(FileDropRejection.UnsupportedFileType);
        }

        if (metadata.Length > maximumFileSize)
        {
            return PublishRejection(FileDropRejection.FileTooLarge);
        }

        FileFingerprint fingerprint;
        try
        {
            fingerprint = await ReadFingerprintAsync(
                canonicalPath,
                metadata.Length,
                expectedImageType.Value,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return PublishRejection(FileDropRejection.FileNotFound);
        }
        catch (InvalidDataException)
        {
            return PublishRejection(FileDropRejection.InvalidImageHeader);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var activityId = Guid.NewGuid();
        var confirmationToken = Guid.NewGuid();
        lock (gate)
        {
            pendingDrops.Add(confirmationToken, new PendingDrop(activityId, canonicalPath, fingerprint));
        }

        eventBus.Publish(new ActivityUpserted(CreateActivity(
            activityId,
            ActivityState.Waiting,
            "Image ready",
            "Review before setting wallpaper.")));

        return FileDropResult.Accept(activityId, confirmationToken);
    }

    public async Task<bool> RevalidatePendingAsync(
        Guid confirmationToken,
        CancellationToken cancellationToken = default)
    {
        PendingDrop pending;
        lock (gate)
        {
            if (!pendingDrops.TryGetValue(confirmationToken, out pending!) || pending.ConfirmationInProgress)
            {
                return false;
            }
        }

        if (await MatchesValidatedFileAsync(pending, cancellationToken).ConfigureAwait(false))
        {
            lock (gate)
            {
                return pendingDrops.TryGetValue(confirmationToken, out var current) &&
                    ReferenceEquals(current, pending) &&
                    !current.ConfirmationInProgress;
            }
        }

        RemoveInvalidPendingDrop(confirmationToken, pending);
        return false;
    }

    public bool Cancel(Guid confirmationToken)
    {
        PendingDrop? pending;
        lock (gate)
        {
            if (!pendingDrops.TryGetValue(confirmationToken, out pending) ||
                pending.ConfirmationInProgress)
            {
                return false;
            }

            pendingDrops.Remove(confirmationToken);
        }

        eventBus.Publish(new ActivityRemoved(pending.ActivityId));
        return true;
    }

    public async Task<WallpaperConfirmationResult> ConfirmAsync(
        Guid confirmationToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PendingDrop pending;
        lock (gate)
        {
            if (!pendingDrops.TryGetValue(confirmationToken, out pending!) || pending.ConfirmationInProgress)
            {
                return WallpaperConfirmationResult.NotPending;
            }

            pending.ConfirmationInProgress = true;
        }

        bool succeeded;
        try
        {
            if (!await MatchesValidatedFileAsync(pending, cancellationToken).ConfigureAwait(false))
            {
                RemoveInvalidPendingDrop(confirmationToken, pending);
                return WallpaperConfirmationResult.Failed;
            }

            succeeded = await wallpaper.TrySetAsync(pending.CanonicalPath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ResetConfirmation(confirmationToken, pending);
            throw;
        }
        catch (Exception exception) when (!IsFatalException(exception))
        {
            succeeded = false;
        }

        if (!succeeded)
        {
            ResetConfirmation(confirmationToken, pending);
            eventBus.Publish(new ActivityUpserted(CreateActivity(
                pending.ActivityId,
                ActivityState.Failed,
                "Wallpaper not changed",
                "Windows could not set the wallpaper. You can retry or cancel.")));
            return WallpaperConfirmationResult.Failed;
        }

        lock (gate)
        {
            pendingDrops.Remove(confirmationToken);
        }

        eventBus.Publish(new ActivityUpserted(CreateActivity(
            pending.ActivityId,
            ActivityState.Completed,
            "Wallpaper changed",
            "The current user's wallpaper was updated.")));
        eventBus.Publish(new ActivityRemoved(pending.ActivityId));
        return WallpaperConfirmationResult.Succeeded;
    }

    internal bool HasPendingDrop(Guid confirmationToken)
    {
        lock (gate)
        {
            return pendingDrops.ContainsKey(confirmationToken);
        }
    }

    private void ResetConfirmation(Guid confirmationToken, PendingDrop pending)
    {
        lock (gate)
        {
            if (pendingDrops.TryGetValue(confirmationToken, out var current) &&
                ReferenceEquals(current, pending))
            {
                current.ConfirmationInProgress = false;
            }
        }
    }

    private async Task<bool> MatchesValidatedFileAsync(
        PendingDrop pending,
        CancellationToken cancellationToken)
    {
        try
        {
            var metadata = fileSystem.GetMetadata(pending.CanonicalPath);
            if ((metadata.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
                fileSystem.HasReparsePointInPath(pending.CanonicalPath) ||
                metadata.Length != pending.Fingerprint.Length ||
                metadata.Length > maximumFileSize ||
                GetImageType(Path.GetExtension(pending.CanonicalPath)) != pending.Fingerprint.ImageType)
            {
                return false;
            }

            var current = await ReadFingerprintAsync(
                pending.CanonicalPath,
                metadata.Length,
                pending.Fingerprint.ImageType,
                cancellationToken).ConfigureAwait(false);
            return CryptographicOperations.FixedTimeEquals(current.Hash, pending.Fingerprint.Hash);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (!IsFatalException(exception))
        {
            return false;
        }
    }

    private async Task<FileFingerprint> ReadFingerprintAsync(
        string canonicalPath,
        long expectedLength,
        ImageType imageType,
        CancellationToken cancellationToken)
    {
        await using var stream = fileSystem.OpenRead(canonicalPath);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        var header = new byte[8];
        var headerLength = 0;
        long totalLength = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            totalLength += read;
            if (totalLength > maximumFileSize || totalLength > expectedLength)
            {
                throw new InvalidDataException("The image changed while it was being validated.");
            }

            var copyLength = Math.Min(read, header.Length - headerLength);
            if (copyLength > 0)
            {
                buffer.AsSpan(0, copyLength).CopyTo(header.AsSpan(headerLength));
                headerLength += copyLength;
            }

            hash.AppendData(buffer, 0, read);
        }

        if (totalLength != expectedLength || !HeaderMatches(imageType, header.AsSpan(0, headerLength)))
        {
            throw new InvalidDataException("The image changed while it was being validated.");
        }

        return new FileFingerprint(totalLength, imageType, hash.GetHashAndReset());
    }

    private void RemoveInvalidPendingDrop(Guid confirmationToken, PendingDrop pending)
    {
        lock (gate)
        {
            if (!pendingDrops.TryGetValue(confirmationToken, out var current) ||
                !ReferenceEquals(current, pending))
            {
                return;
            }

            pendingDrops.Remove(confirmationToken);
        }

        eventBus.Publish(new ActivityUpserted(CreateActivity(
            pending.ActivityId,
            ActivityState.Failed,
            "Image changed",
            "The image changed after validation and was rejected.")));
        eventBus.Publish(new ActivityRemoved(pending.ActivityId));
    }

    private FileDropResult PublishRejection(FileDropRejection rejection)
    {
        var activityId = Guid.NewGuid();
        eventBus.Publish(new ActivityUpserted(CreateActivity(
            activityId,
            ActivityState.Failed,
            "Image rejected",
            GetRejectionDescription(rejection))));
        return FileDropResult.Reject(activityId, rejection);
    }

    private Activity CreateActivity(Guid id, ActivityState state, string title, string description) => new(
        id,
        Id,
        ActivityCategory.FileDrop,
        state,
        Priority: 0,
        title,
        description,
        Progress: null,
        timeProvider.GetUtcNow());

    private static bool ContainsTraversalSegment(string path) =>
        path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.None)
            .Any(segment => segment is "." or "..");

    private static bool IsUncPath(string path) =>
        path.StartsWith(@"\\", StringComparison.Ordinal) ||
        path.StartsWith("//", StringComparison.Ordinal);

    private static bool IsInvalidPathException(Exception exception) =>
        exception is ArgumentException or NotSupportedException or PathTooLongException;

    private static bool IsFatalException(Exception exception) =>
        exception is OutOfMemoryException or StackOverflowException or AccessViolationException;

    private static ImageType? GetImageType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => ImageType.Jpeg,
        ".png" => ImageType.Png,
        ".bmp" => ImageType.Bmp,
        _ => null,
    };

    private static bool HeaderMatches(ImageType type, ReadOnlySpan<byte> header) => type switch
    {
        ImageType.Jpeg => header.StartsWith((ReadOnlySpan<byte>)[0xff, 0xd8, 0xff]),
        ImageType.Png => header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
        ImageType.Bmp => header.StartsWith((ReadOnlySpan<byte>)[0x42, 0x4d]),
        _ => false,
    };

    private static string GetRejectionDescription(FileDropRejection rejection) => rejection switch
    {
        FileDropRejection.NoFile => "Drop one local image.",
        FileDropRejection.MultipleFiles => "Only one image can be used at a time.",
        FileDropRejection.FileTooLarge => "The image exceeds the size limit.",
        FileDropRejection.UnsupportedFileType => "Use a JPG, PNG, or BMP image.",
        _ => "The local image could not be safely validated.",
    };

    private enum ImageType
    {
        Jpeg,
        Png,
        Bmp,
    }

    private sealed record FileFingerprint(long Length, ImageType ImageType, byte[] Hash);

    private sealed class PendingDrop(Guid activityId, string canonicalPath, FileFingerprint fingerprint)
    {
        public Guid ActivityId { get; } = activityId;

        public string CanonicalPath { get; } = canonicalPath;

        public FileFingerprint Fingerprint { get; } = fingerprint;

        public bool ConfirmationInProgress { get; set; }
    }

    private sealed class UnavailableCurrentUserWallpaper : ICurrentUserWallpaper
    {
        public static UnavailableCurrentUserWallpaper Instance { get; } = new();

        public ValueTask<bool> TrySetAsync(
            string canonicalPath,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }
}
