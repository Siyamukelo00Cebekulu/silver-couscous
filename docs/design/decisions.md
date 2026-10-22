# Implementation decisions

## 2026-10-22: Use Spectre.Console for the CLI presentation layer

**Decision:** Add `Spectre.Console` to `GrandPrix.CLI` only and keep the
application, domain, and core projects free of UI dependencies.

**Context:** The requested interface needs navigable terminal screens, tables,
and a live race display. The repository's current CLI is plain `System.Console`
and clears the screen during race updates.

**Rationale:** Spectre.Console provides renderable layouts and live rendering
that fit the requested hub and timing tower. Restricting the package to the
CLI preserves the existing inward dependency direction and the
`netstandard2.1` targets of the reusable libraries. CLI input is kept behind
an application-owned input-source abstraction so sessions can be scripted.

**Trade-off:** The CLI gains an external package dependency. Redirected output
and input must continue to use a plain, non-interactive path.

## 2026-10-22: Replay accepted commands for deterministic career saves

**Decision:** Save an ordered, typed command history in a versioned JSON
envelope and restore a career by replaying it through the normal command
handlers. Persist the content schema and series identifiers with the history.

**Context:** Session state spans aggregates, calendar progress, inbox read
state, race-weekend data, and a partially advanced live race. Serializing
domain object graphs directly would couple the file format to implementation
details and make restoration bypass invariants.

**Rationale:** The application already uses seeded simulations and command
handlers. Replaying accepted commands rebuilds state through the same rules
and supports deterministic continuation during a race.

**Trade-off:** Replays depend on the same content schema and series provider;
incompatible identifiers are rejected rather than silently loaded. New
command shapes require a save migration or compatibility policy.

## 2026-10-22: Load initial season content as typed definitions

**Decision:** Store the default season as JSON, validate it through
`GrandPrix.Data`, and embed it into the data assembly for deployment.

**Rationale:** The CLI and tests can use identical validated content without
depending on the current working directory or the source checkout layout.
Series-owned schedule and scoring rules remain in the series provider.

**Trade-off:** Editing content requires rebuilding the data assembly; external
content override support can be added separately.
