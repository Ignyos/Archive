# Dependency injection + configuration

# Goals/outcomes for the phase (what “done” means).

## Phase2.2 is done when the following have been achieved.

- Dependency injection is configured for Archive.Desktop.
  - Service collection bootstrapped in App startup
  - Core/Infrastructure services registered

- Configuration is bound and available via DI.
  - AppSettings bound to configuration
  - DbContext uses configured connection string

- Composition root is documented.
  - Where registrations live
  - How to add new services

### Acceptance criteria

- App starts with DI container initialized

- Services can be resolved in ViewModels/services

- DbContext resolves and connects using appSettings.json
