using System;
using System.Globalization;
using GrandPrix.Domain.Race;

namespace GrandPrix.CLI.Input;

public enum CliCommandKind
{
    Empty,
    Help,
    Status,
    Market,
    Hire,
    Advance,
    Race,
    Standings,
    Pit,
    Pace,
    Events,
    Exit,
    Invalid
}

public sealed class CliCommand
{
    public CliCommandKind Kind { get; init; }
    public int Number { get; init; }
    public int Seed { get; init; }
    public int Days { get; init; }
    public int CarNumber { get; init; }
    public decimal SalaryDollars { get; init; }
    public int ContractYears { get; init; }
    public double DelaySeconds { get; init; }
    public RaceTyreCompound Tyre { get; init; }
    public DriverMode Mode { get; init; }
    public string Error { get; init; } = string.Empty;
}

public static class CommandParser
{
    public static CliCommand Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new CliCommand { Kind = CliCommandKind.Empty };

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var verb = parts[0].ToLowerInvariant();
        switch (verb)
        {
            case "help":
            case "?":
                return new CliCommand { Kind = CliCommandKind.Help };
            case "status":
                return Exact(parts, 1, CliCommandKind.Status);
            case "market":
                return Exact(parts, 1, CliCommandKind.Market);
            case "standings":
                return Exact(parts, 1, CliCommandKind.Standings);
            case "events":
                return Exact(parts, 1, CliCommandKind.Events);
            case "exit":
            case "quit":
                return Exact(parts, 1, CliCommandKind.Exit);
            case "advance":
                if (parts.Length == 1)
                    return new CliCommand { Kind = CliCommandKind.Advance, Days = 1 };
                return parts.Length == 2 && int.TryParse(parts[1], out var days) && days > 0
                    ? new CliCommand { Kind = CliCommandKind.Advance, Days = days }
                    : Invalid("Usage: advance [positive number of days]");
            case "race":
                if (parts.Length > 3)
                    return Invalid("Usage: race [laps] [seed]");
                var laps = 12;
                var seed = 42;
                if (parts.Length >= 2 && (!int.TryParse(parts[1], out laps) || laps < 3 || laps > 100))
                    return Invalid("Race length must be between 3 and 100 laps.");
                if (parts.Length == 3 && !int.TryParse(parts[2], out seed))
                    return Invalid("Race seed must be an integer.");
                return new CliCommand { Kind = CliCommandKind.Race, Number = laps, Seed = seed };
            case "hire":
                if (parts.Length < 2 || parts.Length > 4 ||
                    !int.TryParse(parts[1], out var candidate))
                    return Invalid("Usage: hire <market-number> [annual-salary-dollars] [contract-years]");
                decimal salary = 0;
                var years = 3;
                if (parts.Length >= 3 &&
                    (!decimal.TryParse(parts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out salary) || salary <= 0))
                    return Invalid("Annual salary must be a positive dollar amount.");
                if (parts.Length == 4 && (!int.TryParse(parts[3], out years) || years < 1 || years > 5))
                    return Invalid("Contract length must be between 1 and 5 years.");
                return new CliCommand
                {
                    Kind = CliCommandKind.Hire,
                    Number = candidate,
                    SalaryDollars = salary,
                    ContractYears = years
                };
            case "pit":
                if (parts.Length < 3 || parts.Length > 4 ||
                    !int.TryParse(parts[1], out var pitCar) ||
                    !Enum.TryParse(parts[2], true, out RaceTyreCompound tyre) ||
                    !Enum.IsDefined(typeof(RaceTyreCompound), tyre))
                    return Invalid("Usage: pit <car-number> <soft|medium|hard|intermediate|wet> [extra-delay-seconds]");
                var delay = 2.0;
                if (parts.Length == 4 &&
                    (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out delay) ||
                     delay < 0 || double.IsNaN(delay) || double.IsInfinity(delay)))
                    return Invalid("Pit delay must be a non-negative number of seconds.");
                return new CliCommand
                {
                    Kind = CliCommandKind.Pit,
                    CarNumber = pitCar,
                    Tyre = tyre,
                    DelaySeconds = delay
                };
            case "pace":
                if (parts.Length != 3 || !int.TryParse(parts[1], out var paceCar) ||
                    !TryParseMode(parts[2], out var mode))
                    return Invalid("Usage: pace <car-number> <push|standard|save-tyres|save-fuel>");
                return new CliCommand { Kind = CliCommandKind.Pace, CarNumber = paceCar, Mode = mode };
            case "next":
                return Exact(parts, 1, CliCommandKind.Advance);
            default:
                return Invalid($"Unknown command '{parts[0]}'. Type 'help' to see available commands.");
        }
    }

    private static CliCommand Exact(string[] parts, int expected, CliCommandKind kind) =>
        parts.Length == expected
            ? new CliCommand { Kind = kind }
            : Invalid($"'{parts[0]}' does not accept additional arguments.");

    private static CliCommand Invalid(string error) =>
        new() { Kind = CliCommandKind.Invalid, Error = error };

    private static bool TryParseMode(string text, out DriverMode mode)
    {
        switch (text.ToLowerInvariant())
        {
            case "push":
                mode = DriverMode.Push;
                return true;
            case "standard":
                mode = DriverMode.Standard;
                return true;
            case "save-tyres":
                mode = DriverMode.SaveTyres;
                return true;
            case "save-fuel":
                mode = DriverMode.SaveFuel;
                return true;
            default:
                mode = DriverMode.Standard;
                return false;
        }
    }
}
