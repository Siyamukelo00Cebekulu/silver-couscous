namespace GrandPrix.Core.Common;

public readonly struct Money
{
    public long Cents { get; }

    public Money(long cents)
    {
        Cents = cents;
    }

    public static Money operator +(Money left, Money right) =>
        new(checked(left.Cents + right.Cents));

    public static Money operator -(Money left, Money right) =>
        new(checked(left.Cents - right.Cents));
}
