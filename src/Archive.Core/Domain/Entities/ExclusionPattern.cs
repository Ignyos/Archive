namespace Archive.Core.Domain.Entities;

public sealed class ExclusionPattern
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Pattern { get; set; } = string.Empty;

    public bool IsGlobal { get; set; }

    public bool IsSystemSuggestion { get; set; }

    public ICollection<BackupJobExclusionPattern> BackupJobExclusionPatterns { get; set; } = new List<BackupJobExclusionPattern>();
}
