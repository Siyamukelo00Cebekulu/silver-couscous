namespace GrandPrix.Core.Models;

public class Driver
{
    public string Name { get; set; } = "";
    public int Pace { get; set; } = 70;        // 0-100
    public int Consistency { get; set; } = 70;  // 0-100
    public int TyreManagement { get; set; } = 70;
    public int WetSkill { get; set; } = 70;
}