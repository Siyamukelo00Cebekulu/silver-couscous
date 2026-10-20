namespace GrandPrix.Domain.Drivers
{
    public class Driver
    {
        public string Name { get; set; } = "";
        public int Pace { get; set; } = 70;        // 0-100
        public int Consistency { get; set; } = 70;  // 0-100
        public int TyreManagement { get; set; } = 70;
        public int WetSkill { get; set; } = 70;
        public int Racecraft { get; set; } = 70;
        public int Focus { get; set; } = 70;
        public int Experience { get; set; } = 50;
        public int Feedback { get; set; } = 70;
        public int Aggression { get; set; } = 60;
        public int Defending { get; set; } = 70;
    }
}