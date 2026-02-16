# Test infrastructure (TDD baseline)

# Goals/outcomes for the phase (what “done” means).

## Phase2.4 is done when the following have been achieved.

- Test project(s) created and added to the solution.
  - Archive.Core.Tests (or Archive.Tests)
  - Target net9.0

- Testing stack selected and configured.
  - Test framework (xUnit or NUnit)
  - Mocking library (Moq, NSubstitute, or similar)

- EF Core testing pattern defined.
  - In-memory SQLite for integration-style tests
  - Clear guidance for unit vs integration tests

- First baseline tests in place.
  - AppSettings binding test
  - DbContext can create and query database

### Acceptance criteria

- `dotnet test` runs successfully

- Baseline tests pass in CI

- Testing guidance documented
