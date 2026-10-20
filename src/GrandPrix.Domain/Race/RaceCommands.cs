using GrandPrix.Core.Common;

namespace GrandPrix.Domain.Race;

public interface IRaceCommand
{
    EntityId CarId { get; }
}

public sealed class BoxThisLapCommand : IRaceCommand
{
    public BoxThisLapCommand(EntityId carId, RaceTyreCompound nextTyre, double delaySeconds = 2)
    {
        CarId = carId;
        NextTyre = nextTyre;
        DelaySeconds = delaySeconds;
    }

    public EntityId CarId { get; }
    public RaceTyreCompound NextTyre { get; }
    public double DelaySeconds { get; }
}

public sealed class SetDriverModeCommand : IRaceCommand
{
    public SetDriverModeCommand(EntityId carId, DriverMode mode)
    {
        CarId = carId;
        Mode = mode;
    }

    public EntityId CarId { get; }
    public DriverMode Mode { get; }
}
