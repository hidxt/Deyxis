namespace Deyxis.Providers.FileDrop;

public enum FileDropRejection
{
    NoFile,
    MultipleFiles,
    InvalidPath,
    PathTraversal,
    NonLocalPath,
    FileNotFound,
    Directory,
    ReparsePoint,
    UnsupportedFileType,
    FileTooLarge,
    InvalidImageHeader,
}
