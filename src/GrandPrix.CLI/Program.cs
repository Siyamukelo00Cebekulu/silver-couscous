using GrandPrix.Application;
using GrandPrix.Core.Common;
using GrandPrix.Core.Simulation;
using GrandPrix.Domain.Car;
using GrandPrix.Domain.Race;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var session = new GameSession();
var menuItems = new[]
{
    ("Dashboard", "status"),
    ("Season calendar", "calendar"),
    ("Team and facilities", "team"),
    ("Driver profiles", "drivers"),
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
        Console.WriteLine($"  {engineer.Name} | {GameSession.FormatMoney(engineer.MonthlySalary)}/month | {engineer.ContractYears}-year contract");
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
        Console.WriteLine("\nPRACTICE");
        foreach (var result in session.RunPractice(2026 + session.Calendar.Count(item => item.Completed)))
            Console.WriteLine($"{result.DriverName,-18} best {result.BestLapSeconds,7:F3}s | long run {result.LongRunLapSeconds,7:F3}s | " +
                              $"wear {result.TyreWearPerLap:P1} | {result.EngineerNote}");
    }
    if (!weekend.QualifyingComplete)
    {
        Console.WriteLine("\nQUALIFYING");
        foreach (var result in session.RunQualifying(90210 + session.Calendar.Count(item => item.Completed)))
            Console.WriteLine($"P{result.GridPosition,2} #{result.CarNumber,-2} {result.DriverName,-18} {result.LapTimeSeconds:F3}s");
    }
    if (weekend.RaceComplete)
    {
        Console.WriteLine("This weekend is already complete.");
        return;
    }

    Console.WriteLine("\nGRAND PRIX — live timing begins shortly. Space pauses/resumes; Q skips to the finish.");
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
    DrawRaceFrame(circuit.Name, snapshot, oldPositions, eventFeed, paused);
    while (!engine.IsFinished)
    {
        if (!Console.IsInputRedirected)
        {
            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true).Key;
                if (key == ConsoleKey.Spacebar)
                {
                    paused = !paused;
                    DrawRaceFrame(circuit.Name, snapshot, oldPositions, eventFeed, paused);
                }
                else if (key == ConsoleKey.Q)
                {
                    fastForward = true;
                    paused = false;
                }
            }
            while (paused && !fastForward)
            {
                Thread.Sleep(100);
                if (!Console.KeyAvailable)
                    continue;
                var key = Console.ReadKey(intercept: true).Key;
                if (key == ConsoleKey.Spacebar)
                {
                    paused = false;
                    DrawRaceFrame(circuit.Name, snapshot, oldPositions, eventFeed, paused);
                }
                else if (key == ConsoleKey.Q)
                {
                    fastForward = true;
                    paused = false;
                }
            }
            if (!fastForward)
                Thread.Sleep(450);
        }
        else
        {
            Thread.Sleep(80);
        }

        var events = engine.AdvanceOneLap();
        foreach (var description in events.Select(item => item.Description).Distinct())
            eventFeed.Add(description);
        if (eventFeed.Count > 5)
            eventFeed.RemoveRange(0, eventFeed.Count - 5);
        snapshot = engine.GetSnapshot();
        DrawRaceFrame(circuit.Name, snapshot, oldPositions, eventFeed, paused);
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

static void DrawRaceFrame(
    string circuitName,
    RaceSnapshot snapshot,
    IReadOnlyDictionary<EntityId, int> previousPositions,
    IReadOnlyList<string> eventFeed,
    bool paused)
{
    if (!Console.IsOutputRedirected)
        Console.Clear();
    Console.WriteLine($"LIVE GRAND PRIX  |  {circuitName}");
    Console.WriteLine($"Lap {snapshot.CurrentLap}  |  Race clock {snapshot.RaceTime:mm\\:ss}  |  " +
                      $"{(paused ? "PAUSED — press Space to resume" : "Space: pause  Q: fast-forward")}");
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
    Console.WriteLine("Use the numbered menu or these commands: status, calendar, team, drivers, facilities, development, inbox, weekend, advance, help, exit.");
    Console.WriteLine("A weekend automatically runs practice, qualifying, and every race lap. Development projects progress through wind tunnel, design, and build.");
}
