using Deyxis.Core.Activities;
using Deyxis.Core.Events;
using Deyxis.Providers.FileDrop;
using Xunit;

namespace Deyxis.Providers.Tests;

public sealed class FileDropProviderTests
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    [Theory]
    [InlineData(@"C:\images\..\secret.png", FileDropRejection.PathTraversal)]
    [InlineData(@"C:\images\shortcut.png", FileDropRejection.ReparsePoint)]
    [InlineData(@"C:\images\notes.txt", FileDropRejection.UnsupportedFileType)]
    public async Task Unsafe_path_is_rejected_before_file_content_is_read(
        string path,
        FileDropRejection expectedRejection)
    {
        var files = new FakeFileDropFileSystem();
        files.AddFile(@"C:\secret.png", PngHeader);
        files.AddFile(@"C:\images\shortcut.png", PngHeader, FileAttributes.ReparsePoint);
        files.AddFile(@"C:\images\notes.txt", PngHeader);
        var provider = new FileDropProvider(new EventBus(), files);

        var result = await provider.HandleDropAsync([path]);

        Assert.False(result.Accepted);
        Assert.Equal(expectedRejection, result.Rejection);
        Assert.Equal(0, files.OpenReadCount);
    }

    [Fact]
    public async Task Multiple_paths_are_rejected_without_inspecting_a_file()
    {
        var files = new FakeFileDropFileSystem();
        files.AddFile(@"C:\images\one.png", PngHeader);
        files.AddFile(@"C:\images\two.png", PngHeader);
        var provider = new FileDropProvider(new EventBus(), files);

        var result = await provider.HandleDropAsync(
            [@"C:\images\one.png", @"C:\images\two.png"]);

        Assert.Equal(FileDropRejection.MultipleFiles, result.Rejection);
        Assert.Equal(0, files.MetadataReadCount);
        Assert.Equal(0, files.OpenReadCount);
    }

    [Fact]
    public async Task Directory_is_rejected_before_file_content_is_read()
    {
        var files = new FakeFileDropFileSystem();
        files.AddDirectory(@"C:\images");
        var provider = new FileDropProvider(new EventBus(), files);

        var result = await provider.HandleDropAsync([@"C:\images"]);

        Assert.Equal(FileDropRejection.Directory, result.Rejection);
        Assert.Equal(0, files.OpenReadCount);
    }

    [Fact]
    public async Task File_beneath_reparse_point_directory_is_rejected_before_file_content_is_read()
    {
        var files = new FakeFileDropFileSystem();
        files.AddDirectory(@"C:\images", FileAttributes.Directory | FileAttributes.ReparsePoint);
        files.AddFile(@"C:\images\photo.png", PngHeader);
        var provider = new FileDropProvider(new EventBus(), files);

        var result = await provider.HandleDropAsync([@"C:\images\photo.png"]);

        Assert.Equal(FileDropRejection.ReparsePoint, result.Rejection);
        Assert.Equal(0, files.OpenReadCount);
    }

    [Fact]
    public async Task Oversized_file_is_rejected_before_file_content_is_read()
    {
        var files = new FakeFileDropFileSystem();
        files.AddFile(@"C:\images\large.png", PngHeader, length: 33);
        var provider = new FileDropProvider(new EventBus(), files, maximumFileSize: 32);

        var result = await provider.HandleDropAsync([@"C:\images\large.png"]);

        Assert.Equal(FileDropRejection.FileTooLarge, result.Rejection);
        Assert.Equal(0, files.OpenReadCount);
    }

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.jpeg")]
    [InlineData("photo.png")]
    [InlineData("photo.bmp")]
    public async Task Header_must_match_the_supported_extension(string fileName)
    {
        var path = $@"C:\images\{fileName}";
        var files = new FakeFileDropFileSystem();
        files.AddFile(path, [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07]);
        var provider = new FileDropProvider(new EventBus(), files);

        var result = await provider.HandleDropAsync([path]);

        Assert.Equal(FileDropRejection.InvalidImageHeader, result.Rejection);
        Assert.Equal(1, files.OpenReadCount);
    }

    [Theory]
    [MemberData(nameof(SupportedImages))]
    public async Task Supported_image_publishes_waiting_activity_without_exposing_its_path(
        string fileName,
        byte[] header)
    {
        var path = $@"C:\private\{fileName}";
        var files = new FakeFileDropFileSystem();
        files.AddFile(path, header);
        var bus = new EventBus();
        ActivityUpserted? published = null;
        using var subscription = bus.Subscribe<ActivityUpserted>(message => published = message);
        var provider = new FileDropProvider(bus, files);

        var result = await provider.HandleDropAsync([path]);

        Assert.True(result.Accepted);
        Assert.NotEqual(Guid.Empty, result.ConfirmationToken);
        Assert.Null(result.Rejection);
        Assert.NotNull(published);
        Assert.Equal(result.ActivityId, published.Activity.Id);
        Assert.Equal("file-drop", published.Activity.ProviderId);
        Assert.Equal(ActivityCategory.FileDrop, published.Activity.Category);
        Assert.Equal(ActivityState.Waiting, published.Activity.State);
        Assert.DoesNotContain(path, published.Activity.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(path, published.Activity.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(path, result.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(provider.HasPendingDrop(result.ConfirmationToken));
    }

    [Fact]
    public async Task Cancel_with_private_confirmation_token_removes_pending_activity()
    {
        var files = new FakeFileDropFileSystem();
        files.AddFile(@"C:\images\photo.png", PngHeader);
        var bus = new EventBus();
        ActivityRemoved? removed = null;
        using var subscription = bus.Subscribe<ActivityRemoved>(message => removed = message);
        var provider = new FileDropProvider(bus, files);
        var accepted = await provider.HandleDropAsync([@"C:\images\photo.png"]);

        var canceled = provider.Cancel(accepted.ConfirmationToken);

        Assert.True(canceled);
        Assert.NotNull(removed);
        Assert.Equal(accepted.ActivityId, removed.ActivityId);
        Assert.False(provider.HasPendingDrop(accepted.ConfirmationToken));
        Assert.False(provider.Cancel(accepted.ConfirmationToken));
    }

    [Fact]
    public async Task Wallpaper_is_not_changed_until_the_accepted_drop_is_explicitly_confirmed()
    {
        var files = new FakeFileDropFileSystem();
        files.AddFile(@"C:\images\photo.png", PngHeader);
        var wallpaper = new FakeCurrentUserWallpaper();
        var provider = new FileDropProvider(new EventBus(), files, wallpaper);

        var accepted = await provider.HandleDropAsync([@"C:\images\photo.png"]);

        Assert.Empty(wallpaper.RequestedPaths);

        await provider.ConfirmAsync(accepted.ConfirmationToken);

        Assert.Equal([@"C:\images\photo.png"], wallpaper.RequestedPaths);
    }

    [Fact]
    public async Task Successful_confirmation_completes_then_removes_activity_and_consumes_token()
    {
        var files = new FakeFileDropFileSystem();
        files.AddFile(@"C:\images\photo.png", PngHeader);
        var wallpaper = new FakeCurrentUserWallpaper();
        var bus = new EventBus();
        var events = new List<object>();
        using var upserted = bus.Subscribe<ActivityUpserted>(message => events.Add(message));
        using var removed = bus.Subscribe<ActivityRemoved>(message => events.Add(message));
        var provider = new FileDropProvider(bus, files, wallpaper);
        var accepted = await provider.HandleDropAsync([@"C:\images\photo.png"]);
        events.Clear();

        var result = await provider.ConfirmAsync(accepted.ConfirmationToken);

        Assert.Equal(WallpaperConfirmationResult.Succeeded, result);
        var completed = Assert.IsType<ActivityUpserted>(events[0]);
        Assert.Equal(accepted.ActivityId, completed.Activity.Id);
        Assert.Equal(ActivityState.Completed, completed.Activity.State);
        var activityRemoved = Assert.IsType<ActivityRemoved>(events[1]);
        Assert.Equal(accepted.ActivityId, activityRemoved.ActivityId);
        Assert.False(provider.HasPendingDrop(accepted.ConfirmationToken));
        Assert.Equal(
            WallpaperConfirmationResult.NotPending,
            await provider.ConfirmAsync(accepted.ConfirmationToken));
        Assert.Single(wallpaper.RequestedPaths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_failure_is_contained_as_path_free_failed_activity_and_remains_cancellable(
        bool throwException)
    {
        const string privatePath = @"C:\private\sensitive-photo.png";
        var files = new FakeFileDropFileSystem();
        files.AddFile(privatePath, PngHeader);
        var wallpaper = new FakeCurrentUserWallpaper
        {
            Result = false,
            Exception = throwException ? new InvalidOperationException("native failure") : null,
        };
        var bus = new EventBus();
        ActivityUpserted? latest = null;
        ActivityRemoved? removed = null;
        using var upsertedSubscription = bus.Subscribe<ActivityUpserted>(message => latest = message);
        using var removedSubscription = bus.Subscribe<ActivityRemoved>(message => removed = message);
        var provider = new FileDropProvider(bus, files, wallpaper);
        var accepted = await provider.HandleDropAsync([privatePath]);

        var result = await provider.ConfirmAsync(accepted.ConfirmationToken);

        Assert.Equal(WallpaperConfirmationResult.Failed, result);
        Assert.NotNull(latest);
        Assert.Equal(accepted.ActivityId, latest.Activity.Id);
        Assert.Equal(ActivityState.Failed, latest.Activity.State);
        Assert.DoesNotContain(privatePath, latest.Activity.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(privatePath, latest.Activity.Description, StringComparison.OrdinalIgnoreCase);
        Assert.True(provider.HasPendingDrop(accepted.ConfirmationToken));

        Assert.True(provider.Cancel(accepted.ConfirmationToken));
        Assert.Equal(accepted.ActivityId, removed?.ActivityId);
    }

    [Fact]
    public async Task Canceled_drop_cannot_be_confirmed_and_never_calls_wallpaper_facade()
    {
        var files = new FakeFileDropFileSystem();
        files.AddFile(@"C:\images\photo.png", PngHeader);
        var wallpaper = new FakeCurrentUserWallpaper();
        var provider = new FileDropProvider(new EventBus(), files, wallpaper);
        var accepted = await provider.HandleDropAsync([@"C:\images\photo.png"]);

        Assert.True(provider.Cancel(accepted.ConfirmationToken));

        Assert.Equal(
            WallpaperConfirmationResult.NotPending,
            await provider.ConfirmAsync(accepted.ConfirmationToken));
        Assert.Empty(wallpaper.RequestedPaths);
    }

    [Fact]
    public async Task Confirmation_in_progress_cannot_be_canceled_or_started_twice()
    {
        var files = new FakeFileDropFileSystem();
        files.AddFile(@"C:\images\photo.png", PngHeader);
        var wallpaper = new BlockingCurrentUserWallpaper();
        var provider = new FileDropProvider(new EventBus(), files, wallpaper);
        var accepted = await provider.HandleDropAsync([@"C:\images\photo.png"]);

        var firstConfirmation = provider.ConfirmAsync(accepted.ConfirmationToken);
        await wallpaper.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(provider.Cancel(accepted.ConfirmationToken));
        Assert.Equal(
            WallpaperConfirmationResult.NotPending,
            await provider.ConfirmAsync(accepted.ConfirmationToken));

        wallpaper.Complete(true);
        Assert.Equal(WallpaperConfirmationResult.Succeeded, await firstConfirmation);
        Assert.Equal(1, wallpaper.CallCount);
    }

    [Fact]
    public async Task Revalidation_rejects_a_file_replaced_before_preview_is_exposed()
    {
        const string path = @"C:\images\photo.png";
        var files = new FakeFileDropFileSystem();
        files.AddFile(path, [.. PngHeader, 0x01]);
        var provider = new FileDropProvider(new EventBus(), files);
        var accepted = await provider.HandleDropAsync([path]);
        files.AddFile(path, [.. PngHeader, 0x02]);

        var remainsValid = await provider.RevalidatePendingAsync(accepted.ConfirmationToken);

        Assert.False(remainsValid);
        Assert.False(provider.HasPendingDrop(accepted.ConfirmationToken));
    }

    [Fact]
    public async Task Confirmation_rejects_a_file_replaced_after_preview_validation()
    {
        const string path = @"C:\images\photo.png";
        var files = new FakeFileDropFileSystem();
        files.AddFile(path, [.. PngHeader, 0x01]);
        var wallpaper = new FakeCurrentUserWallpaper();
        var provider = new FileDropProvider(new EventBus(), files, wallpaper);
        var accepted = await provider.HandleDropAsync([path]);
        Assert.True(await provider.RevalidatePendingAsync(accepted.ConfirmationToken));
        files.AddFile(path, [.. PngHeader, 0x02]);

        var result = await provider.ConfirmAsync(accepted.ConfirmationToken);

        Assert.Equal(WallpaperConfirmationResult.Failed, result);
        Assert.Empty(wallpaper.RequestedPaths);
        Assert.False(provider.HasPendingDrop(accepted.ConfirmationToken));
    }

    [Fact]
    public async Task Canceled_validation_does_not_read_or_publish()
    {
        var files = new FakeFileDropFileSystem();
        files.AddFile(@"C:\images\photo.png", PngHeader);
        var bus = new EventBus();
        var publicationCount = 0;
        using var subscription = bus.Subscribe<ActivityUpserted>(_ => publicationCount++);
        var provider = new FileDropProvider(bus, files);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.HandleDropAsync([@"C:\images\photo.png"], cancellation.Token));

        Assert.Equal(0, files.MetadataReadCount);
        Assert.Equal(0, files.OpenReadCount);
        Assert.Equal(0, publicationCount);
    }

    [Fact]
    public async Task Bounded_local_png_fixture_is_validated_and_canceled_without_wallpaper_change()
    {
        var fixtureDirectory = Path.Combine(
            Path.GetTempPath(),
            $"deyxis-file-drop-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureDirectory);
        var fixturePath = Path.Combine(fixtureDirectory, "fixture.png");
        await File.WriteAllBytesAsync(fixturePath, PngHeader);
        var wallpaper = new FakeCurrentUserWallpaper();
        var provider = new FileDropProvider(new EventBus(), wallpaper);

        try
        {
            var result = await provider.HandleDropAsync([fixturePath]);

            Assert.True(result.Accepted);
            Assert.Empty(wallpaper.RequestedPaths);
            Assert.True(provider.Cancel(result.ConfirmationToken));
            Assert.Empty(wallpaper.RequestedPaths);
        }
        finally
        {
            File.Delete(fixturePath);
            Directory.Delete(fixtureDirectory);
        }
    }

    public static TheoryData<string, byte[]> SupportedImages => new()
    {
        { "photo.jpg", [0xff, 0xd8, 0xff] },
        { "photo.jpeg", [0xff, 0xd8, 0xff] },
        { "photo.png", PngHeader },
        { "photo.bmp", [0x42, 0x4d] },
    };

    private sealed class FakeFileDropFileSystem : IFileDropFileSystem
    {
        private readonly Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);

        public int MetadataReadCount { get; private set; }

        public int OpenReadCount { get; private set; }

        public string GetFullPath(string path) => Path.GetFullPath(path);

        public FileDropFileMetadata GetMetadata(string path)
        {
            MetadataReadCount++;
            if (!entries.TryGetValue(path, out var entry))
            {
                throw new FileNotFoundException(null, path);
            }

            return new FileDropFileMetadata(entry.Attributes, entry.Length);
        }

        public Stream OpenRead(string path)
        {
            OpenReadCount++;
            if (!entries.TryGetValue(path, out var entry))
            {
                throw new FileNotFoundException(null, path);
            }

            return new MemoryStream(entry.Content, writable: false);
        }

        public bool HasReparsePointInPath(string path)
        {
            var current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if (entries.TryGetValue(current, out var entry) &&
                    (entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }

                current = Path.GetDirectoryName(current);
            }

            return false;
        }

        public void AddFile(
            string path,
            byte[] content,
            FileAttributes attributes = FileAttributes.Normal,
            long? length = null) =>
            entries[Path.GetFullPath(path)] = new Entry(attributes, length ?? content.LongLength, content);

        public void AddDirectory(string path, FileAttributes attributes = FileAttributes.Directory) =>
            entries[Path.GetFullPath(path)] = new Entry(attributes, 0, []);

        private sealed record Entry(FileAttributes Attributes, long Length, byte[] Content);
    }

    private sealed class FakeCurrentUserWallpaper : ICurrentUserWallpaper
    {
        public List<string> RequestedPaths { get; } = [];

        public bool Result { get; init; } = true;

        public Exception? Exception { get; init; }

        public ValueTask<bool> TrySetAsync(string canonicalPath, CancellationToken cancellationToken = default)
        {
            RequestedPaths.Add(canonicalPath);
            if (Exception is not null)
            {
                throw Exception;
            }

            return ValueTask.FromResult(Result);
        }
    }

    private sealed class BlockingCurrentUserWallpaper : ICurrentUserWallpaper
    {
        private readonly TaskCompletionSource<bool> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public async ValueTask<bool> TrySetAsync(
            string canonicalPath,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Started.TrySetResult();
            return await completion.Task.WaitAsync(cancellationToken);
        }

        public void Complete(bool result) => completion.TrySetResult(result);
    }
}
