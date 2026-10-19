using System;

namespace GrandPrix.Core.Common;

public readonly struct EntityId : IEquatable<EntityId>
{
    public Guid Value { get; }

    public EntityId(Guid value)
    {
        Value = value;
    }

    public bool Equals(EntityId other) => Value.Equals(other.Value);

    public override bool Equals(object? obj) => obj is EntityId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);

    public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
}
