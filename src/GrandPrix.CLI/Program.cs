using GrandPrix.Application;
using GrandPrix.Core.Common;
using GrandPrix.Core.Simulation;
using GrandPrix.Domain.Car;
using GrandPrix.Domain.Race;
using GrandPrix.Domain.Staff;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var session = new GameSession();
var menuItems = new[]
{
    ("Dashboard", "status"),
    ("Season calendar", "calendar"),
    ("Team and facilities", "team"),
    ("Driver profiles", "drivers"),
    ("Hire engineer", "staff"),
    ("Upgrade facilities", "facilities"),
    ("Car development", "development"),
    ("Team inbox", "inbox"),
    ("Race weekend", "weekend"),
    ("Advance to next event", "advance"),
    ("Help", "help"),
    ("Exit", "exit")
};
var running = true;
var selectedMenuItem = 0;
while (running)
{
    selectedMenuItem = DrawMenu(session, menuItems, selectedMenuItem);
    var choice = ReadMenuChoice(menuItems, ref selectedMenuItem);
    switch (choice)
    {
        case "status":
            PrintDashboard(session);
            break;
        case "calendar":
            PrintCalendar(session);
            break;
        case "team":
            PrintTeam(session);
            break;
        case "drivers":
            PrintDrivers(session);
            break;
        case "staff":
            ManageStaff(session);
            break;
        case "facilities":
            ManageFacilities(session);
            break;
        case "development":
            ManageDevelopment(session);
            break;
        case "inbox":
            PrintInbox(session);
            break;
        case "weekend":
            RunWeekend(session);
            break;
        case "advance":
            AdvanceToNextEvent(session);
            break;
        case "help":
            PrintHelp();
            break;
        case "exit":
            running = false;
            break;
    }
    if (running)
        WaitForMenuReturn();
}

static int DrawMenu(GameSession session, IReadOnlyList<(string Label, string Command)> menuItems, int selected)
{
    if (!Console.IsOutputRedirected)
        Console.Clear();
    var dashboard = session.GetDashboard();
    Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
    Console.WriteLine($"║ GRAND PRIX TEAM PRINCIPAL  |  {session.Today:ddd dd MMM yyyy}                         ║");
    Console.WriteLine($"║ {session.PlayerTeam.Name,-24} Cash {GameSession.FormatMoney(new Money(dashboard.BudgetRemainingCents)),12}  ║");
    Console.WriteLine($"║ Next: {session.NextEvent.Circuit.Name,-28} {session.NextEvent.Date:dd MMM yyyy}  ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
    Console.WriteLine("Use ↑/↓ or W/S to navigate, Enter to select, number keys as shortcuts.");
    for (var index = 0; index < menuItems.Count; index++)
        Console.WriteLine($"{(index == selected ? " > " : "   ")}{index + 1,2}. {menuItems[index].Label}");
    return selected;
}

static string ReadMenuChoice(IReadOnlyList<(string Label, string Command)> menuItems, ref int selected)
{
    if (Console.IsInputRedirected)
    {
        Console.Write("team> ");
        var line = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(line) || line is "0" or "quit")
            return "exit";
        if (int.TryParse(line, out var number) && number >= 1 && number <= menuItems.Count)
            return menuItems[number - 1].Command;
        return line;
    }

    while (true)
    {
        var key = Console.ReadKey(intercept: true).Key;
        switch (key)
        {
            case ConsoleKey.UpArrow:
            case ConsoleKey.W:
                selected = (selected - 1 + menuItems.Count) % menuItems.Count;
                DrawMenuForKey(menuItems, selected);
                break;
            case ConsoleKey.DownArrow:
            case ConsoleKey.S:
                selected = (selected + 1) % menuItems.Count;
                DrawMenuForKey(menuItems, selected);
                break;
            case ConsoleKey.Enter:
                return menuItems[selected].Command;
            case ConsoleKey.Escape:
                return "exit";
            default:
                if (key >= ConsoleKey.D1 && key <= ConsoleKey.D9)
                {
                    var index = key - ConsoleKey.D1;
                    if (index < menuItems.Count)
                    {
                        selected = index;
                        return menuItems[index].Command;
                    }
                }
                if (key == ConsoleKey.D0 || key == ConsoleKey.NumPad0)
                    return "exit";
                break;
        }
    }
}

static void DrawMenuForKey(IReadOnlyList<(string Label, string Command)> menuItems, int selected)
{
    Console.SetCursorPosition(0, 6);
    for (var index = 0; index < menuItems.Count; index++)
        Console.WriteLine($"{(index == selected ? " > " : "   ")}{index + 1,2}. {menuItems[index].Label}       ");
}

static void WaitForMenuReturn()
{
    Console.WriteLine();
    if (Console.IsInputRedirected)
        return;
    Console.Write("Press any key to return to the menu...");
    Console.ReadKey(intercept: true);
}

static void PrintDashboard(GameSession session)
{
    var view = session.GetDashboard();
    Console.WriteLine($"\n{view.TeamName} | {view.Today:yyyy-MM-dd} | Championship P{view.ChampionshipPosition}");
    Console.WriteLine($"Cash {GameSession.FormatMoney(new Money(view.BudgetRemainingCents))} | " +
                      $"Payroll {GameSession.FormatMoney(session.PlayerTeam.MonthlyPayroll)}/month | " +
                      $"{session.PlayerTeam.Budget.Cents / Math.Max(1, session.PlayerTeam.MonthlyPayroll.Cents)} months runway");
    Console.WriteLine($"Next: {session.NextEvent.Circuit.Name}, {session.NextEvent.Circuit.Country} on {session.NextEvent.Date:dd MMM yyyy}");
    Console.WriteLine($"Car: aero {session.PlayerCarDesign.Downforce:F0}, efficiency {session.PlayerCarDesign.DragEfficiency:F0}, " +
                      $"reliability {session.PlayerCarDesign.Reliability:F0}, component life {session.PlayerCarDesign.ComponentLifeLaps:F0} laps");
    if (session.CurrentDevelopment is { } project &&
        project.Stage is not (DevelopmentStage.Complete or DevelopmentStage.Rejected))
        Console.WriteLine($"Active project: {project.Focus}, {project.Stage} step {project.Progress}/3.");
}

static void PrintCalendar(GameSession session)
{
    Console.WriteLine("\nSEASON CALENDAR");
    foreach (var item in session.Calendar)
        Console.WriteLine($"{(item.Completed ? "[DONE]" : item == session.NextEvent ? "[NEXT]" : "[    ]")} " +
                          $"{item.Date:dd MMM yyyy} | {item.Circuit.Name,-30} {item.Circuit.Country,-14} " +
                          $"{item.Circuit.RaceLaps} laps | {item.Circuit.Characteristic}");
}

static void PrintTeam(GameSession session)
{
    Console.WriteLine($"\n{session.PlayerTeam.Name} | founded for the 2026 championship");
    Console.WriteLine($"Budget: {GameSession.FormatMoney(session.PlayerTeam.Budget)}");
    Console.WriteLine($"Base payroll: {GameSession.FormatMoney(session.PlayerTeam.MonthlyBasePayroll)} monthly");
    Console.WriteLine($"Total payroll: {GameSession.FormatMoney(session.PlayerTeam.MonthlyPayroll)} monthly");
    Console.WriteLine($"Engineers: {session.PlayerTeam.Engineers.Count}");
    foreach (var engineer in session.PlayerTeam.Engineers)
        Console.WriteLine($"  {engineer.Name} | strategy skill {engineer.Skill} | " +
                          $"{GameSession.FormatMoney(engineer.MonthlySalary)}/month | {engineer.ContractYears}-year contract");
    Console.WriteLine($"Facilities: wind tunnel L{session.Facilities.WindTunnelLevel}, simulator L{session.Facilities.SimulatorLevel}, factory L{session.Facilities.FactoryLevel}");
    Console.WriteLine($"Cost cap: {GameSession.FormatMoney(new Money(session.PlayerCarDesign.CostCapSpentCents))} / " +
                      $"{GameSession.FormatMoney(new Money(CarDesign.CostCapLimitCents))}");
}

static void PrintDrivers(GameSession session)
{
    Console.WriteLine("\nDRIVER PROFILES");
    for (var index = 0; index < session.Drivers.Count; index++)
    {
        var driver = session.Drivers[index];
        var team = session.Teams[index / 2];
        Console.WriteLine($"{driver.Name,-18} {team.Name,-22} Pace {driver.Pace,2} | Racecraft {driver.Racecraft,2} | " +
                          $"Focus {driver.Focus,2} | Consistency {driver.Consistency,2} | Tyres {driver.TyreManagement,2}");
        Console.WriteLine($"  Wet skill {driver.WetSkill,2} | Experience {driver.Experience,2} | Feedback {driver.Feedback,2} | " +
                          $"Aggression {driver.Aggression,2} | Defending {driver.Defending,2}");
    }
}

static void ManageStaff(GameSession session)
{
    Console.WriteLine("\nENGINEER MARKET");
    for (var index = 0; index < session.EngineerCandidates.Count; index++)
    {
        var candidate = session.EngineerCandidates[index];
        var alreadyHired = session.PlayerTeam.Engineers.Any(engineer => engineer.Id == candidate.Id);
        Console.WriteLine($"{index + 1}. {candidate.Name,-16} strategy skill {candidate.Skill,2} | " +
                          $"{GameSession.FormatMoney(candidate.AskingAnnualSalary)}/year" +
                          (alreadyHired ? " | HIRED" : string.Empty));
    }
    Console.WriteLine("Your engineers advise on pit windows and tyre calls during every race.");
    Console.Write("Candidate number to hire (Enter to cancel)> ");
    if (!int.TryParse(Console.ReadLine(), out var number) ||
        number < 1 || number > session.EngineerCandidates.Count)
        return;

    var selected = session.EngineerCandidates[number - 1];
    var result = session.Execute(new HireEngineerCommand(
        session.PlayerTeam.Id,
        selected.Id,
        selected.AskingAnnualSalary,
        3));
    Console.WriteLine(result.Succeeded
        ? $"Hired {selected.Name} for {GameSession.FormatMoney(selected.AskingAnnualSalary)}/year on a 3-year contract."
        : $"Hire rejected: {result.Error}");
}

static PracticeFocus ChoosePracticeFocus()
{
    if (Console.IsInputRedirected)
        return PracticeFocus.Setup;
    Console.WriteLine("\nChoose practice focus: 1 Setup, 2 Tyre evaluation, 3 Race simulation.");
    Console.Write("practice focus> ");
    return Console.ReadLine()?.Trim() switch
    {
        "2" => PracticeFocus.TyreEvaluation,
        "3" => PracticeFocus.RaceSimulation,
        _ => PracticeFocus.Setup
    };
}

static TyreCompoundType ChooseQualifyingTyre()
{
    if (Console.IsInputRedirected)
        return TyreCompoundType.Soft;
    Console.WriteLine("\nQualifying tyre: 1 Soft (fastest), 2 Medium (balanced), 3 Hard (durable).");
    Console.Write("qualifying tyre> ");
    return Console.ReadLine()?.Trim() switch
    {
        "2" => TyreCompoundType.Medium,
        "3" => TyreCompoundType.Hard,
        _ => TyreCompoundType.Soft
    };
}

static QualiRisk ChooseQualifyingRisk()
{
    if (Console.IsInputRedirected)
        return QualiRisk.Normal;
    Console.WriteLine("Qualifying risk: 1 Safe, 2 Normal, 3 Push.");
    Console.Write("qualifying risk> ");
    return Console.ReadLine()?.Trim() switch
    {
        "1" => QualiRisk.Safe,
        "3" => QualiRisk.Push,
        _ => QualiRisk.Normal
    };
}

static int ChooseQualifyingRuns()
{
    if (Console.IsInputRedirected)
        return 2;
    Console.WriteLine("Qualifying runs: 1–5 (more runs give extra attempts but cost more time).");
    Console.Write("runs> ");
    return int.TryParse(Console.ReadLine(), out var runs) ? Math.Clamp(runs, 1, 5) : 2;
}

static void ManageFacilities(GameSession session)
{
    Console.WriteLine("\nFACILITIES");
    Console.WriteLine($"Wind tunnel L{session.Facilities.WindTunnelLevel} | Simulator L{session.Facilities.SimulatorLevel} | Factory L{session.Facilities.FactoryLevel}");
    Console.WriteLine("Upgrades cost from $200,000 and increase with level. Choose: wind, simulator, factory, or back.");
    Console.Write("facility> ");
    var facility = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(facility) || facility.Equals("back", StringComparison.OrdinalIgnoreCase))
        return;
    try
    {
        var cost = session.Facilities.UpgradeCost(facility);
        session.UpgradeFacility(facility);
        Console.WriteLine($"Facility upgraded for {GameSession.FormatMoney(cost)}.");
    }
    catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
    {
        Console.WriteLine($"Upgrade unavailable: {exception.Message}");
    }
}

static void ManageDevelopment(GameSession session)
{
    var design = session.PlayerCarDesign;
    Console.WriteLine("\nCAR DEVELOPMENT");
    Console.WriteLine($"Aero {design.Downforce:F0} | efficiency {design.DragEfficiency:F0} | reliability {design.Reliability:F0} | " +
                      $"life {design.ComponentLifeLaps:F0} laps | cost cap {GameSession.FormatMoney(new Money(design.CostCapSpentCents))}");
    if (session.CurrentDevelopment is { Stage: not (DevelopmentStage.Complete or DevelopmentStage.Rejected) } project)
    {
        Console.WriteLine($"Project: {project.Focus} | {project.Stage} (stage actions take three work steps).");
        Console.Write("Advance project? (y/n)> ");
        if (!string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            return;
        var result = session.AdvanceDevelopment();
        Console.WriteLine(result.Message);
        return;
    }

    Console.WriteLine("Start project: aero, chassis, reliability. Enter budget in whole dollars (minimum 100000).");
    Console.Write("focus> ");
    var focusInput = Console.ReadLine()?.Trim().ToLowerInvariant();
    var focus = focusInput switch
    {
        "aero" or "aerodynamics" => DevelopmentFocus.Aerodynamics,
        "chassis" => DevelopmentFocus.Chassis,
        "reliability" => DevelopmentFocus.Reliability,
        _ => (DevelopmentFocus?)null
    };
    if (focus is null)
    {
        Console.WriteLine("No project started.");
        return;
    }
    Console.Write("budget dollars> ");
    if (!decimal.TryParse(Console.ReadLine(), out var dollars) || dollars < 100_000 || dollars > long.MaxValue / 100m)
    {
        Console.WriteLine("Enter a valid budget of at least $100,000.");
        return;
    }
    try
    {
        var result = session.StartDevelopment(focus.Value,
            new Money(checked((long)decimal.Round(dollars * 100m, 0))));
        Console.WriteLine(result.Message);
        Console.WriteLine("Return to Car development and advance each stage as the work is completed.");
    }
    catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
    {
        Console.WriteLine($"Project not started: {exception.Message}");
    }
}

static void PrintInbox(GameSession session)
{
    Console.WriteLine("\nTEAM INBOX");
    if (session.Inbox.Count == 0)
    {
        Console.WriteLine("No messages.");
        return;
    }
    foreach (var message in session.Inbox)
        Console.WriteLine($"• {message}");
}

static void RunWeekend(GameSession session)
{
    var weekend = session.StartWeekend();
    var circuit = session.NextEvent.Circuit;
    Console.WriteLine($"\nRACE WEEKEND: {circuit.Name} | {circuit.Country} | {circuit.RaceLaps} laps");
    if (!weekend.PracticeComplete)
    {
        var focus = ChoosePracticeFocus();
        Console.WriteLine($"\nPRACTICE — selected focus: {focus}");
        foreach (var result in session.RunPractice(
                     2026 + session.Calendar.Count(item => item.Completed), focus))
            Console.WriteLine($"{result.DriverName,-18} best {result.BestLapSeconds,7:F3}s | long run {result.LongRunLapSeconds,7:F3}s | " +
                              $"wear {result.TyreWearPerLap:P1} | {result.EngineerNote}");
    }
    if (!weekend.QualifyingComplete)
    {
        var tyre = ChooseQualifyingTyre();
        var risk = ChooseQualifyingRisk();
        var runs = ChooseQualifyingRuns();
        Console.WriteLine($"\nQUALIFYING — {tyre} tyres | {risk} risk | {runs} run(s)");
        foreach (var result in session.RunQualifying(
                     90210 + session.Calendar.Count(item => item.Completed), tyre, risk, runs))
            Console.WriteLine($"P{result.GridPosition,2} #{result.CarNumber,-2} {result.DriverName,-18} {result.LapTimeSeconds:F3}s");
    }
    if (weekend.RaceComplete)
    {
        Console.WriteLine("This weekend is already complete.");
        return;
    }

    Console.WriteLine("\nGRAND PRIX — live timing begins shortly. Space pauses; Q fast-forwards.");
    Console.WriteLine("Arrows/1/2 select your car | A accepts strategy call | T selects pit tyre | P boxes | M changes pace.");
    if (!Console.IsInputRedirected)
        Thread.Sleep(700);
    var setup = session.CreateRaceSetup(circuit.RaceLaps, 42 + session.Calendar.Count(item => item.Completed));
    var engine = new LapRaceEngine();
    engine.Start(setup, new SeededRandom(setup.Seed));
    var oldPositions = engine.GetSnapshot().Cars.ToDictionary(car => car.CarId, car => car.Position);
    var snapshot = engine.GetSnapshot();
    var eventFeed = new List<string>();
    var paused = false;
    var fastForward = false;
    var selectedCarNumber = 11;
    RaceTyreCompound? selectedPitTyre = null;
    var advice = GetPlayerAdvice(session, snapshot);
    DrawRaceFrame(circuit.Name, snapshot, oldPositions, eventFeed, advice, paused, selectedCarNumber, selectedPitTyre);
    while (!engine.IsFinished)
    {
        if (!Console.IsInputRedirected)
        {
            var inputReceived = false;
            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true).Key;
                inputReceived = true;
                if (key == ConsoleKey.Spacebar)
                {
                    paused = !paused;
                }
                else if (key == ConsoleKey.Q)
                {
                    fastForward = true;
                    paused = false;
                }
                else if (key is ConsoleKey.UpArrow or ConsoleKey.LeftArrow or ConsoleKey.D1)
                {
                    selectedCarNumber = 11;
                }
                else if (key is ConsoleKey.DownArrow or ConsoleKey.RightArrow or ConsoleKey.D2)
                {
                    selectedCarNumber = 12;
                }
                else if (key == ConsoleKey.T)
                {
                    selectedPitTyre = NextPitTyre(selectedPitTyre);
                    eventFeed.Add($"Pit menu: selected {selectedPitTyre} for car #{selectedCarNumber}.");
                }
                else if (key == ConsoleKey.P)
                {
                    QueuePit(engine, snapshot, selectedCarNumber,
                        selectedPitTyre ?? advice[selectedCarNumber].SuggestedTyre, eventFeed);
                }
                else if (key == ConsoleKey.A)
                {
                    var call = advice[selectedCarNumber];
                    if (call.RecommendPit)
                        QueuePit(engine, snapshot, selectedCarNumber, call.SuggestedTyre, eventFeed);
                    else
                        eventFeed.Add($"Strategy: keep car #{selectedCarNumber} out; no stop is recommended yet.");
                }
                else if (key == ConsoleKey.M)
                {
                    var car = snapshot.Cars.FirstOrDefault(item => item.CarNumber == selectedCarNumber);
                    if (car is not null)
                    {
                        var mode = NextDriverMode(car.Mode);
                        engine.Submit(new SetDriverModeCommand(car.CarId, mode));
                        eventFeed.Add($"Pit wall: car #{selectedCarNumber} instructed to {mode}.");
                    }
                }
            }
            if (inputReceived && !fastForward)
                DrawRaceFrame(circuit.Name, snapshot, oldPositions, eventFeed, advice, paused, selectedCarNumber, selectedPitTyre);
            while (paused && !fastForward)
            {
                Thread.Sleep(100);
                if (!Console.KeyAvailable)
                    continue;
                var key = Console.ReadKey(intercept: true).Key;
                if (key == ConsoleKey.Spacebar)
                {
                    paused = false;
                    DrawRaceFrame(circuit.Name, snapshot, oldPositions, eventFeed, advice, paused, selectedCarNumber, selectedPitTyre);
                }
                else if (key == ConsoleKey.Q)
                {
                    fastForward = true;
                    paused = false;
                }
                else if (key is ConsoleKey.UpArrow or ConsoleKey.LeftArrow or ConsoleKey.D1)
                    selectedCarNumber = 11;
                else if (key is ConsoleKey.DownArrow or ConsoleKey.RightArrow or ConsoleKey.D2)
                    selectedCarNumber = 12;
            }
            if (!fastForward)
                Thread.Sleep(1200);
        }
        else
        {
            Thread.Sleep(1200);
        }

        var events = engine.AdvanceOneLap();
        foreach (var description in events.Select(item => item.Description).Distinct())
            eventFeed.Add(description);
        if (eventFeed.Count > 5)
            eventFeed.RemoveRange(0, eventFeed.Count - 5);
        snapshot = engine.GetSnapshot();
        advice = GetPlayerAdvice(session, snapshot);
        DrawRaceFrame(circuit.Name, snapshot, oldPositions, eventFeed, advice, paused, selectedCarNumber, selectedPitTyre);
        oldPositions = snapshot.Cars.ToDictionary(car => car.CarId, car => car.Position);
    }
    var payouts = session.SettleRace(setup, engine.GetResult());
    session.CompleteNextRace();
    Console.WriteLine("\nFINAL CLASSIFICATION");
    PrintStandings(engine.GetSnapshot(), oldPositions);
    Console.WriteLine("Prize payouts:");
    foreach (var payout in payouts.Where(item => item.Amount.Cents > 0))
        Console.WriteLine($"  {payout.TeamName,-22} {GameSession.FormatMoney(payout.Amount)}" +
                          (payout.IsPlayer ? "  <- your team" : string.Empty));
    Console.WriteLine($"Team cash: {GameSession.FormatMoney(session.PlayerTeam.Budget)}");
}

static void PrintStandings(RaceSnapshot snapshot, IReadOnlyDictionary<EntityId, int> previousPositions)
{
    Console.WriteLine($"Lap {snapshot.CurrentLap} | race clock {snapshot.RaceTime:mm\\:ss}");
    Console.WriteLine("Pos  Move  No  Driver             Team                  Tyre       Wear   Gap");
    foreach (var car in snapshot.Cars)
    {
        var previous = previousPositions.TryGetValue(car.CarId, out var position) ? position : car.Position;
        var marker = car.Position < previous ? "▲" : car.Position > previous ? "▼" : "—";
        var gap = car.Position == 1 ? "LEADER" : $"+{car.GapToLeaderSec:F1}s";
        Console.WriteLine($"{car.Position,3} {marker,4} {car.CarNumber,3}  {car.DriverName,-17} " +
                          $"{car.TeamName,-21} {car.Tyre,-10} {car.TyreWear,5:P0} {gap,8}");
    }
}

static Dictionary<int, RaceStrategyAdvice> GetPlayerAdvice(GameSession session, RaceSnapshot snapshot) =>
    new[]
    {
        session.GetTyreAdvice(snapshot, 11),
        session.GetTyreAdvice(snapshot, 12)
    }.Select((advice, index) => (Advice: advice, CarNumber: index + 11))
        .ToDictionary(item => item.CarNumber, item => item.Advice);

static RaceTyreCompound NextPitTyre(RaceTyreCompound? current) => current switch
{
    null => RaceTyreCompound.Soft,
    RaceTyreCompound.Soft => RaceTyreCompound.Medium,
    RaceTyreCompound.Medium => RaceTyreCompound.Hard,
    _ => RaceTyreCompound.Soft
};

static DriverMode NextDriverMode(DriverMode current) => current switch
{
    DriverMode.Standard => DriverMode.Push,
    DriverMode.Push => DriverMode.SaveTyres,
    DriverMode.SaveTyres => DriverMode.SaveFuel,
    _ => DriverMode.Standard
};

static void QueuePit(
    LapRaceEngine engine,
    RaceSnapshot snapshot,
    int carNumber,
    RaceTyreCompound tyre,
    ICollection<string> eventFeed)
{
    var car = snapshot.Cars.FirstOrDefault(item => item.CarNumber == carNumber && item.TeamName == "Apex GP");
    if (car is null || car.Retired)
    {
        eventFeed.Add($"Pit wall: car #{carNumber} is not available for a pit instruction.");
        return;
    }
    engine.Submit(new BoxThisLapCommand(car.CarId, tyre, 0));
    eventFeed.Add($"Pit wall: car #{carNumber} will box this lap for {tyre} tyres.");
}

static void DrawRaceFrame(
    string circuitName,
    RaceSnapshot snapshot,
    IReadOnlyDictionary<EntityId, int> previousPositions,
    IReadOnlyList<string> eventFeed,
    IReadOnlyDictionary<int, RaceStrategyAdvice> advice,
    bool paused,
    int selectedCarNumber,
    RaceTyreCompound? selectedPitTyre)
{
    if (!Console.IsOutputRedirected)
        Console.Clear();
    Console.WriteLine($"LIVE GRAND PRIX  |  {circuitName}");
    Console.WriteLine($"Lap {snapshot.CurrentLap}  |  Race clock {snapshot.RaceTime:mm\\:ss}  |  " +
                      $"{(paused ? "PAUSED — Space resumes" : "Space: pause  Q: fast-forward")}");
    Console.WriteLine($"Selected car #{selectedCarNumber} | Pit tyre: {selectedPitTyre?.ToString() ?? "staff recommendation"} | " +
                      "Arrows/1/2 select  A advice  T tyre  P pit  M pace");
    Console.WriteLine("Pos  Move  No  Driver             Team                  Tyre       Wear   Gap");
    foreach (var car in snapshot.Cars)
    {
        var previous = previousPositions.TryGetValue(car.CarId, out var position) ? position : car.Position;
        var marker = car.Position < previous ? "▲" : car.Position > previous ? "▼" : "—";
        var gap = car.Position == 1 ? "LEADER" : $"+{car.GapToLeaderSec:F1}s";
        Console.WriteLine($"{car.Position,3} {marker,4} {car.CarNumber,3}  {car.DriverName,-17} " +
                          $"{car.TeamName,-21} {car.Tyre,-10} {car.TyreWear,5:P0} {gap,8}");
    }
    Console.WriteLine("\nTRACK RADIO");
    if (eventFeed.Count == 0)
        Console.WriteLine("Formation complete. Green flag!");
    else
        foreach (var message in eventFeed)
            Console.WriteLine($"• {message}");
    Console.WriteLine("\nSTRATEGY RADIO");
    foreach (var carNumber in new[] { 11, 12 })
        Console.WriteLine($"{(carNumber == selectedCarNumber ? "> " : "  ")}#{carNumber}: {advice[carNumber].Message}");
}

static void AdvanceToNextEvent(GameSession session)
{
    var eventDate = session.NextEvent.Date;
    while (session.Today < eventDate)
    {
        session.AdvanceDay();
        if (session.Today.Day == 1)
            Console.WriteLine($"{session.Today:yyyy-MM-dd}: payroll processed.");
    }
    Console.WriteLine($"Arrived at race week: {session.NextEvent.Circuit.Name} on {session.Today:dd MMM yyyy}.");
    PrintCalendar(session);
}

static void PrintHelp()
{
    Console.WriteLine("Use arrows/W/S and Enter to navigate; the menu includes staff hiring and race management.");
    Console.WriteLine("Practice: choose setup, tyre evaluation, or race simulation. Qualifying: choose tyre, risk, and runs.");
    Console.WriteLine("Race: arrows or 1/2 select your car; A accepts a staff tyre call, T chooses a tyre, P boxes, M changes pace.");
    Console.WriteLine("Races display each lap for about 1.2 seconds. Space pauses/resumes; Q fast-forwards.");
}
