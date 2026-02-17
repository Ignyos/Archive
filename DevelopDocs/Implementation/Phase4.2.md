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

## Implementation tasks (suggested breakdown)

- Dialog composition
  - Build modal sections for Basic Information, Sync Options, and Schedule
  - Add field controls for paths, options, and enabled state
  - Load defaults for create mode and persisted values for edit mode

- Validation layer
  - Add required and uniqueness checks for job name
  - Add path validation (exists, accessible, source/destination relationship rules)
  - Surface inline validation state and disable Preview/OK when invalid

- Destructive option safeguards
  - Add confirmation prompt for delete-orphaned toggle
  - Implement Yes/No/Preview behavior from confirmation dialog
  - Ensure canceled confirmation reverts checkbox state correctly

- Preview workflow
  - Wire Preview Operations command to dry-run service call
  - Display progress dialog during preview computation
  - Show preview results without persisting unsaved dialog changes