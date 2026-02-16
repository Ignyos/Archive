# EF Core DbContext + migrations

# Goals/outcomes for the phase (what “done” means).

## Phase2.1 is done when the following have been achieved.

- EF Core is added and configured for SQLite.
  - ArchiveDbContext created in Archive.Infrastructure
  - Connection string defined in appSettings.json

- Initial entity set is defined (skeletons only).
  - BackupJob
  - SyncOptions
  - ExclusionPattern
  - JobExecution
  - ExecutionLog
  - AppSettings

- Migrations are enabled and baseline migration created.
  - InitialCreate migration exists
  - Database can be created locally

### Acceptance criteria

- `dotnet ef migrations add InitialCreate` succeeds

- `dotnet ef database update` succeeds

- Local database file is created and contains expected tables
