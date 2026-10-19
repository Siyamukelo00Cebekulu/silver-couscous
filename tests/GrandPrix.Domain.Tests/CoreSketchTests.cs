using System;
using GrandPrix.Core.Common;
using GrandPrix.Core.Simulation;
using GrandPrix.Domain.Race;
using GrandPrix.Domain.Staff;

namespace GrandPrix.Domain.Tests;

public class CoreSketchTests
{
    [Fact]
    public void SeededRandomProducesSameSequenceForSameSeed()
    {
        var first = new SeededRandom(42);
        var second = new SeededRandom(42);

        Assert.Equal(first.Next(0, 1000), second.Next(0, 1000));
        Assert.Equal(first.NextDouble(), second.NextDouble());
        Assert.Equal(first.NextGaussian(0, 1), second.NextGaussian(0, 1));
    }

    [Fact]
    public void ForkedRandomStreamDoesNotDependOnParentDraws()
    {
        var firstParent = new SeededRandom(42);
        var firstStream = firstParent.Fork("tyres");
        firstParent.NextDouble();

        var secondStream = new SeededRandom(42).Fork("tyres");

        Assert.Equal(firstStream.NextDouble(), secondStream.NextDouble());
    }

    [Fact]
    public void CommandAndHiringEventUseSharedEntityIds()
    {
        var team = new EntityId(Guid.NewGuid());
        var engineer = new EntityId(Guid.NewGuid());
        var command = new HireEngineerCommand(team, engineer, new Money(125_000_00), 3);
        var occurredOn = new DateTime(2026, 1, 1);
        var hired = new EngineerHiredEvent(occurredOn, command.Issuer, command.CandidateId);

        Assert.Equal(team, command.Issuer);
        Assert.Equal(engineer, hired.EngineerId);
        Assert.Equal(occurredOn, hired.OccurredOn);
    }

    [Fact]
    public void RaceCommandsAndSnapshotExposeRaceFacingTypes()
    {
        var carId = new EntityId(Guid.NewGuid());
        IRaceCommand command = new BoxThisLapCommand(carId, RaceTyreCompound.Soft);
        var snapshot = new RaceSnapshot
        {
            CurrentLap = 3,
            Cars = new[] { new CarState { CarId = carId, Position = 1 } }
        };

        Assert.Equal(carId, command.CarId);
        Assert.Single(snapshot.Cars);
        Assert.Equal(RaceTyreCompound.Soft, ((BoxThisLapCommand)command).NextTyre);
    }
}
