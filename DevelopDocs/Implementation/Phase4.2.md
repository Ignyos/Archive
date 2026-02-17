# Job create/edit workflow + validation

# Goals/outcomes for the phase (what “done” means).

## Phase4.2 is done when the following have been achieved.

- Job Create/Edit dialog supports full baseline configuration.
  - Name, description, source, destination, enabled flag
  - Sync options (recursive, delete orphaned, skip hidden/system, verify after copy)
  - Trigger type selection (Manual, One-Time, Recurring)

- Validation and guardrails are implemented.
  - Required-field and path accessibility validation with inline messages
  - Destination cannot equal source or be nested under source
  - Destructive delete-orphaned option requires explicit confirmation

- Preview workflow is wired.
  - Preview Operations executes dry-run behavior
  - Results open in a details-style view without saving until user confirms

### Acceptance criteria

- Invalid configurations block Preview/OK with clear inline validation messages

- Valid configurations save correctly and are reflected in the main job list