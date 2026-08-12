namespace Deyxis.Providers.FileDrop;

internal sealed class LocalFileDropFileSystem : IFileDropFileSystem
{
    public string GetFullPath(string path) => Path.GetFullPath(path);

    public FileDropFileMetadata GetMetadata(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists && !Directory.Exists(path))
        {
            throw new FileNotFoundException(null, path);
        }

        return new FileDropFileMetadata(file.Attributes, file.Exists ? file.Length : 0);
    }

    public bool HasReparsePointInPath(string path)
    {
        FileSystemInfo? current = new FileInfo(path);
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }

            current = current switch
            {
                FileInfo file => file.Directory,
                DirectoryInfo directory => directory.Parent,
                _ => null,
            };
        }

        return false;
    }

    public Stream OpenRead(string path) => new FileStream(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 4096,
        FileOptions.Asynchronous | FileOptions.SequentialScan);
}
