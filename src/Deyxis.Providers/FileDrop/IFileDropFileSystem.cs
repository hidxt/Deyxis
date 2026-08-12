namespace Deyxis.Providers.FileDrop;

internal interface IFileDropFileSystem
{
    string GetFullPath(string path);

    FileDropFileMetadata GetMetadata(string path);

    bool HasReparsePointInPath(string path);

    Stream OpenRead(string path);
}

internal readonly record struct FileDropFileMetadata(FileAttributes Attributes, long Length);
