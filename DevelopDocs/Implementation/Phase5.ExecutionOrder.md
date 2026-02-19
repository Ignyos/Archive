# Phase 5 recommended execution order

This document defines a dependency-aware plan for post-Phase-4 work, focused on real-time execution visibility, higher-confidence operations, and runtime safety at increased concurrency.

## Ordering principles

- Deliver end-to-end progress telemetry before building progress UI surfaces.
- Prefer operational safety and diagnosability before adding optional convenience features.
- Keep each wave independently shippable with clear exit criteria.
- Preserve current stable behavior while introducing deeper runtime instrumentation.

## Recommended sequence (by wave)

### Wave 1 — Runtime telemetry foundation

Primary goal: expose live execution state from engine/orchestration in a UI-consumable form.

1) Execution progress model + event stream
- Define progress payload contract (current file, totals, percentage, bytes transferred, counters)
- Emit progress events from execution pipeline with bounded frequency
- Include cancellation-safe final state events

2) Job runtime session registry
- Track active execution sessions by job ID
- Expose read-only snapshots for current progress and state
- Ensure cleanup on completion/failure/cancel

3) Integration + tests
- Unit tests for progress event sequencing and snapshot consistency
- Validate no regressions in current scheduling/execution behavior

Exit criteria:
- Running jobs produce stable live telemetry, and consumers can query current progress state at any time.

---

### Wave 2 — Progress Window (real-time UX)

Primary goal: deliver the UI experience described in future features.

4) Progress window shell + navigation
- Add non-modal single-instance Progress window
- Open from running-job interactions (double-click/context action)
- Reuse/activate existing progress window when already open

5) Live metrics + controls
- Show current file, percentage, counters, bytes transferred, and ETA (when computable)
- Add live log tail (bounded list)
- Add Cancel action with confirmation + execution stop integration

6) UX hardening
- Empty-state/loading behaviors
- Clear completion/failure/cancel terminal states
- Thread-safe UI updates and disposal behavior

Exit criteria:
- Users can watch an active run live and cancel safely with clear status transitions.

---

### Wave 3 — Notification and diagnostics refinement

Primary goal: reduce noise while improving post-run insight quality.

7) Notification payload quality pass
- Improve completion/failure summaries from structured execution diagnostics
- Ensure dedupe/rate-limit behavior remains predictable under burst runs
- Keep notification preferences/overrides authoritative

8) History/details parity with progress telemetry
- Ensure final history/details match live run counters and statuses
- Add tests for telemetry-to-persistence consistency

Exit criteria:
- Users receive actionable notifications and consistent run data across live and historical views.

---

### Wave 4 — Concurrency safety and scheduling hardening

Primary goal: safely support higher parallelism and avoid destructive contention.

9) Path conflict detection (pre-run guardrails)
- Detect unsafe overlapping source/destination combinations across concurrently runnable jobs
- Block or defer unsafe starts with explicit diagnostics

10) Concurrency policy enforcement
- Enforce max concurrent jobs with deterministic queueing/selection behavior
- Add visibility into pending/running states

11) Misfire/recovery resilience pass
- Validate trigger behavior around app restarts and missed schedules
- Ensure scheduler global toggle and per-job enable semantics remain consistent

Exit criteria:
- Concurrent scheduling is predictable, and unsafe path collisions are prevented before damage can occur.

---

### Wave 5 — Exclusion and filtering enhancements

Primary goal: improve practical control over what is synced.

12) Ignore-file support (`.archiveignore` / `.gitignore`-style)
- Define syntax scope and precedence with existing exclusion patterns
- Add parser + matching integration into sync walk

13) File size exclusion (optional threshold)
- Add per-job threshold setting and validation
- Include skip accounting/logging for excluded large files

14) Verification + UX updates
- Extend create/edit UX for new exclusion controls
- Add test coverage for mixed-rule evaluation order

Exit criteria:
- Users can express common real-world exclusion rules with predictable outcomes.

## Parallelization guidance

- Lane A (runtime/engine): items 1,2,3,9,10,11
- Lane B (desktop UX): items 4,5,6,14
- Lane C (observability): items 7,8
- Lane D (filtering rules): items 12,13

Recommended pairing:
- Wave 1: Lane A
- Wave 2: Lane B + A
- Wave 3: Lane C
- Wave 4: Lane A
- Wave 5: Lane D + B

## Suggested milestone checkpoints

- Milestone P5-M1: End of Wave 1 (telemetry foundation complete)
- Milestone P5-M2: End of Wave 2 (progress window usable)
- Milestone P5-M3: End of Wave 3 (notification/diagnostics consistency)
- Milestone P5-M4: End of Wave 4 (concurrency safety complete)
- Milestone P5-M5: End of Wave 5 (advanced exclusions complete)