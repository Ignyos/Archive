namespace Archive.Desktop;

public sealed class JobListItemViewModel
{
    public Guid Id { get; init; }

    public string Status { get; set; } = "Idle";

    public bool Enabled { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string SourcePath { get; set; } = string.Empty;

    public string DestinationPath { get; set; } = string.Empty;

    public DateTime? NextRun { get; set; }
}