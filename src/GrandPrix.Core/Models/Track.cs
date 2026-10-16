public class Track
{
    public string Name { get; set; } = "";
    public double BaseLapTime { get; set; }   // seconds
    public int Laps { get; set; }
    public double TyreWearFactor { get; set; }
    public double OvertakingDifficulty { get; set; }
    public double PitLossTime { get; set; }  // seconds
}