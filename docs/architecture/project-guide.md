# Project and code guide

## What this project is

Grand Prix Team Principal is a .NET racing-management simulation. The player
manages a team through a five-round championship: advance the calendar, pay
staff, hire an engineer, improve facilities and the car, set up race weekends,
and earn championship points and prize money.

The primary playable entry point is the terminal application:

```powershell
dotnet run --project src\GrandPrix.CLI\GrandPrix.CLI.csproj
```

The game currently starts a new in-memory session on March 1, 2026. The CLI
does not load or save a game between runs.

## Repository map

| Location | Responsibility |
|---|---|
| `src\GrandPrix.CLI` | Main terminal interface: menu, input, rendering, and race controls. |
| `src\GrandPrix.Application` | Coordinates a game session and connects player actions to the domain. |
| `src\GrandPrix.Domain` | Game rules and state: teams, drivers, tracks, car development, staff, and race simulation. |
| `src\GrandPrix.Core` | Shared contracts and primitives such as commands, events, money, IDs, time, and random-number sources. |
| `src\GrandPrix.AI`, `src\GrandPrix.Data`, `src\GrandPrix.Persistence` | Separate assemblies referenced by the application for supporting capabilities. They are not needed to launch the current CLI flow. |
| `src\GrandPrix.Series.Abstractions`, `src\GrandPrix.Series.F1` | Series-related project boundaries; the F1 project depends on the series abstractions and domain. |
| `src\GrandPrix.Unity.Bridge`, `UnityProject` | Unity integration boundary and Unity client assets. The terminal game can be run independently. |
| `content` | Data organized by balance, localization, and series. |
| `tests` | Domain/session, race, persistence, and balance checks. |
| `tools` | Supporting command-line utilities such as the simulator and content validator. |

Most of the currently playable behavior is implemented in `GrandPrix.Domain`,
`GrandPrix.Application`, and `GrandPrix.CLI`. The project layout also includes
additional assembly boundaries for other clients and future integrations.

## Main classes and how they fit together

### Session and application

| Type | File | Role |
|---|---|---|
| `GameSession` | `src\GrandPrix.Application\GameSession.cs` | Creates the initial world and is the main coordinator for one playthrough. It owns the calendar, teams, drivers, player facilities, car design, race-weekend state, inbox, and championship scoring. It exposes operations used by the CLI, such as advancing time, running practice and qualifying, building race setups, and settling race results. |
| `IGameSession` | `src\GrandPrix.Application\IGameSession.cs` | Small application-facing contract for executing commands, advancing a day, and retrieving dashboard and inbox views. |
| `DashboardViewModel`, `InboxItemViewModel` | `src\GrandPrix.Application\IGameSession.cs` | Simple data shapes for presenting session information without exposing console rendering details. |
| `Program` (top-level statements) | `src\GrandPrix.CLI\Program.cs` | Starts a `GameSession`, presents the menu, reads player choices, calls session operations, and renders results. |
| `CommandParser` | `src\GrandPrix.CLI\Input\CommandParser.cs` | Converts text commands into CLI command representations for text-based input. |

`GameSession` is currently also the composition root: its constructor creates
the sample championship, registers command handlers, and wires events. It
uses deterministic IDs for seeded entities and seedable random sources for
simulation so runs can be reproduced when given the same seeds.

### Shared core building blocks

| Type | File | Role |
|---|---|---|
| `ICommand`, `ICommandHandler<TCommand>`, `ICommandBus` | `src\GrandPrix.Core\Abstractions\Commands.cs` | Contracts for representing player actions and routing them to handlers. |
| `CommandBus` | `src\GrandPrix.Core\Commands\CommandBus.cs` | Dispatches a command to its registered handler. When a command succeeds, it publishes the events returned by that handler. |
| `CommandResult` | `src\GrandPrix.Core\Common\CommandResult.cs` | Carries command success/failure information and any events produced by successful commands. |
| `IDomainEvent`, `IEventBus` | `src\GrandPrix.Core\Abstractions\DomainEvents.cs` | Contracts for announcing things that have happened in the game. |
| `InMemoryEventBus` | `src\GrandPrix.Core\Events\InMemoryEventBus.cs` | Delivers domain events to in-process subscribers. |
| `IClock`, `ManualGameClock` | `src\GrandPrix.Core\Abstractions\ClockAndRandom.cs`, `src\GrandPrix.Core\Simulation\ManualGameClock.cs` | Allow game time to advance by explicit player actions rather than wall-clock time. |
| `IRandom`, `SeededRandom` | `src\GrandPrix.Core\Abstractions\ClockAndRandom.cs`, `src\GrandPrix.Core\Simulation\SeededRandom.cs` | Provide repeatable random values and independent random streams for simulation. |
| `EntityId`, `Money` | `src\GrandPrix.Core\Common\EntityId.cs`, `src\GrandPrix.Core\Common\Money.cs` | Shared identity and money value types. Money is represented in integer cents. |

### Team management domain

| Type | File | Role |
|---|---|---|
| `Team` | `src\GrandPrix.Domain\Team\Team.cs` | Holds a team's budget, base payroll, engineers, and championship points. It validates deductions and calculates total monthly payroll. |
| `Driver` | `src\GrandPrix.Domain\Drivers\Driver.cs` | Stores driver attributes such as pace, consistency, tyre management, wet skill, racecraft, focus, and defending. Simulation uses these values to influence results. |
| `Engineer`, `EngineerCandidate` | `src\GrandPrix.Domain\Staff\Engineer.cs` | Represent a hired engineer and a market candidate, including skill and salary details. |
| `HireEngineerCommand`, `HireEngineerHandler` | `src\GrandPrix.Domain\Staff\HireEngineerCommand.cs`, `src\GrandPrix.Domain\Staff\HireEngineerHandler.cs` | Express and validate an engineer-hiring action. The handler checks the issuer, candidate, contract and salary limits, and payroll runway before changing the team. |
| `EngineerHiredEvent` | `src\GrandPrix.Domain\Staff\EngineerHiredEvent.cs` | Announces a successful hire; `GameSession` subscribes to add a message to the inbox. |
| `TeamFacilities` | `src\GrandPrix.Domain\Facilities\TeamFacilities.cs` | Tracks facility levels and upgrade costs/effects. |
| `CarDesign`, `DevelopmentProject` | `src\GrandPrix.Domain\Car\CarDevelopment.cs` | Track car performance, reliability, component life, cost-cap spend, and multi-step upgrades. Development can complete or be rejected by the cost cap. |

### Calendar and racing domain

| Type | File | Role |
|---|---|---|
| `RaceCircuit`, `CalendarRaceEvent` | `src\GrandPrix.Domain\Calendar\RaceCircuit.cs` | Hold circuit characteristics and scheduled/completed event state. |
| `Track` | `src\GrandPrix.Domain\Calendar\Track.cs` | Supplies track parameters used by the simulation. |
| `RaceWeekend` | `src\GrandPrix.Domain\Race\RaceWeekend.cs` | Stores the current event's practice, qualifying, strategy choices, and completion flags. |
| `RaceSetup`, `CarSetup`, `WeatherPoint` | `src\GrandPrix.Domain\Race\RaceSetup.cs` | Describe the grid, track/race parameters, each car's starting conditions, and weather forecast passed to the race engine. |
| `IRaceEngine` | `src\GrandPrix.Domain\Race\IRaceEngine.cs` | Contract for starting and advancing a race, submitting instructions, reading snapshots, and retrieving the final result. |
| `LapRaceEngine` | `src\GrandPrix.Domain\Race\LapRaceEngine.cs` | Live, multi-car race engine used by the CLI. It advances elapsed race time, manages per-car runtime state, applies queued pit/driver-mode commands, and emits race events. |
| `RaceSnapshot`, `RaceResult` | `src\GrandPrix.Domain\Race\RaceSnapshot.cs`, `src\GrandPrix.Domain\Race\IRaceEngine.cs` | Represent current live timing and final classification/event history. |
| `RaceEvent` and event subclasses | `src\GrandPrix.Domain\Race\RaceEvents.cs` | Describe lap completion, overtakes, pit stops, incidents, safety-car periods, and race completion for display or analysis. |
| `IRaceCommand`, `BoxThisLapCommand`, `SetDriverModeCommand` | `src\GrandPrix.Domain\Race\RaceCommands.cs` | Represent player instructions to pit or change a car's pace mode during a live race. |
| `PracticeSimulator`, `QualifyingSimulator`, `RaceSimulator`, `LapCalculator` | `src\GrandPrix.Domain\Race\Simulation` | Standalone simulation helpers. The CLI's main session flow coordinates practice and qualifying through `GameSession`, then runs the live multi-car race with `LapRaceEngine`. |

There is also a `RaceSimulator` (`src\GrandPrix.Domain\Race\Simulation\RaceSimulator.cs`)
for a simpler single-driver, strategy-plan simulation. It is separate from the
live multi-car engine used by the CLI.

## How a playthrough works

1. **Start:** `Program` creates `GameSession`. The session seeds the player
   team, rival teams, drivers, five circuits, calendar, facilities, and staff
   market.
2. **Manage:** Menu actions display session state or call operations for
   hiring, upgrading facilities, advancing car development, and advancing the
   game clock. The CLI handles input and display; the session/domain own the
   state and rules.
3. **Advance time:** The manual clock raises a day-advanced notification.
   `GameSession` processes monthly payroll on the first day of each month,
   deducting it from solvent teams and recording messages/events.
4. **Prepare for an event:** `RunPractice` applies a selected focus and
   calculates practice/long-run feedback. `RunQualifying` requires practice to
   be complete, then calculates lap times and establishes the grid using
   driver, tyre, risk, run-count, setup, and random factors.
5. **Race:** `CreateRaceSetup` builds the cars and conditions for the next
   circuit. `LapRaceEngine.Start` initializes car state; the CLI advances the
   engine and presents snapshots. Player race instructions are submitted as
   race commands, and the engine produces events as laps and incidents occur.
6. **Settle:** `GameSession.SettleRace` maps finishing cars to teams and
   drivers, awards points and prize money, and updates budgets. The session
   marks the event complete and opens the next weekend.

Command-based actions follow a more specific path: the application calls
`GameSession.Execute`, the `CommandBus` finds the registered handler, and the
handler validates and applies the action. Successful results publish their
domain events through the event bus; subscribers can then react without the
handler needing to know about UI details.

## Build and test

From the repository root:

```powershell
dotnet build GrandPrixTeamPrincipal.sln
dotnet run --project tests\GrandPrix.Race.Tests\GrandPrix.Race.Tests.csproj
dotnet test GrandPrixTeamPrincipal.sln
```

For controls and player-facing instructions, see
[`docs/design/quick-start.md`](../design/quick-start.md). For game rules and
design goals, see [`docs/design/game-design.md`](../design/game-design.md).
