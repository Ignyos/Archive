# Exclusion rules (minimal glob)

# Goals/outcomes for the phase (what “done” means).

## Phase3.2 is done when the following have been achieved.

- Minimal exclusion engine implemented.
  - Simple glob/wildcard matching (* and ?)
  - Per-job exclusion list supported

- Advanced syntax deferred.
  - No regex or .gitignore support yet
  - Documented as future enhancement

### Acceptance criteria

- Unit tests cover glob matching

- Exclusions are applied during scan/plan
