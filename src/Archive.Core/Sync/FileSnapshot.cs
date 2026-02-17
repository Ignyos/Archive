namespace Archive.Core.Sync;

public sealed class FileSnapshot
{
    public FileSnapshot(string path, long size, DateTime lastModifiedUtc)
    {
        Path = path;
        Size = size;
        LastModifiedUtc = lastModifiedUtc;
    }

    public string Path { get; }

    public long Size { get; }

    public DateTime LastModifiedUtc { get; }
}
