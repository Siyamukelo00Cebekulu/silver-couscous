using GrandPrix.Application;
using GrandPrix.CLI.Input;
using GrandPrix.Core.Common;
using GrandPrix.Core.Simulation;
using GrandPrix.Domain.Race;
using GrandPrix.Domain.Staff;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var session = new GameSession();
Console.WriteLine("GRAND PRIX TEAM PRINCIPAL");
Console.WriteLine("Build a team, manage the budget, and call the race strategy.");
PrintHelp();

var running = true;
while (running)
{
    Console.Write("\nteam> ");
    var line = Console.ReadLine();
    if (line is null)
        break;

    var command = CommandParser.Parse(line);
    switch (command.Kind)
    {
        case CliCommandKind.Empty:
            break;
        case CliCommandKind.Help:
            PrintHelp();
            break;
        case CliCommandKind.Status:
            PrintStatus(session);
            break;
        case CliCommandKind.Market:
            PrintMarket(session);
            break;
        case CliCommandKind.Hire:
            Hire(session, command);
            break;
        case CliCommandKind.Advance:
            AdvanceDays(session, command.Days == 0 ? 1 : command.Days);
            break;
        case CliCommandKind.Race:
            RunRace(session, command.Number, command.Seed);
            break;
        case CliCommandKind.Exit:
            running = false;
            break;
        case CliCommandKind.Invalid:
            Console.WriteLine(command.Error);
            break;
        default:
            Console.WriteLine("That command is only available during a race.");
            break;
    }
}

static void PrintHelp()
{
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  status                         Show budget, payroll, date, and championship position");
    Console.WriteLine("  market                         List fictional engineer candidates and asking salaries");
    Console.WriteLine("  hire <#> [salary] [years]      Hire at annual salary in dollars (default: asking salary)");
    Console.WriteLine("  advance [days]                 Advance the in-game calendar and process monthly payroll");
    Console.WriteLine("  race [laps] [seed]             Start a deterministic race (defaults: 12 laps, seed 42)");
    Console.WriteLine("  help                           Show this help");
    Console.WriteLine("  exit                           Quit");
    Console.WriteLine("During a race: next, standings, pit <car#> <tyre> [delay], pace <car#> <mode>, events, exit");
}

static void PrintStatus(GameSession session)
{
    var view = session.GetDashboard();
    Console.WriteLine($"{view.TeamName} | {view.Today:yyyy-MM-dd} | Championship P{view.ChampionshipPosition}");
    Console.WriteLine($"Cash: {GameSession.FormatMoney(new Money(view.BudgetRemainingCents))} | " +
                      $"Monthly payroll: {GameSession.FormatMoney(session.PlayerTeam.MonthlyPayroll)}");
    Console.WriteLine($"Payroll runway: {session.PlayerTeam.Budget.Cents / Math.Max(1, session.PlayerTeam.MonthlyPayroll.Cents)} months");
    Console.WriteLine($"Next event: {view.NextEvent}");
    foreach (var inboxItem in session.Inbox.TakeLast(3))
        Console.WriteLine($"  News: {inboxItem}");
}

static void PrintMarket(GameSession session)
{
    Console.WriteLine("Engineer market:");
    for (var i = 0; i < session.EngineerCandidates.Count; i++)
    {
        var candidate = session.EngineerCandidates[i];
        Console.WriteLine($"  {i + 1}. {candidate.Name,-16} skill {candidate.Skill,2} | " +
                          $"asking {GameSession.FormatMoney(candidate.AskingAnnualSalary)}/year");
    }
}

static void Hire(GameSession session, CliCommand command)
{
    if (command.Number < 1 || command.Number > session.EngineerCandidates.Count)
    {
        Console.WriteLine("Choose a valid engineer market number.");
        return;
    }

    var candidate = session.EngineerCandidates[command.Number - 1];
    var offer = command.SalaryDollars == 0
        ? candidate.AskingAnnualSalary
        : new Money(checked((long)decimal.Round(command.SalaryDollars * 100m, 0)));
    var result = session.Execute(new HireEngineerCommand(
        session.PlayerTeam.Id,
        candidate.Id,
        offer,
        command.ContractYears));

    Console.WriteLine(result.Succeeded
        ? $"Hired {candidate.Name} for {GameSession.FormatMoney(offer)}/year. Signing payment deducted."
        : $"Hire rejected: {result.Error}");
}

static void AdvanceDays(GameSession session, int days)
{
    for (var i = 0; i < days; i++)
    {
        session.AdvanceDay();
        Console.WriteLine($"Advanced to {session.Today:yyyy-MM-dd}.");
    }
    PrintStatus(session);
}

static void RunRace(GameSession session, int laps, int seed)
{
    RaceSetup setup;
    try
    {
        setup = session.CreateRaceSetup(laps, seed);
    }
    catch (ArgumentOutOfRangeException exception)
    {
        Console.WriteLine(exception.Message);
        return;
    }

    var engine = new LapRaceEngine();
    engine.Start(setup, new SeededRandom(seed));
    var raceEvents = new List<RaceEvent>();
    Console.WriteLine($"\n{setup.TrackId} Grand Prix | {laps} laps | seed {seed}");
    Console.WriteLine("Player cars: #11 Lena Hart and #12 Marco Bell.");
    Console.WriteLine("Use 'next' to advance one lap, or 'pit'/'pace' to queue an instruction.");
    PrintStandings(engine.GetSnapshot());

    while (!engine.IsFinished)
    {
        Console.Write("\nrace> ");
        var line = Console.ReadLine();
        if (line is null)
            return;

        var command = CommandParser.Parse(line);
        switch (command.Kind)
        {
            case CliCommandKind.Empty:
                continue;
            case CliCommandKind.Help:
                Console.WriteLine("Race commands: next | standings | pit <car#> <tyre> [delay] | pace <car#> <mode> | events | exit");
                continue;
            case CliCommandKind.Advance:
                var events = engine.AdvanceOneLap();
                raceEvents.AddRange(events);
                foreach (var raceEvent in events)
                    Console.WriteLine($"  {raceEvent.Description}");
                PrintStandings(engine.GetSnapshot());
                continue;
            case CliCommandKind.Standings:
                PrintStandings(engine.GetSnapshot());
                continue;
            case CliCommandKind.Pit:
                if (TryGetCar(engine.GetSnapshot(), command.CarNumber, out var pitCar))
                {
                    engine.Submit(new BoxThisLapCommand(pitCar.CarId, command.Tyre, command.DelaySeconds));
                    Console.WriteLine($"Pit stop queued for car #{command.CarNumber}: {command.Tyre}, " +
                                      $"{command.DelaySeconds:F1}s additional delay.");
                }
                else
                {
                    Console.WriteLine("No car has that number.");
                }
                continue;
            case CliCommandKind.Pace:
                if (TryGetCar(engine.GetSnapshot(), command.CarNumber, out var paceCar))
                {
                    engine.Submit(new SetDriverModeCommand(paceCar.CarId, command.Mode));
                    Console.WriteLine($"Pace instruction queued for car #{command.CarNumber}: {command.Mode}.");
                }
                else
                {
                    Console.WriteLine("No car has that number.");
                }
                continue;
            case CliCommandKind.Events:
                foreach (var raceEvent in raceEvents.TakeLast(12))
                    Console.WriteLine($"  {raceEvent.Description}");
                continue;
            case CliCommandKind.Exit:
                return;
            case CliCommandKind.Invalid:
                Console.WriteLine(command.Error);
                continue;
            default:
                Console.WriteLine("That command is not available during a race.");
                continue;
        }
    }

    var result = engine.GetResult();
    var payouts = session.SettleRace(setup, result);
    Console.WriteLine("\nFINAL CLASSIFICATION");
    PrintStandings(engine.GetSnapshot());
    Console.WriteLine("\nRace prize payouts:");
    foreach (var payout in payouts.Where(item => item.Amount.Cents > 0))
        Console.WriteLine($"  {payout.TeamName,-22} {GameSession.FormatMoney(payout.Amount)}" +
                          (payout.IsPlayer ? "  <- your team" : string.Empty));
    Console.WriteLine($"Cash after prize: {GameSession.FormatMoney(session.PlayerTeam.Budget)}");
}

static bool TryGetCar(RaceSnapshot snapshot, int carNumber, out CarState car)
{
    car = snapshot.Cars.FirstOrDefault(state => state.CarNumber == carNumber)!;
    return car is not null;
}

static void PrintStandings(RaceSnapshot snapshot)
{
    Console.WriteLine($"Lap {snapshot.CurrentLap} | race clock {snapshot.RaceTime:mm\\:ss}");
    Console.WriteLine("Pos  No  Driver             Team                  Tyre       Wear   Gap");
    foreach (var car in snapshot.Cars)
    {
        var gap = car.Position == 1 ? "LEADER" : $"+{car.GapToLeaderSec:F1}s";
        Console.WriteLine($"{car.Position,3} {car.CarNumber,3}  {car.DriverName,-17} " +
                          $"{car.TeamName,-21} {car.Tyre,-10} {car.TyreWear,5:P0} {gap,8}");
    }
}
