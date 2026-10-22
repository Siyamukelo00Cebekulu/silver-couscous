# Grand Prix Team Principal

A compact, dependency-free .NET console racing-management simulation. Build a
team, manage its finances and facilities, develop a car within the cost cap, and
guide your drivers through a five-round championship.

## Requirements and launch

- .NET 9 SDK
- No external runtime packages

From the repository root:

```powershell
dotnet run --project src\GrandPrix.CLI\GrandPrix.CLI.csproj
```

The terminal menu accepts numbered selections or the text shortcuts shown
below. The game starts on March 1, 2026. Select **Advance to event** to move
through the calendar; payroll is charged on the first day of each month.

## Game features

- **Race weekends:** practice, qualifying, and an automatically advancing
  Grand Prix. Standings show ▲ for gained positions, ▼ for lost positions, and
  — when unchanged. Race timing, tyre wear, weather effects, and per-car
  randomness are seedable.
- **Race management:** race timing updates each lap at a readable pace, with
  live engineer/driver tyre calls. Select a car, accept a pit recommendation,
  choose a tyre, pit, or change driver pace from the keyboard.
- **Session strategy:** choose practice focus, qualifying tyre compound, risk,
  and number of runs. These choices affect setup, race pace, and grid position.
- **Engineer hiring:** recruit strategy engineers from the staff market; their
  skill informs race calls and their salary is part of team payroll.
- **Five circuits:** Caspian Park, Silverstone, Monza, Suzuka, and Marina Bay.
  Track length, tyre wear, pit loss, and overtaking difficulty vary by venue.
- **Driver profiles:** pace, consistency, tyre management, racecraft, focus,
  wet skill, experience, feedback, aggression, and defending affect performance.
- **Car development:** fund a wind-tunnel study, design, and build over several
  management turns. Aerodynamic gains trade against component lifespan and
  reliability; facility levels improve development, while the cost cap can
  reject an illegal package.
- **Facilities:** upgrade the wind tunnel, simulator, and factory. Costs rise
  with each level, up to level 5.
- **Team and inbox:** review finances, payroll, engineers, drivers, team
  facilities, and staff messages or upgrade recommendations.
- **Championship:** race results award points and prize money to teams and
  drivers.

## Terminal interface
`
In an interactive terminal, use the **Up/Down arrow keys** (or W/S) to move
through the menu and press **Enter** to select. Number keys are shortcuts;
Escape exits. When input is redirected, enter a menu number or command on a
line.

During a race, the live timing screen refreshes after each simulated lap with
standings, gaps, tyre wear, position-change indicators, and recent track radio
events. Each lap takes about 1.2 seconds. Use arrow keys to select car #11 or
#12, **A** to accept a staff pit call, **T** to choose a tyre, **P** to pit,
and **M** to cycle pace modes. Press **Space** to pause/resume or **Q** to
fast-forward to the finish.

## Menu and commands

| Menu | Shortcut | Purpose |
|---|---|---|
| 1 Dashboard | `status` | Budget, payroll runway, next event, and car status |
| 2 Calendar | `calendar` | View scheduled races and completed rounds |
| 3 Team | `team` | Team finances, engineers, facilities, and cost cap |
| 4 Drivers | `drivers` | Driver attribute profiles |
| 5 Hire engineer | `staff` | Recruit a strategy engineer from the market |
| 6 Facilities | `facilities` | Upgrade wind tunnel, simulator, or factory |
| 7 Car development | `development` | Start or advance a development project |
| 8 Inbox | `inbox` | Read staff and race-weekend messages |
| 9 Race weekend | `weekend` | Set session strategies and run the race |
| 10 Advance to event | `advance` | Advance the in-game date to the next race |
| 11 Exit | `exit` | Quit |

Development asks for a focus (`aero`, `chassis`, or `reliability`) and a budget
of at least $100,000. Re-enter **Car development** and confirm each work step
until the three project stages are complete.

Facilities ask for `wind`, `simulator`, or `factory`. All expenditures are
validated against the team's available budget.

## Build and tests

```powershell
dotnet build GrandPrixTeamPrincipal.sln
dotnet run --project tests\GrandPrix.Race.Tests\GrandPrix.Race.Tests.csproj
dotnet test GrandPrixTeamPrincipal.sln
```

The source tree separates Core abstractions, Domain simulation, Application
session orchestration, and the CLI. Additional persistence, series, AI, Unity
bridge, test, and tool projects are kept in their respective directories.
