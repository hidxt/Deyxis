using System.Text.Json;

namespace Deyxis.Platform.Windows.Storage;

internal sealed class LocalJsonFile
{
    private readonly string root;
    private readonly string path;
    private readonly int maximumSizeBytes;

    public LocalJsonFile(string appDataRoot, string fileName, int maximumSizeBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSizeBytes);

        root = Path.GetFullPath(appDataRoot);
        path = ResolveContainedPath(root, fileName);
        this.maximumSizeBytes = maximumSizeBytes;
    }

    public async ValueTask<byte[]?> ReadAsync(CancellationToken cancellationToken)
    {
        FileInfo fileInfo = new(path);
        if (!fileInfo.Exists)
        {
            return null;
        }

        if (fileInfo.Length > maximumSizeBytes)
        {
            throw new InvalidDataException("The JSON file exceeds its size limit.");
        }

        var length = checked((int)fileInfo.Length);
        var bytes = new byte[length];
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var offset = 0;
        while (offset < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(offset), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("The JSON file changed while it was being read.");
            }

            offset += read;
        }

        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("The JSON file exceeds its declared size.");
        }

        return bytes;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (bytes.Length > maximumSizeBytes)
        {
            throw new InvalidDataException("The JSON document exceeds its size limit.");
        }

        Directory.CreateDirectory(root);
        var temporaryPath = ResolveContainedPath(root, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (IsRecoverableReadException(exception))
            {
            }
        }
    }

    public void Delete()
    {
        File.Delete(path);
    }

    internal static string ResolveContainedPath(string rootPath, string fileName)
    {
        var canonicalRoot = Path.GetFullPath(rootPath);
        var candidate = Path.GetFullPath(fileName, canonicalRoot);
        var rootPrefix = canonicalRoot.EndsWith(Path.DirectorySeparatorChar)
            ? canonicalRoot
            : canonicalRoot + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The storage path must remain inside the app-data root.", nameof(fileName));
        }

        return candidate;
    }

    internal static bool IsRecoverableReadException(Exception exception) => exception is
        InvalidDataException or
        IOException or
        UnauthorizedAccessException or
        JsonException or
        NotSupportedException or
        InvalidOperationException;
}
