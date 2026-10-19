namespace GrandPrix.Domain.Race
{
    public enum WeatherCondition { Dry, LightRain, HeavyRain }

    public class Weather
    {
        public WeatherCondition Condition { get; set; } = WeatherCondition.Dry;
        public double TrackTemperature { get; set; } = 30.0;
        public double AirTemperature { get; set; } = 22.0;

        public static Weather Dry => new Weather
        {
            Condition = WeatherCondition.Dry,
            TrackTemperature = 30,
            AirTemperature = 22
        };

        public static Weather LightRain => new Weather
        {
            Condition = WeatherCondition.LightRain,
            TrackTemperature = 20,
            AirTemperature = 17
        };

        public static Weather HeavyRain => new Weather
        {
            Condition = WeatherCondition.HeavyRain,
            TrackTemperature = 16,
            AirTemperature = 14
        };
    }
}