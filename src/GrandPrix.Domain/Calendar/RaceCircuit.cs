namespace GrandPrix.Domain.Calendar;

public sealed class RaceCircuit
{
    public RaceCircuit(
        string id,
        string name,
        string country,
        int raceLaps,
        double baseLapTimeSeconds,
        double tyreWearFactor,
        double pitLossSeconds,
        double overtakingDifficulty,
        string characteristic)
    {
        Id = id;
        Name = name;
        Country = country;
        RaceLaps = raceLaps;
        BaseLapTimeSeconds = baseLapTimeSeconds;
        TyreWearFactor = tyreWearFactor;
        PitLossSeconds = pitLossSeconds;
        OvertakingDifficulty = overtakingDifficulty;
        Characteristic = characteristic;
    }

    public string Id { get; }
    public string Name { get; }
    public string Country { get; }
    public int RaceLaps { get; }
    public double BaseLapTimeSeconds { get; }
    public double TyreWearFactor { get; }
    public double PitLossSeconds { get; }
    public double OvertakingDifficulty { get; }
    public string Characteristic { get; }
}

public sealed class CalendarRaceEvent
{
    public CalendarRaceEvent(DateTime date, RaceCircuit circuit)
    {
        Date = date.Date;
        Circuit = circuit;
    }

    public DateTime Date { get; }
    public RaceCircuit Circuit { get; }
    public bool Completed { get; set; }
}
