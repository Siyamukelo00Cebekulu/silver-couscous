# Quick start

The console app targets .NET 9 and has no external package references.

From the repository root:

```powershell
dotnet run --project src\GrandPrix.CLI\GrandPrix.CLI.csproj
```

In an interactive terminal, navigate the on-screen menu with the Up/Down arrow
keys (or W/S), then press Enter. Number keys are shortcuts and Escape exits.
When standard input is redirected, the CLI accepts a menu number or command
name on a line (`status`, `calendar`, `team`, `drivers`, `facilities`,
`development`, `inbox`, `weekend`, `advance`, `help`, or `exit`).

Race weekends automatically run practice, qualifying, and the full race; there
is no `next` command required. The race screen refreshes live after each lap,
showing standings, gaps, tyre wear, position-change markers, and recent track
radio events. Press Space to pause/resume or Q to fast-forward to the finish.

The 2026 calendar contains five fictional race venues. `advance` moves the date
to the next event and processes monthly payroll on the first day of each month.
Practice provides setup and long-run data, and qualifying establishes the
starting grid used by the race.

Car development requires a chosen focus and a budget of at least $100,000. Each
project progresses through wind tunnel, design, and build, requiring three work
steps per stage. Aerodynamic improvements affect pace but can reduce component
lifespan and reliability. The cost cap is checked when a package is completed.
Wind tunnel, simulator, and factory upgrades improve development or practice;
their costs increase with facility level.

Driver profiles include pace, consistency, tyre management, racecraft, focus,
wet skill, experience, feedback, aggression, and defending. These affect race
pace, variability, tyre wear, wet-weather losses, and close-running performance.

Run the dependency-free race and team-flow checks with:

```powershell
dotnet run --project tests\GrandPrix.Race.Tests\GrandPrix.Race.Tests.csproj
```

Run the framework-backed solution tests with:

```powershell
dotnet test GrandPrixTeamPrincipal.sln
```
