namespace Archive.Core.Domain.Entities;

public sealed class ApplicationLog
{
    public long Id { get; set; }

    public DateTime TimestampUtc { get; set; }

    public string Level { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Exception { get; set; }

    public string? SourceContext { get; set; }
}