namespace Archive.Desktop;

public sealed class JobPreviewResult
{
    public int FilesToAdd { get; init; }

    public int FilesToUpdate { get; init; }

    public int FilesToDelete { get; init; }

    public int FilesUnchanged { get; init; }

    public int FilesSkipped { get; init; }

    public long TotalBytesToTransfer { get; init; }
}
