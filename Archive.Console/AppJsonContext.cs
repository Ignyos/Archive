using System.Text.Json.Serialization;
using Archive.Core;

namespace Archive.Console;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BackupJobCollection))]
[JsonSerializable(typeof(BackupJob))]
[JsonSerializable(typeof(SyncOptions))]
[JsonSerializable(typeof(SyncResult))]
[JsonSerializable(typeof(OperationSchedule))]
[JsonSerializable(typeof(TimeWindow))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
