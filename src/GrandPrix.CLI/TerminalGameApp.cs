using System.Globalization;
using GrandPrix.Application;
using GrandPrix.CLI.Input;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace GrandPrix.CLI;

public sealed class TerminalGameApp
{
    private static readonly (string Label, string Command)[] MenuItems =
    {
        ("Dashboard", "status"),
        ("Season calendar and standings", "calendar"),
        ("Team", "team"),
        ("Driver profiles", "drivers"),
        ("Staff market", "staff"),
        ("Facilities", "facilities"),
        ("Car development", "development"),
        ("Inbox", "inbox"),
        ("Race weekend", "weekend"),
        ("Advance calendar", "advance"),
        ("Help", "help"),
        ("Quit", "exit")
    };

    private static readonly int[] RaceSpeeds = { 1, 2, 4, 8, 16 };
    private readonly IGameSession _session;
    private readonly IAnsiConsole _console;
    private readonly IInputSource _input;
    private readonly bool _plainMode;
    private string _status = string.Empty;
    private bool _running = true;
    private bool _inputEnded;

    public TerminalGameApp(
        IGameSession session,
        IAnsiConsole console,
        IInputSource input,
        bool? plainMode = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _plainMode = plainMode ?? !input.IsInteractive;
    }

    public void Run()
    {
        if (_plainMode)
            RunPlain();
        else
            RunInteractive();
    }

    private void RunPlain()
    {
        while (_running)
        {
            RenderHub(-1);
            var line = ReadLine("team> ");
            if (line is null)
                break;
            RunTextCommand(line);
        }
    }

    private void RunInteractive()
    {
        var selected = 0;
        while (_running)
        {
            RenderHub(selected);
            var key = _input.ReadKey();
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.W:
                    selected = (selected - 1 + MenuItems.Length) % MenuItems.Length;
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.S:
                    selected = (selected + 1) % MenuItems.Length;
                    break;
                case ConsoleKey.Enter:
                    RunMenuCommand(MenuItems[selected].Command);
                    break;
                case ConsoleKey.Escape:
                case ConsoleKey.Backspace:
                    _status = "You are already at the team hub.";
                    break;
                case ConsoleKey.Oem2:
                    ShowHelp();
                    break;
                case ConsoleKey.Q:
                    ConfirmQuit();
                    break;
                default:
                    if (key.Key >= ConsoleKey.D1 && key.Key <= ConsoleKey.D9)
                    {
                        var index = (int)key.Key - (int)ConsoleKey.D1;
                        selected = index;
                        RunMenuCommand(MenuItems[index].Command);
                    }
                    else
                    {
                        _status = $"Unknown key: {key.Key}. Use the footer controls.";
                    }
                    break;
            }
        }
    }

    private void RenderHub(int selected)
    {
        if (!_plainMode)
            _console.Clear();

        var dashboard = _session.GetDashboard();
        _console.Write(new Rule("GRAND PRIX TEAM PRINCIPAL"));

        var summary = new Table();
        summary.AddColumn("Team");
        summary.AddColumn("Date");
        summary.AddColumn("Next event");
        summary.AddColumn("Rounds");
        summary.AddColumn("Budget");
        summary.AddColumn("Cost-cap headroom");
        summary.AddRow(
            dashboard.TeamName,
            dashboard.Today.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            $"{dashboard.NextCircuit} — {dashboard.NextEventDate:dd MMM}",
            dashboard.RoundsRemaining.ToString(CultureInfo.InvariantCulture),
            FormatMoney(dashboard.BudgetRemainingCents),
            FormatMoney(dashboard.CostCapHeadroomCents));
        _console.Write(summary);

        var standings = new Table();
        standings.AddColumn("Position");
        standings.AddColumn("Championship");
        standings.AddColumn("Points");
        standings.AddColumn("Car");
        standings.AddColumn("Staff");
        standings.AddColumn("Inbox");
        standings.AddRow(
            $"P{dashboard.ChampionshipPosition}",
            dashboard.TeamName,
            dashboard.ChampionshipPoints.ToString(CultureInfo.InvariantCulture),
            $"Aero {dashboard.Downforce:F0} | efficiency {dashboard.DragEfficiency:F0} | " +
            $"reliability {dashboard.Reliability:F0} | life {dashboard.ComponentLifeLaps:F0} laps",
            dashboard.StaffCount.ToString(CultureInfo.InvariantCulture),
            dashboard.UnreadInboxCount.ToString(CultureInfo.InvariantCulture));
        _console.Write(standings);

        if (dashboard.LatestInboxHeadlines.Count > 0)
        {
            _console.Write(new Rule("LATEST INBOX"));
            foreach (var headline in dashboard.LatestInboxHeadlines)
                WriteLine($"• {headline}");
        }

        var menu = new Table();
        menu.AddColumn("Key");
        menu.AddColumn("Screen");
        for (var index = 0; index < MenuItems.Length; index++)
        {
            var marker = index == selected ? "›" : " ";
            menu.AddRow($"{marker} {index + 1}", MenuItems[index].Label);
        }
        _console.Write(menu);
        WriteFooter("↑/↓ or W/S navigate · Enter select · 1-9 shortcuts · Esc/Backspace back · ? help · Q quit");
        if (!string.IsNullOrWhiteSpace(_status))
            WriteStatus(_status);
    }

    private void RunTextCommand(string input)
    {
        var text = input.Trim();
        if (text.Length == 0)
            return;

        switch (text.ToLowerInvariant())
        {
            case "calendar":
            case "standings":
                ShowCalendar();
                return;
            case "team":
                ShowTeam();
                return;
            case "drivers":
                ShowDrivers();
                return;
            case "staff":
            case "market":
                ShowStaff();
                return;
            case "facilities":
                ManageFacilities();
                return;
            case "development":
                ManageDevelopment();
                return;
            case "inbox":
                ShowInbox();
                return;
            case "weekend":
            case "race":
                RunWeekend();
                return;
            case "advance":
                AdvanceCalendar();
                return;
            case "status":
                _status = "Dashboard refreshed.";
                return;
            case "help":
            case "?":
                ShowHelp();
                return;
            case "exit":
            case "quit":
                ConfirmQuit();
                return;
        }

        var command = CommandParser.Parse(text);
        switch (command.Kind)
        {
            case CliCommandKind.Empty:
                return;
            case CliCommandKind.Status:
                _status = "Dashboard refreshed.";
                break;
            case CliCommandKind.Market:
                ShowStaff();
                break;
            case CliCommandKind.Hire:
                HireFromCommand(command);
                break;
            case CliCommandKind.Advance:
                try
                {
                    var result = _session.AdvanceDays(command.Days);
                    _status = result.Message;
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    _status = exception.Message;
                }
                break;
            case CliCommandKind.Race:
                RunWeekend(command.Number, command.Seed);
                break;
            case CliCommandKind.Standings:
                ShowCalendar();
                break;
            case CliCommandKind.Help:
                ShowHelp();
                break;
            case CliCommandKind.Exit:
                ConfirmQuit();
                break;
            case CliCommandKind.Invalid:
                _status = command.Error;
                break;
            default:
                _status = "That command is only available during a live race.";
                break;
        }
    }

    private void RunMenuCommand(string command)
    {
        switch (command)
        {
            case "status":
                _status = "Dashboard refreshed.";
                break;
            case "calendar":
                ShowCalendar();
                break;
            case "team":
                ShowTeam();
                break;
            case "drivers":
                ShowDrivers();
                break;
            case "staff":
                ShowStaff();
                break;
            case "facilities":
                ManageFacilities();
                break;
            case "development":
                ManageDevelopment();
                break;
            case "inbox":
                ShowInbox();
                break;
            case "weekend":
                RunWeekend();
                break;
            case "advance":
                AdvanceCalendar();
                break;
            case "help":
                ShowHelp();
                break;
            case "exit":
                ConfirmQuit();
                break;
        }
    }

    private void ShowCalendar()
    {
        var table = new Table();
        table.AddColumn("Status");
        table.AddColumn("Date");
        table.AddColumn("Circuit");
        table.AddColumn("Country");
        table.AddColumn("Laps");
        table.AddColumn("Character");
        foreach (var race in _session.GetCalendar())
            table.AddRow(
                race.Completed ? "DONE" : race.Next ? "NEXT" : "UPCOMING",
                race.Date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
                race.Circuit,
                race.Country,
                race.RaceLaps.ToString(CultureInfo.InvariantCulture),
                race.Characteristic);

        _console.Write(new Rule("SEASON CALENDAR"));
        _console.Write(table);
        var standings = new Table();
        standings.AddColumn("Pos");
        standings.AddColumn("Team");
        standings.AddColumn("Points");
        foreach (var entry in _session.GetStandings())
            standings.AddRow(entry.Position.ToString(CultureInfo.InvariantCulture), entry.TeamName,
                entry.Points.ToString(CultureInfo.InvariantCulture));
        _console.Write(new Rule("CHAMPIONSHIP"));
        _console.Write(standings);
        WriteFooter("Enter a screen shortcut · Esc/Backspace returns · ? help · Q quit");
    }

    private void ShowTeam()
    {
        var dashboard = _session.GetDashboard();
        var staff = _session.GetStaff();
        var facilities = _session.GetFacilities();
        _console.Write(new Rule($"TEAM — {dashboard.TeamName}"));
        WriteLine($"Budget: {FormatMoney(dashboard.BudgetRemainingCents)}");
        WriteLine($"Championship: P{dashboard.ChampionshipPosition}, {dashboard.ChampionshipPoints} points");
        WriteLine($"Engineers: {staff.Engineers.Count}");
        foreach (var engineer in staff.Engineers)
            WriteLine($"{engineer.Name} · strategy {engineer.Skill} · " +
                      $"{FormatMoney(engineer.AnnualSalaryCents)}/year · {engineer.ContractYears}-year deal");
        WriteLine($"Facilities: wind tunnel L{facilities.WindTunnelLevel}, " +
                  $"simulator L{facilities.SimulatorLevel}, factory L{facilities.FactoryLevel}");
        WriteFooter("Enter a screen shortcut · Esc/Backspace returns · ? help · Q quit");
    }

    private void ShowDrivers()
    {
        var table = new Table();
        table.AddColumn("No.");
        table.AddColumn("Driver");
        table.AddColumn("Team");
        table.AddColumn("Pace");
        table.AddColumn("Racecraft");
        table.AddColumn("Focus");
        table.AddColumn("Consistency");
        table.AddColumn("Tyres");
        foreach (var driver in _session.GetDrivers())
            table.AddRow(driver.CarNumber.ToString(CultureInfo.InvariantCulture), driver.Name, driver.TeamName,
                driver.Pace.ToString(CultureInfo.InvariantCulture),
                driver.Racecraft.ToString(CultureInfo.InvariantCulture),
                driver.Focus.ToString(CultureInfo.InvariantCulture),
                driver.Consistency.ToString(CultureInfo.InvariantCulture),
                driver.TyreManagement.ToString(CultureInfo.InvariantCulture));
        _console.Write(new Rule("DRIVER PROFILES"));
        _console.Write(table);
        WriteFooter("Enter a screen shortcut · Esc/Backspace returns · ? help · Q quit");
    }

    private void ShowStaff()
    {
        var view = _session.GetStaff();
        var team = new Table();
        team.AddColumn("Engineer");
        team.AddColumn("Skill");
        team.AddColumn("Annual salary");
        team.AddColumn("Contract");
        foreach (var engineer in view.Engineers)
            team.AddRow(engineer.Name, engineer.Skill.ToString(CultureInfo.InvariantCulture),
                FormatMoney(engineer.AnnualSalaryCents), $"{engineer.ContractYears} years");
        _console.Write(new Rule($"STAFF — {view.TeamName}"));
        _console.Write(team);

        var market = new Table();
        market.AddColumn("#");
        market.AddColumn("Candidate");
        market.AddColumn("Skill");
        market.AddColumn("Asking salary");
        market.AddColumn("Status");
        foreach (var candidate in view.Candidates)
            market.AddRow(candidate.MarketNumber.ToString(CultureInfo.InvariantCulture),
                candidate.Name, candidate.Skill.ToString(CultureInfo.InvariantCulture),
                FormatMoney(candidate.AskingAnnualSalaryCents), candidate.Hired ? "HIRED" : "Available");
        _console.Write(new Rule("ENGINEER MARKET"));
        _console.Write(market);

        var available = view.Candidates.Where(item => !item.Hired).ToArray();
        if (available.Length == 0)
        {
            _status = "No engineers are currently available to hire.";
            return;
        }
        var choice = ChooseOption(
            "SELECT ENGINEER",
            available.Select(item => (
                item.MarketNumber.ToString(CultureInfo.InvariantCulture),
                $"{item.Name} · skill {item.Skill} · {FormatMoney(item.AskingAnnualSalaryCents)}/year"))
                .ToArray());
        if (!int.TryParse(choice, out var number))
            return;
        var candidateView = view.Candidates.FirstOrDefault(item => item.MarketNumber == number);
        if (candidateView is null || candidateView.Hired)
        {
            _status = "Choose an available engineer from the displayed market.";
            return;
        }
        if (!Confirm($"Hire {candidateView.Name} for {FormatMoney(candidateView.AskingAnnualSalaryCents)} per year?"))
            return;

        var result = _session.HireEngineer(
            number,
            candidateView.AskingAnnualSalaryCents,
            3);
        _status = result.Succeeded
            ? $"Hired {candidateView.Name} on a three-year contract."
            : result.Error ?? "Engineer hiring was rejected.";
    }

    private void HireFromCommand(CliCommand command)
    {
        var candidate = _session.GetStaff().Candidates
            .FirstOrDefault(item => item.MarketNumber == command.Number);
        if (candidate is null)
        {
            _status = "Choose an engineer from the displayed market.";
            return;
        }

        var salaryCents = command.SalaryDollars > 0
            ? checked((long)decimal.Round(command.SalaryDollars * 100m, 0))
            : candidate.AskingAnnualSalaryCents;
        if (!Confirm($"Hire {candidate.Name} for {FormatMoney(salaryCents)} per year?"))
            return;
        var result = _session.HireEngineer(command.Number, salaryCents, command.ContractYears);
        _status = result.Succeeded
            ? $"Hired {candidate.Name} on a {command.ContractYears}-year contract."
            : result.Error ?? "Engineer hiring was rejected.";
    }

    private void ShowInbox()
    {
        _console.Write(new Rule("TEAM INBOX"));
        var messages = _session.GetInbox();
        if (messages.Count == 0)
            WriteLine("No messages.");
        foreach (var item in messages)
            WriteLine($"{(item.Unread ? "NEW" : "   ")} {item.Date:yyyy-MM-dd} · {item.Headline}");
        _session.MarkInboxRead();
        WriteFooter("Enter a screen shortcut · Esc/Backspace returns · ? help · Q quit");
    }

    private void ManageFacilities()
    {
        var view = _session.GetFacilities();
        _console.Write(new Rule("FACILITIES"));
        WriteLine($"Wind tunnel L{view.WindTunnelLevel} · next upgrade {FormatMoney(view.WindTunnelUpgradeCostCents)}");
        WriteLine($"Simulator L{view.SimulatorLevel} · next upgrade {FormatMoney(view.SimulatorUpgradeCostCents)}");
        WriteLine($"Factory L{view.FactoryLevel} · next upgrade {FormatMoney(view.FactoryUpgradeCostCents)}");
        WriteLine($"Available budget: {FormatMoney(view.BudgetRemainingCents)}");
        var facility = ChooseOption("SELECT FACILITY", new[]
        {
            ("wind", $"Wind tunnel · L{view.WindTunnelLevel} · {FormatMoney(view.WindTunnelUpgradeCostCents)}"),
            ("simulator", $"Simulator · L{view.SimulatorLevel} · {FormatMoney(view.SimulatorUpgradeCostCents)}"),
            ("factory", $"Factory · L{view.FactoryLevel} · {FormatMoney(view.FactoryUpgradeCostCents)}")
        });
        if (string.IsNullOrWhiteSpace(facility) || facility.Equals("back", StringComparison.OrdinalIgnoreCase))
            return;
        if (!Confirm($"Upgrade {facility} using team funds?"))
            return;
        var result = _session.UpgradeFacilityView(facility);
        _status = result.Message;
    }

    private void ManageDevelopment()
    {
        var view = _session.GetDevelopment();
        _console.Write(new Rule("CAR DEVELOPMENT"));
        WriteLine($"Aero {view.Downforce:F0} · efficiency {view.DragEfficiency:F0} · " +
                  $"reliability {view.Reliability:F0} · component life {view.ComponentLifeLaps:F0} laps");
        WriteLine($"Cost cap: {FormatMoney(view.CostCapSpentCents)} / {FormatMoney(view.CostCapLimitCents)} " +
                  $"({FormatMoney(view.CostCapHeadroomCents)} headroom)");

        if (view.HasActiveProject)
        {
            WriteLine($"Project: {view.ProjectFocus} · {view.ProjectStage} · step {view.ProjectProgress:F0}/3");
            if (!Confirm("Advance this development project by one work step?"))
                return;
            _status = _session.AdvanceDevelopmentView().Message;
            return;
        }

        var normalizedFocus = ChooseOption("SELECT DEVELOPMENT FOCUS", new[]
        {
            ("aero", "Aerodynamics"),
            ("chassis", "Chassis"),
            ("reliability", "Reliability")
        });
        if (string.IsNullOrWhiteSpace(normalizedFocus))
            return;
        var budgetText = ReadLine("Budget in whole dollars (minimum 100000)> ");
        if (!decimal.TryParse(budgetText, NumberStyles.Number, CultureInfo.InvariantCulture, out var dollars) ||
            dollars < 100_000 || dollars > long.MaxValue / 100m)
        {
            _status = "Enter a valid budget of at least $100,000.";
            return;
        }
        var cents = checked((long)decimal.Round(dollars * 100m, 0));
        if (!Confirm($"Commit {FormatMoney(cents)} to the {normalizedFocus} project?"))
            return;
        var action = _session.StartDevelopmentView(normalizedFocus, cents);
        _status = action.Message;
    }

    private void AdvanceCalendar()
    {
        var choice = ChooseOption("ADVANCE CALENDAR", new[]
        {
            ("day", "Advance one day"),
            ("week", "Advance one week"),
            ("event", "Advance to next event")
        });
        if (choice is null)
            return;
        try
        {
            var result = choice switch
            {
                "day" => _session.AdvanceDays(1),
                "week" => _session.AdvanceDays(7),
                "event" or "next" => _session.AdvanceToNextEvent(),
                _ => null
            };
            if (result is null)
            {
                _status = "Choose day, week, or event.";
                return;
            }
            _status = result.Message;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            _status = exception.Message;
        }
    }

    private void RunWeekend(int? laps = null, int? seed = null)
    {
        var round = _session.GetCalendar().Count(item => item.Completed);
        var view = _session.GetRaceWeekend();
        if (view.RaceComplete || view.RaceLaps == 0)
        {
            _status = "There is no scheduled race weekend to run.";
            return;
        }

        _console.Write(new Rule($"RACE WEEKEND — {view.Circuit}"));
        WriteLine($"{view.Country} · {laps ?? view.RaceLaps} laps");
        if (!view.PracticeComplete)
        {
            var focus = ChooseOption("PRACTICE FOCUS", new[]
            {
                ("setup", "Setup"),
                ("tyreevaluation", "Tyre evaluation"),
                ("racesimulation", "Race simulation")
            });
            if (focus is null)
                return;
            try
            {
                var practice = _session.RunPracticeView(2026 + round, focus);
                var table = new Table();
                table.AddColumn("Driver");
                table.AddColumn("Best lap");
                table.AddColumn("Long run");
                table.AddColumn("Wear/lap");
                table.AddColumn("Feedback");
                foreach (var item in practice)
                    table.AddRow(item.DriverName, $"{item.BestLapSeconds:F3}s",
                        $"{item.LongRunLapSeconds:F3}s", $"{item.TyreWearPerLap:P1}", item.EngineerNote);
                _console.Write(table);
            }
            catch (ArgumentException exception)
            {
                _status = exception.Message;
                return;
            }
        }

        view = _session.GetRaceWeekend();
        if (!view.QualifyingComplete)
        {
            var tyre = ChooseOption("QUALIFYING TYRE", new[]
            {
                ("Soft", "Soft"),
                ("Medium", "Medium"),
                ("Hard", "Hard")
            });
            var risk = ChooseOption("QUALIFYING RISK", new[]
            {
                ("Safe", "Safe"),
                ("Normal", "Normal"),
                ("Push", "Push")
            });
            var runsText = ChooseOption("QUALIFYING RUNS",
                Enumerable.Range(1, 5).Select(value =>
                    (value.ToString(CultureInfo.InvariantCulture), $"{value} run(s)"))
                    .ToArray());
            if (tyre is null || risk is null || !int.TryParse(runsText, out var runs))
                return;
            try
            {
                var qualifying = _session.RunQualifyingView(
                    90210 + round, tyre, risk, runs);
                var table = new Table();
                table.AddColumn("Grid");
                table.AddColumn("Car");
                table.AddColumn("Driver");
                table.AddColumn("Lap time");
                foreach (var item in qualifying)
                    table.AddRow($"P{item.GridPosition}", $"#{item.CarNumber}",
                        item.DriverName, $"{item.LapTimeSeconds:F3}s");
                _console.Write(table);
            }
            catch (ArgumentException exception)
            {
                _status = exception.Message;
                return;
            }
            catch (InvalidOperationException exception)
            {
                _status = exception.Message;
                return;
            }
        }

        RunLiveRace(laps ?? view.RaceLaps, seed ?? 42 + round);
    }

    private void RunLiveRace(int laps, int seed)
    {
        RaceTimingViewModel timing;
        try
        {
            timing = _session.StartRace(laps, seed);
        }
        catch (InvalidOperationException exception)
        {
            _status = exception.Message;
            return;
        }

        var previousPositions = timing.Cars.ToDictionary(car => car.CarNumber, car => car.Position);
        var eventFeed = new List<string>();
        var selectedCar = 11;
        var selectedTyre = "Medium";
        var speedIndex = 0;
        var paused = false;
        var ticks = 0;
        var maxTicks = Math.Max(100, laps * 400);
        string? raceError = null;

        if (_plainMode)
        {
            ReadPlainRaceInstruction(timing, eventFeed, message => raceError = message);
            speedIndex = RaceSpeeds.Length - 1;
        }

        void AdvanceAndRender(LiveDisplayContext? live)
        {
            previousPositions = timing.Cars.ToDictionary(car => car.CarNumber, car => car.Position);
            timing = _session.AdvanceRace(3 * RaceSpeeds[speedIndex]);
            eventFeed.Clear();
            eventFeed.AddRange(timing.RecentEvents);
            live?.UpdateTarget(BuildRaceFrame(
                timing, previousPositions, eventFeed, selectedCar, selectedTyre, RaceSpeeds[speedIndex], paused));
            ticks++;
        }

        if (_plainMode)
        {
            while (!timing.Finished && ticks < maxTicks)
                AdvanceAndRender(null);
            _console.Write(BuildRaceFrame(
                timing, previousPositions, eventFeed, selectedCar,
                selectedTyre, RaceSpeeds[speedIndex], paused));
        }
        else
        {
            _console.Live(BuildRaceFrame(
                    timing, previousPositions, eventFeed, selectedCar, selectedTyre, RaceSpeeds[speedIndex], paused))
                .Start(live =>
                {
                    while (!timing.Finished && ticks < maxTicks)
                    {
                        while (_input.TryReadKey(out var key))
                        {
                            switch (key.Key)
                            {
                                case ConsoleKey.Spacebar:
                                    paused = !paused;
                                    break;
                                case ConsoleKey.Q:
                                    speedIndex = RaceSpeeds.Length - 1;
                                    paused = false;
                                    break;
                                case ConsoleKey.UpArrow:
                                case ConsoleKey.LeftArrow:
                                    selectedCar = 11;
                                    break;
                                case ConsoleKey.DownArrow:
                                case ConsoleKey.RightArrow:
                                    selectedCar = 12;
                                    break;
                                case ConsoleKey.T:
                                    selectedTyre = NextTyre(selectedTyre);
                                    break;
                                case ConsoleKey.P:
                                    ApplyRaceAction(_session.QueuePitStop(selectedCar, selectedTyre, 0),
                                        eventFeed, message => raceError = message);
                                    break;
                                case ConsoleKey.A:
                                    try
                                    {
                                        var advice = _session.GetRaceAdvice(selectedCar);
                                        if (advice.RecommendPit)
                                            ApplyRaceAction(_session.QueuePitStop(
                                                selectedCar, advice.SuggestedTyre, 0),
                                                eventFeed, message => raceError = message);
                                        else
                                            eventFeed.Add(advice.Message);
                                    }
                                    catch (ArgumentException exception)
                                    {
                                        raceError = exception.Message;
                                        eventFeed.Add(exception.Message);
                                    }
                                    break;
                                case ConsoleKey.M:
                                    var playerCar = timing.Cars.FirstOrDefault(item =>
                                        item.CarNumber == selectedCar && item.IsPlayerCar);
                                    if (playerCar is not null)
                                        ApplyRaceAction(_session.SetRaceMode(
                                            selectedCar, NextMode(playerCar.Mode)),
                                            eventFeed, message => raceError = message);
                                    break;
                                case ConsoleKey.D1:
                                case ConsoleKey.D2:
                                case ConsoleKey.D3:
                                case ConsoleKey.D4:
                                case ConsoleKey.D5:
                                    speedIndex = (int)key.Key - (int)ConsoleKey.D1;
                                    break;
                            }
                        }
                        if (!paused)
                            AdvanceAndRender(live);
                        else
                            live.UpdateTarget(BuildRaceFrame(
                                timing, previousPositions, eventFeed, selectedCar,
                                selectedTyre, RaceSpeeds[speedIndex], paused));
                        if (!timing.Finished && !paused)
                            Thread.Sleep(80);
                    }
                });
        }

        if (!timing.Finished)
        {
            _status = $"Race stopped at the {maxTicks}-step safety limit; no result was settled.";
            return;
        }

        var settlement = _session.SettleLiveRace();
        _console.Write(new Rule("FINAL CLASSIFICATION"));
        foreach (var result in settlement.Classification)
            WriteLine(result);
        foreach (var payout in settlement.Payouts.Where(item => item.AmountCents > 0))
            WriteLine($"{payout.TeamName}: {FormatMoney(payout.AmountCents)}" +
                      (payout.IsPlayer ? " · player team" : string.Empty));
        _status = raceError ??
            $"Race settled. Team budget: {FormatMoney(settlement.TeamBudgetCents)}.";
    }

    private void ReadPlainRaceInstruction(
        RaceTimingViewModel timing,
        ICollection<string> feed,
        Action<string> reportError)
    {
        var instruction = ReadLine("Race control: pit [11|12] [soft|medium|hard], pace [11|12] [mode], or finish> ");
        if (instruction is null || instruction.Equals("finish", StringComparison.OrdinalIgnoreCase))
            return;
        var command = CommandParser.Parse(instruction);
        if (command.Kind == CliCommandKind.Pit)
        {
            ApplyRaceAction(_session.QueuePitStop(
                command.CarNumber, command.Tyre.ToString(), command.DelaySeconds), feed, reportError);
            return;
        }
        if (command.Kind == CliCommandKind.Pace)
        {
            ApplyRaceAction(_session.SetRaceMode(
                command.CarNumber, command.Mode.ToString()), feed, reportError);
            return;
        }
        var error = command.Kind == CliCommandKind.Invalid
            ? command.Error
            : $"Unknown race instruction: {instruction}";
        feed.Add(error);
        reportError(error);
    }

    private IRenderable BuildRaceFrame(
        RaceTimingViewModel snapshot,
        IReadOnlyDictionary<int, int> previousPositions,
        IReadOnlyList<string> feed,
        int selectedCar,
        string tyre,
        int speed,
        bool paused)
    {
        var rows = new Table();
        rows.AddColumn("Pos");
        rows.AddColumn("Driver");
        rows.AddColumn("Team");
        rows.AddColumn("Gap");
        rows.AddColumn("Tyre");
        rows.AddColumn("Wear");
        rows.AddColumn("Stops");
        rows.AddColumn("Flag");
        foreach (var car in snapshot.Cars)
        {
            var previous = previousPositions.TryGetValue(car.CarNumber, out var position) ? position : car.Position;
            var movement = car.Position < previous ? "▲" : car.Position > previous ? "▼" : "—";
            rows.AddRow($"{car.Position} {movement}", $"#{car.CarNumber} {car.DriverName}", car.TeamName,
                car.Position == 1 ? "LEADER" : $"+{car.GapToLeaderSeconds:F1}s",
                car.Tyre, $"{car.TyreWear:P0}", car.PitStops.ToString(CultureInfo.InvariantCulture), car.Status);
        }

        var body = new Table();
        body.AddColumn("Race");
        body.AddRow($"Lap {snapshot.CurrentLap} / {snapshot.TotalLaps} · " +
                    $"{snapshot.RaceTime:mm\\:ss} · {(paused ? "PAUSED" : $"{speed}x")}");
        body.AddRow($"Selected car #{selectedCar} · pit tyre {tyre} · 1-5 speed · arrows select · T tyre · P pit · A advice · M mode · Space pause · Q 16x");
        body.AddRow(rows);
        body.AddRow("COMMENTARY");
        foreach (var item in feed.TakeLast(5))
            body.AddRow(item);
        if (feed.Count == 0)
            body.AddRow("Formation lap complete. Green flag.");
        return new Panel(body);
    }

    private static void ApplyRaceAction(
        RaceActionViewModel action,
        ICollection<string> feed,
        Action<string> reportError)
    {
        feed.Add(action.Message);
        if (!action.Succeeded)
        {
            reportError(action.Message);
        }
    }

    private void ShowHelp()
    {
        _console.Write(new Rule("HELP"));
        WriteLine("Use the arrow keys or W/S to navigate, Enter to select, Esc/Backspace to return.");
        WriteLine("Use Q to request a confirmed quit, or ? for this help.");
        WriteLine("Text commands: status, calendar, team, drivers, staff, facilities, development, inbox, weekend, advance [days], help, exit.");
        WriteLine("During a race: arrows select your car, T selects a tyre, P pits, M changes mode, 1-5 select 1x/2x/4x/8x/16x, Space pauses, Q fast-forwards.");
        WriteFooter("Esc/Backspace returns · Q quit");
    }

    private void ConfirmQuit()
    {
        if (!Confirm("Quit the current session? (y/n)"))
            return;
        _running = false;
    }

    private bool Confirm(string prompt)
    {
        var answer = ReadLine($"{prompt} y/n> ");
        return answer is not null &&
            (answer.Equals("y", StringComparison.OrdinalIgnoreCase) ||
             answer.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    private string? ReadLine(string prompt)
    {
        if (_inputEnded)
            return null;
        WriteLine(prompt);
        if (_input.IsInteractive)
        {
            var buffer = new System.Text.StringBuilder();
            while (true)
            {
                var key = _input.ReadKey();
                if (key.Key == ConsoleKey.Enter)
                {
                    WriteLine(string.Empty);
                    return buffer.ToString();
                }
                if (key.Key == ConsoleKey.Escape || (key.Key == ConsoleKey.Backspace && buffer.Length == 0))
                    return string.Empty;
                if (key.Key == ConsoleKey.Backspace)
                {
                    buffer.Length--;
                    _console.Write(new Text("\b \b"));
                    continue;
                }
                if (!char.IsControl(key.KeyChar))
                {
                    buffer.Append(key.KeyChar);
                    _console.Write(new Text(key.KeyChar.ToString()));
                }
            }
        }

        var result = _input.ReadLine();
        if (result is null)
        {
            _inputEnded = true;
            _running = false;
        }
        return result;
    }

    private string? ChooseOption(string title, IReadOnlyList<(string Value, string Label)> options)
    {
        if (_plainMode)
        {
            var input = ReadLine($"{title} ({string.Join(", ", options.Select(item => item.Value))}, back)> ");
            if (input is null || string.IsNullOrWhiteSpace(input) ||
                input.Equals("back", StringComparison.OrdinalIgnoreCase))
                return null;
            var match = options.FirstOrDefault(item =>
                item.Value.Equals(input, StringComparison.OrdinalIgnoreCase) ||
                item.Label.Equals(input, StringComparison.OrdinalIgnoreCase));
            return match == default ? input : match.Value;
        }

        if (options.Count == 0)
            return null;
        var selected = 0;
        while (true)
        {
            _console.Clear();
            _console.Write(new Rule(title));
            var table = new Table();
            table.AddColumn("Select");
            table.AddColumn("Option");
            for (var index = 0; index < options.Count; index++)
                table.AddRow(index == selected ? "›" : " ", options[index].Label);
            _console.Write(table);
            WriteFooter("↑/↓ navigate · Enter select · Esc/Backspace back");
            var key = _input.ReadKey();
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.W:
                    selected = (selected - 1 + options.Count) % options.Count;
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.S:
                    selected = (selected + 1) % options.Count;
                    break;
                case ConsoleKey.Enter:
                    return options[selected].Value;
                case ConsoleKey.Escape:
                case ConsoleKey.Backspace:
                    return null;
                default:
                    _status = $"Unknown key: {key.Key}. Use the selection controls.";
                    break;
            }
        }
    }

    private void WriteFooter(string text) =>
        WriteLine(text);

    private void WriteStatus(string text)
    {
        _console.Write(new Panel(new Markup(Markup.Escape(text))) { Border = BoxBorder.Rounded });
    }

    private void WriteLine(string text)
    {
        _console.Write(new Markup($"{Markup.Escape(text)}{Environment.NewLine}"));
    }

    private static string FormatMoney(long cents) =>
        (cents / 100m).ToString("C0", CultureInfo.GetCultureInfo("en-US"));

    private static string NextTyre(string current) => current switch
    {
        "Soft" => "Medium",
        "Medium" => "Hard",
        _ => "Soft"
    };

    private static string NextMode(string current) => current switch
    {
        "Standard" => "Push",
        "Push" => "SaveTyres",
        "SaveTyres" => "SaveFuel",
        _ => "Standard"
    };
}
