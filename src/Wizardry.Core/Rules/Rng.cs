namespace Wizardry.Core.Rules;

/// <summary>
/// The game's only source of randomness: SplitMix64, so a saved state continues exactly where it
/// left off and tests can replay a game from a seed.
/// </summary>
public sealed class Rng(ulong state)
{
    public ulong State { get; set; } = state;

    public ulong NextULong()
    {
        ulong z = State += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>0 ≤ result &lt; max.</summary>
    public int Next(int max) => max <= 1 ? 0 : (int)(NextULong() % (ulong)max);

    /// <summary>min ≤ result ≤ max.</summary>
    public int Range(int min, int max) => max <= min ? min : min + Next(max - min + 1);

    /// <summary>True with the given percentage chance.</summary>
    public bool Percent(int chance) => Next(100) < chance;

    public bool OneIn(int n) => Next(n) == 0;

    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];

    public int Roll(Dice d) => d.Roll(this);
}

/// <summary>NdS+B, the way the game's tables write damage and hit points.</summary>
public readonly record struct Dice(int Count, int Sides, int Bonus = 0)
{
    public int Roll(Rng rng)
    {
        int total = Bonus;
        for (int i = 0; i < Count; i++) total += rng.Range(1, Sides);
        return total;
    }

    public int Min => Count + Bonus;
    public int Max => Count * Sides + Bonus;
    public double Average => Count * (Sides + 1) / 2.0 + Bonus;

    public override string ToString() => Bonus switch
    {
        0 => $"{Count}d{Sides}",
        > 0 => $"{Count}d{Sides}+{Bonus}",
        _ => $"{Count}d{Sides}{Bonus}",
    };

    /// <summary>Parses "2d8", "1d6+2" or "3d4-1".</summary>
    public static Dice Parse(string text)
    {
        var s = text.Trim().ToLowerInvariant();
        int d = s.IndexOf('d');
        if (d < 0) return new Dice(0, 1, int.Parse(s));
        int count = d == 0 ? 1 : int.Parse(s[..d]);
        var rest = s[(d + 1)..];
        int sign = rest.IndexOfAny(['+', '-']);
        if (sign < 0) return new Dice(count, int.Parse(rest));
        return new Dice(count, int.Parse(rest[..sign]), int.Parse(rest[sign..]));
    }

    public static implicit operator Dice(string text) => Parse(text);
}
