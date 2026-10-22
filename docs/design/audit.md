# Implementation audit

## Phase 0 baseline

The audit was performed against the authored source files, project references,
content tree, tests, and the project guide at
`docs/architecture/project-guide.md`.

- `dotnet build GrandPrixTeamPrincipal.sln --no-restore`: passed with 0 warnings
  and 0 errors.
- `dotnet test GrandPrixTeamPrincipal.sln --no-restore`: exited successfully;
  the seven `GrandPrix.Domain.Tests` tests passed. The Persistence and Balance
  test projects report that no tests are available. They need actual tests
  before they can provide the coverage their names imply.
- `dotnet run --project tests\GrandPrix.Race.Tests\GrandPrix.Race.Tests.csproj
  --no-restore`: all 17 executable checks passed.
- `dotnet run --project src\GrandPrix.CLI\GrandPrix.CLI.csproj` was exercised
  once using redirected input to display the current menu and exit. It starts
  a new session at 1 March 2026 and shows Apex GP with $42,000,000, Caspian
  Park as the next event, and twelve menu actions. The menu is implemented,
  but is not yet the requested hub-and-screen TUI.
- `ContentValidator` ran successfully and reported zero JSON files validated.
  `SimRunner` ran and simulated its hard-coded sample race.
- A whole-source search found no `NotImplementedException`, `TODO`, `FIXME`, or
  `NotSupportedException` markers. The missing work is primarily absent
  implementations and functionality, rather than marked placeholder bodies.

The current menu actions are:

| Menu action | Existing behavior |
|---|---|
| Dashboard | Prints team/date/position, budget and payroll runway, next event, car condition, and active development. Some values are read directly from domain objects. |
| Season calendar | Lists the five in-memory races and completion state. |
| Team and facilities | Prints budget, payroll, engineers, facility levels, and cost-cap spend. |
| Driver profiles | Prints driver attributes from domain objects. |
| Hire engineer | Lists the market, reads a number, and dispatches `HireEngineerCommand`; invalid selection cancels. |
| Upgrade facilities | Reads a facility name and upgrades it directly through the session; catches expected invalid-operation/argument errors for display. |
| Car development | Starts or advances a development project through session methods and prompts for focus/budget. |
| Team inbox | Prints all current messages; the view model currently marks every message unread. |
| Race weekend | Runs practice, qualifying, then a live race; race timing uses `LapRaceEngine`, supports pit/pace instructions, and settles standings and payouts. |
| Advance to next event | Advances one day at a time to the next race date and prints payroll notices at month boundaries. |
| Help | Prints keyboard, practice, qualifying, and race controls. |
| Exit | Ends the menu loop. Escape and zero also exit. |

On redirected input, race advancement still sleeps for about 1.2 seconds per
lap, and the race loop reads no commands. In interactive mode the UI redraws by
clearing the console, polls for key input, and offers pause/fast-forward but no
1x–16x speed selection. The main menu also waits for a key between actions in
interactive mode.

## Missing implementations and systems

No named stub body was found by marker search. The rows below identify files or
project boundaries where the requested type/system is absent, and the intended
role from the task and existing architecture.

| Missing type/system | File or project | Intended role |
|---|---|---|
| Save-store implementation and versioned session serializer/migrator | `src\GrandPrix.Core\Abstractions\ISaveStore.cs`; `src\GrandPrix.Persistence` (currently only project metadata and `.gitkeep`) | Persist complete session state, seed, calendar progress, and command history; load/migrate versioned saves. `ISaveStore` has no implementation in the source tree. |
| Game content models, loaders, and validation | `src\GrandPrix.Data` (currently empty); `content\**` (only `.gitkeep` files) | Load teams, drivers, circuits, staff market, and balance/rules from content, validate required fields, and replace hard-coded world data. |
| AI team principal and AI command policies | `src\GrandPrix.AI` (currently empty) | Have rival teams make game actions through the same command path as the player. |
| Series contracts and F1 rules provider | `src\GrandPrix.Series.Abstractions` and `src\GrandPrix.Series.F1` (currently empty) | Define series extension interfaces and move F1 points, calendar, prize money, and regulation constants behind them. |
| Unity-facing bridge | `src\GrandPrix.Unity.Bridge` (currently empty) | Adapt application/session view models and operations for a future Unity client without coupling Domain/Core to Unity. |
| Console app behavior | `src\GrandPrix.Console` (contains only a project file; not included in the solution) | Unclear: the runnable terminal client is `GrandPrix.CLI`; treat this unused project as an obsolete/placeholder boundary unless later repository evidence gives it a distinct purpose. |
| Content schema validation | `tools\ContentValidator\Program.cs` | The utility only parses JSON syntax. It does not validate schemas, required fields, or domain constraints, and currently sees no content files. |
| Content-driven sample simulation | `tools\SimRunner\Program.cs` | The current runner constructs a hard-coded track, driver, and strategy and runs the legacy single-driver `RaceSimulator`; it is useful as a smoke tool, not a season/balance runner. |
| Broader player/AI command handling | `src\GrandPrix.Application\GameSession.cs`; `src\GrandPrix.Domain\Facilities\TeamFacilities.cs`; `src\GrandPrix.Domain\Car\CarDevelopment.cs` | Only hiring currently uses `ICommand`/`CommandBus`. Facility, development, time, and race-weekend actions are invoked directly, so the required uniform command/event path and AI reuse are missing. |
| Screen-specific presentation models and application operations | `src\GrandPrix.Application\IGameSession.cs`; `src\GrandPrix.Application\GameSession.cs` | Only dashboard and inbox view models exist. Staff, facilities, development, calendar/standings, and race screens need application-facing view models/operations rather than CLI reads of domain state. |
| TUI shell, injectable I/O, and scripted interaction tests | `src\GrandPrix.CLI\Program.cs`; `src\GrandPrix.CLI\GrandPrix.CLI.csproj`; no CLI test project | Replace the large static menu/race loop with navigable views, visible errors/confirmations, redirected/EOF-safe operation, and testable console/input adapters. The CLI currently has no package references. |
| Calendar advance policies and interruption conditions | `src\GrandPrix.Application\GameSession.cs` | Only `AdvanceDay` and CLI-owned advance-to-event looping exist. Advance-week/next-event commands and stop-on-attention behavior are absent. |
| Contracts, morale, reliability failures, weather/safety-car strategy, rival poaching, additional staff roles, scouting/juniors, sprint formats, and replay | Domain/Application/Data/AI projects | These are the requested Phase 3 vertical slices; the current source has no corresponding complete game systems or UI paths. |

## Guide claims versus repository

| Guide statement | Observed state |
|---|---|
| `GameSession` seeds the world, registers handlers, and wires events | Accurate. Its constructor directly creates all sample teams, drivers, circuits, facilities, candidates, and the single hire handler. It is also the composition root. |
| CommandBus routes actions and publishes successful events | The core mechanism exists and is used for engineer hiring. Most other current management actions bypass it. |
| `LapRaceEngine` is the live multi-car race engine | Accurate. It advances a race, handles queued pit/mode commands, and records snapshots/events. Existing tests establish seeded repeatability and basic progression. |
| `RaceSimulator` is a separate simpler single-driver simulator | Accurate. It is used by `SimRunner`, while the CLI uses `LapRaceEngine`. Neither should be removed without checking the compatibility/test surface. |
| Supporting AI, Data, Persistence, Series, and Unity assemblies are project boundaries | Project files/references exist, but these projects contain no authored `.cs` implementation. |
| Tests cover persistence and balance | Test projects and package references exist, but `dotnet test` reports no available tests for both projects. |
| Content is organized by balance/localization/series | Directories exist, but contain only `.gitkeep`; no JSON or other game data exists. |
| The terminal UI is a thin renderer of application view models | Not currently true. `Program.cs` contains input, game-flow orchestration, race loop, strategy controls, and rendering; it reads teams, drivers, facilities, calendar, car design, and other domain objects directly. |
| Race step-size independence | Not currently verified. Race tests cover seeded races and time-based progress, but do not compare equivalent runs advanced in 0.25-second, 1-second, and whole-lap increments. |

## Proposed order of work

1. Preserve the current behavior as tests and establish command/event, time,
   and random-stream invariants; add the missing race step-size-independence
   check before relying on fast-forward/frame-based updates.
2. Add application view models and command handlers for existing management
   actions, plus a composition factory. Keep Core/Domain/Application on their
   existing `netstandard2.1` target and free of UI/Unity dependencies.
3. Build the CLI TUI shell and screen flows against those view models, with
   Spectre.Console restricted to `GrandPrix.CLI`, injectable input/output,
   plain redirected mode, bounded waits, and scripted interaction tests.
4. Implement persistence with versioning and exact save/load continuation
   tests; expose save/load in the CLI and autosave after settlement.
5. Implement typed content loading and validation, populate `content/`, then
   move the initial world configuration out of `GameSession`.
6. Introduce series abstractions and an F1 provider, then move scoring,
   calendar, payout, and regulation constants behind the provider.
7. Implement AI through the same command handlers and remove/relocate any
   race-engine behavior that is actually rival decision-making.
8. Add Phase 3 vertical slices in the task's priority order. Stop before
   starting a slice that cannot be completed through domain, application,
   view model, UI, and tests together.
9. Run solution build/tests, interactive and redirected CLI smoke runs, and
   balance simulations; update documentation and the audit after each phase.

## Assumptions and decisions to resolve during implementation

- The repository's actual project guide is
  `docs/architecture/project-guide.md`; the requested `docs/project-guide.md`
  path does not exist. Continue treating the existing guide as the Phase 0
  source of truth, then reconcile the requested documentation path explicitly.
- Keep the existing five fictional circuits and current seeded start as the
  default scenario while moving values to content. The five-round default
  must not become a hard-coded limit in new APIs.
- Keep both race simulation APIs until tests and call sites establish whether
  the legacy simulator is intentionally supported. Make any deprecation
  explicit rather than deleting it as part of unrelated work.
- Preserve existing public operations and game behavior while migrating
  actions to commands; where the task requires more explicit validation or
  stopping conditions, represent those as intentional behavior changes and
  add tests.
- The existing untracked `tests\GrandPrix.Domain.Tests\EngineerHiredEventTests.cs`
  was present before implementation and is not part of this audit change.
