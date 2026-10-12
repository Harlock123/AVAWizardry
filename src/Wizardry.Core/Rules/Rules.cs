namespace Wizardry.Core.Rules;

public enum Race { Human, Elf, Dwarf, Gnome, Hobbit }

public enum CharClass { Fighter, Mage, Priest, Thief, Bishop, Samurai, Lord, Ninja }

public enum Alignment { Good, Neutral, Evil }

public enum Stat { Str, Iq, Pie, Vit, Agi, Luc }

/// <summary>Worst last: a character's condition only ever gets worse down this list until cured.</summary>
public enum Status { Ok, Asleep, Afraid, Paralyzed, Stoned, Dead, Ashes, Lost }

/// <summary>The two schools of magic.</summary>
public enum School { Mage, Priest }

public static class StatusExtensions
{
    /// <summary>Can act in combat and walk.</summary>
    public static bool CanAct(this Status s) => s is Status.Ok or Status.Afraid;

    /// <summary>Counts as alive (not dead, ashes or lost).</summary>
    public static bool IsAlive(this Status s) => s < Status.Dead;

    /// <summary>Asleep, paralysed or stoned: alive but out of the fight.</summary>
    public static bool IsDisabled(this Status s) => s is Status.Asleep or Status.Paralyzed or Status.Stoned;

    public static string Label(this Status s) => s switch
    {
        Status.Ok => "OK",
        Status.Asleep => "ASLEEP",
        Status.Afraid => "AFRAID",
        Status.Paralyzed => "PLYZE",
        Status.Stoned => "STONED",
        Status.Dead => "DEAD",
        Status.Ashes => "ASHES",
        Status.Lost => "LOST",
        _ => s.ToString().ToUpperInvariant(),
    };
}

public static class RuleTables
{
    public static readonly Stat[] AllStats = [Stat.Str, Stat.Iq, Stat.Pie, Stat.Vit, Stat.Agi, Stat.Luc];

    public static string Abbrev(this Stat s) => s switch
    {
        Stat.Str => "STR",
        Stat.Iq => "I.Q.",
        Stat.Pie => "PIE",
        Stat.Vit => "VIT",
        Stat.Agi => "AGI",
        _ => "LUCK",
    };

    /// <summary>Starting attributes before bonus points, in STR IQ PIE VIT AGI LUC order.</summary>
    public static int[] BaseStats(Race race) => race switch
    {
        Race.Human => [8, 8, 5, 8, 8, 9],
        Race.Elf => [7, 10, 10, 6, 9, 6],
        Race.Dwarf => [10, 7, 10, 10, 5, 6],
        Race.Gnome => [7, 7, 10, 8, 10, 7],
        _ => [5, 7, 7, 6, 10, 15],
    };

    /// <summary>A stat can be raised to 18 at creation and by levelling.</summary>
    public const int MaxStat = 18;

    /// <summary>Minimum attributes for each class, in STR IQ PIE VIT AGI LUC order.</summary>
    public static int[] Requirements(CharClass c) => c switch
    {
        CharClass.Fighter => [11, 0, 0, 0, 0, 0],
        CharClass.Mage => [0, 11, 0, 0, 0, 0],
        CharClass.Priest => [0, 0, 11, 0, 0, 0],
        CharClass.Thief => [0, 0, 0, 0, 11, 0],
        CharClass.Bishop => [0, 12, 12, 0, 0, 0],
        CharClass.Samurai => [15, 11, 10, 14, 10, 0],
        CharClass.Lord => [15, 12, 12, 15, 14, 15],
        _ => [17, 17, 17, 17, 17, 17],
    };

    public static bool AlignmentAllows(CharClass c, Alignment a) => c switch
    {
        CharClass.Priest or CharClass.Bishop => a != Alignment.Neutral,
        CharClass.Thief => a != Alignment.Good,
        CharClass.Samurai => a != Alignment.Evil,
        CharClass.Lord => a == Alignment.Good,
        CharClass.Ninja => a == Alignment.Evil,
        _ => true,
    };

    public static bool Qualifies(CharClass c, Alignment a, IReadOnlyList<int> stats)
    {
        if (!AlignmentAllows(c, a)) return false;
        var req = Requirements(c);
        for (int i = 0; i < 6; i++)
            if (stats[i] < req[i]) return false;
        return true;
    }

    public static string Abbrev(this CharClass c) => c switch
    {
        CharClass.Fighter => "FIG",
        CharClass.Mage => "MAG",
        CharClass.Priest => "PRI",
        CharClass.Thief => "THI",
        CharClass.Bishop => "BIS",
        CharClass.Samurai => "SAM",
        CharClass.Lord => "LOR",
        _ => "NIN",
    };

    public static char Letter(this Alignment a) => a switch { Alignment.Good => 'G', Alignment.Neutral => 'N', _ => 'E' };

    /// <summary>Die rolled for hit points at each new level.</summary>
    public static int HitDie(CharClass c) => c switch
    {
        CharClass.Fighter or CharClass.Lord => 10,
        CharClass.Priest or CharClass.Samurai => 8,
        CharClass.Thief or CharClass.Bishop or CharClass.Ninja => 6,
        _ => 4,
    };

    /// <summary>Hit points at level 1, before the vitality adjustment.</summary>
    public static int StartingHp(CharClass c) => c switch
    {
        CharClass.Samurai => 16,
        CharClass.Lord => 12,
        _ => HitDie(c),
    };

    /// <summary>Hit points added (or taken) per level for vitality.</summary>
    public static int VitalityBonus(int vit) => vit switch
    {
        <= 3 => -2,
        <= 5 => -1,
        <= 15 => 0,
        16 => 1,
        17 => 2,
        _ => 3,
    };

    /// <summary>To-hit and damage adjustment for strength.</summary>
    public static int StrengthBonus(int str) => str switch
    {
        <= 5 => -1,
        <= 15 => 0,
        16 => 1,
        17 => 2,
        _ => 3,
    };

    /// <summary>Initiative adjustment for agility (higher acts sooner).</summary>
    public static int AgilityBonus(int agi) => agi switch
    {
        <= 3 => -3,
        <= 5 => -2,
        <= 7 => -1,
        <= 14 => 0,
        15 => 1,
        16 => 2,
        17 => 3,
        _ => 4,
    };

    public static bool IsFighterType(CharClass c) => c is CharClass.Fighter or CharClass.Samurai or CharClass.Lord or CharClass.Ninja;

    /// <summary>Experience to reach level 2..13; each level past 13 costs the last step again.</summary>
    private static readonly Dictionary<CharClass, int[]> Experience = new()
    {
        [CharClass.Fighter] = [1000, 1724, 2972, 5124, 8834, 15231, 26260, 45275, 78060, 134586, 232044, 400075],
        [CharClass.Mage] = [1100, 1896, 3268, 5634, 9713, 16746, 28872, 49779, 85825, 147974, 255127, 439874],
        [CharClass.Priest] = [1050, 1810, 3120, 5379, 9274, 15989, 27567, 47529, 81946, 141286, 243596, 419993],
        [CharClass.Thief] = [900, 1551, 2674, 4610, 7948, 13703, 23625, 40732, 70227, 121081, 208760, 359931],
        [CharClass.Bishop] = [1150, 1983, 3419, 5894, 10162, 17520, 30207, 52081, 89793, 154816, 266921, 460207],
        [CharClass.Samurai] = [1250, 2155, 3715, 6405, 11042, 19037, 32821, 56587, 97561, 168207, 290012, 500021],
        [CharClass.Lord] = [1300, 2241, 3864, 6661, 11484, 19800, 34137, 58855, 101474, 174951, 301640, 520069],
        [CharClass.Ninja] = [1450, 2499, 4308, 7428, 12807, 22080, 38068, 65634, 113162, 195106, 336388, 579979],
    };

    private static readonly Dictionary<CharClass, int> ExperienceStep = new()
    {
        [CharClass.Fighter] = 289709,
        [CharClass.Mage] = 318529,
        [CharClass.Priest] = 304132,
        [CharClass.Thief] = 260639,
        [CharClass.Bishop] = 333247,
        [CharClass.Samurai] = 362086,
        [CharClass.Lord] = 376602,
        [CharClass.Ninja] = 419992,
    };

    /// <summary>Total experience needed to reach <paramref name="level"/> (level 1 needs none).</summary>
    public static long ExperienceFor(CharClass c, int level)
    {
        if (level <= 1) return 0;
        var table = Experience[c];
        if (level - 2 < table.Length) return table[level - 2];
        return table[^1] + (long)ExperienceStep[c] * (level - 1 - table.Length);
    }

    /// <summary>
    /// The character level at which each spell level (1-7) of a school opens to a class, or null
    /// if the class never learns that school.
    /// </summary>
    public static int? SpellLevelOpensAt(CharClass c, School school, int spellLevel) => (c, school) switch
    {
        (CharClass.Mage, School.Mage) or (CharClass.Priest, School.Priest) => 2 * spellLevel - 1,
        (CharClass.Bishop, School.Mage) => 4 * spellLevel - 3,
        (CharClass.Bishop, School.Priest) => 4 * spellLevel,
        (CharClass.Samurai, School.Mage) or (CharClass.Lord, School.Priest) => 3 * spellLevel + 1,
        _ => null,
    };

    /// <summary>Casts per spell level at a character level (0 when the level isn't open), at most 9.</summary>
    public static int SpellSlots(CharClass c, School school, int spellLevel, int charLevel)
    {
        if (SpellLevelOpensAt(c, school, spellLevel) is not { } opens || charLevel < opens) return 0;
        int step = c switch
        {
            CharClass.Mage or CharClass.Priest => 2,
            CharClass.Bishop => 4,
            _ => 3,
        };
        return Math.Min(9, 1 + (charLevel - opens) / step);
    }

    /// <summary>Gold a new character starts with.</summary>
    public static Dice StartingGold => new(1, 100, 90);

    /// <summary>A new character's age in weeks (18-21 years).</summary>
    public static int StartingAgeWeeks(Rng rng) => 52 * 18 + rng.Next(52 * 3);
}
