# Application logging (Serilog)

# Goals/outcomes for the phase (what “done” means).

## Phase2.3 is done when the following have been achieved.

- Serilog is configured for application/diagnostic logging.
  - Always-on verbose logging (not per file transfer)
  - Rolling file logs with retention (e.g., 7–14 days)
  - Log file location documented

- Logging integrates with Microsoft.Extensions.Logging.
  - ILogger<T> usable throughout the app

- Future support-export scenario is enabled by design.
  - Logs are stored locally and can be packaged by date range

### Acceptance criteria

- App writes structured logs to disk on startup and shutdown

- Logs roll daily (or by size) and old logs are pruned

- Log location and retention policy are documented
