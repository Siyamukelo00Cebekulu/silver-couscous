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
