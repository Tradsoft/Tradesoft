using System;

namespace TradeSoft.Core.Trading;

/// <summary>
/// Prix stocké en entier "scaled" (ex: 1.23456 avec scalePow10=5 => 123456).
/// Zéro allocation, comparaisons rapides, pas de drift.
/// </summary>
public readonly struct Price : IComparable<Price>, IEquatable<Price>
{
    public Price(long scaled, int scalePow10)
    {
        if (scalePow10 < 0 || scalePow10 > 9)
            throw new ArgumentOutOfRangeException(nameof(scalePow10), "scalePow10 must be between 0 and 9.");

        Scaled = scaled;
        ScalePow10 = scalePow10;
    }

    public long Scaled { get; }
    public int ScalePow10 { get; }

    public decimal ToDecimal()
        => Scaled / Pow10Decimal(ScalePow10);

    public static Price FromDecimal(decimal value, int scalePow10)
    {
        var scaled = decimal.Round(value * Pow10Decimal(scalePow10), 0, MidpointRounding.AwayFromZero);
        return new Price((long)scaled, scalePow10);
    }

    public int CompareTo(Price other)
    {
        EnsureSameScale(other);
        return Scaled.CompareTo(other.Scaled);
    }

    public bool Equals(Price other)
        => ScalePow10 == other.ScalePow10 && Scaled == other.Scaled;

    public override bool Equals(object? obj)
        => obj is Price p && Equals(p);

    public override int GetHashCode()
        => HashCode.Combine(Scaled, ScalePow10);

    public override string ToString() => ToDecimal().ToString();

    public static bool operator <(Price a, Price b) => a.CompareTo(b) < 0;
    public static bool operator >(Price a, Price b) => a.CompareTo(b) > 0;
    public static bool operator <=(Price a, Price b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Price a, Price b) => a.CompareTo(b) >= 0;

    private void EnsureSameScale(Price other)
    {
        if (ScalePow10 != other.ScalePow10)
            throw new InvalidOperationException("Cannot compare prices with different scales.");
    }

    private static decimal Pow10Decimal(int pow10)
    {
        decimal x = 1m;
        for (int i = 0; i < pow10; i++) x *= 10m;
        return x;
    }
}
