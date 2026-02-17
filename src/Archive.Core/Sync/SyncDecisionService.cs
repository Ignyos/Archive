using Archive.Core.Domain.Enums;

namespace Archive.Core.Sync;

public sealed class SyncDecisionService
{
    public SyncAction Decide(
        FileSnapshot source,
        FileSnapshot? destination,
        SyncMode syncMode,
        ComparisonMethod comparisonMethod,
        OverwriteBehavior overwriteBehavior)
    {
        if (destination is null)
        {
            return SyncAction.Copy;
        }

        var unchanged = comparisonMethod switch
        {
            ComparisonMethod.Fast =>
                source.Size == destination.Size &&
                source.LastModifiedUtc == destination.LastModifiedUtc,
            ComparisonMethod.Accurate =>
                source.Size == destination.Size &&
                source.LastModifiedUtc == destination.LastModifiedUtc,
            _ => false
        };

        if (unchanged)
        {
            return SyncAction.Skip;
        }

        return SyncAction.Update;
    }
}
