# Running the playable console prototype

The console app targets .NET 9 and has no external package references.

From the repository root:

```powershell
dotnet run --project src\GrandPrix.CLI\GrandPrix.CLI.csproj
```

The app starts at the `team>` prompt. Try:

```text
status
market
hire 1
advance 31
race 12 42
```

`hire` accepts a market number, optional annual salary in dollars, and optional
contract term in years. For example, `hire 2 600000 3` offers $600,000 per year
for three years. Offers below the candidate's asking salary, above the 150%
negotiation ceiling, or leaving less than six months of payroll runway are
rejected. A one-month salary signing payment is deducted immediately; payroll
is paid on the first day of each in-game month.

Races use a reproducible seed. The defaults are 12 laps and seed 42. During a
race, use `next` to advance until every running car completes its next lap,
`standings` to show
positions and gaps, `pit <car-number> <tyre> [extra-delay-seconds]` to queue a
pit stop, and `pace <car-number> <push|standard|save-tyres|save-fuel>` to queue
a driving mode. `events` lists recent race events. For example:

```text
race 8 123
pit 11 hard
next
pace 11 push
next
```

Finish the race to see the classification, championship points, prize payout,
and updated team cash. Race timing, tyre wear, pace variation, and per-car
random streams are deterministic for a given setup and seed.

Run the package-free race and team-flow checks with:

```powershell
dotnet run --project tests\GrandPrix.Race.Tests\GrandPrix.Race.Tests.csproj
```

Run the existing framework-backed solution tests with:

```powershell
dotnet test GrandPrixTeamPrincipal.sln
```
