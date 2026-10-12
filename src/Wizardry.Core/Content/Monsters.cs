using Wizardry.Core.Rules;

namespace Wizardry.Core.Content;

/// <summary>What a monster's touch can do besides damage.</summary>
[Flags]
public enum Touch
{
    None = 0,
    Poison = 1,
    Paralyze = 2,
    Stone = 4,
    Sleep = 8,
    Drain = 16,
    Critical = 32,
}

public enum Breath { None, Fire, Cold, Poison, Stone }

/// <summary>
/// A monster type. Until a group is identified the party sees only <see cref="UnknownName"/>.
/// <see cref="Mage"/> and <see cref="Priest"/> are the highest spell levels it casts (0 = none).
/// </summary>
public sealed record MonsterDef(
    int Id,
    string Name,
    string Plural,
    string UnknownName,
    string UnknownPlural,
    int Level,
    Dice Hp,
    int Ac,
    Dice[] Attacks,
    int Xp,
    Dice Group,
    Family Family = Family.None,
    Touch Touch = Touch.None,
    Breath Breath = Breath.None,
    int Mage = 0,
    int Priest = 0,
    /// <summary>Chance in 100 that a spell aimed at it fizzles.</summary>
    int MagicResist = 0,
    bool ResistsSleep = false,
    /// <summary>Hit points regained each round.</summary>
    int Regenerate = 0,
    /// <summary>Chance in 100 each round that a wounded group calls for help.</summary>
    int CallsForHelp = 0,
    Dice Gold = default,
    Alignment Align = Alignment.Neutral,
    /// <summary>Monster type that often comes along as a second group.</summary>
    int Companion = 0,
    bool Unique = false)
{
    /// <summary>
    /// Experience for slaying one: grows with the square of the level, with a fifth more for
    /// monsters with nasty tricks (spells, breath, a dangerous touch). The table's own figure is
    /// used for unique guardians.
    /// </summary>
    public int Experience => Unique ? Xp
        : (int)((40 * Level * Level + 80 * Level) * (Mage > 0 || Priest > 0 || Breath != Breath.None || Touch != Touch.None ? 1.2 : 1.0));

    public string NameFor(int count, bool identified) => identified
        ? (count == 1 ? Name : Plural)
        : (count == 1 ? UnknownName : UnknownPlural);
}

public static class Monsters
{
    public const int Warden = 40;
    public const int Morthrain = 60;

    private static Dice[] A(params string[] d) => [.. d.Select(Dice.Parse)];

    public static readonly IReadOnlyList<MonsterDef> All =
    [
        // Level 1-2
        new(1, "Kobold", "Kobolds", "Small Humanoid", "Small Humanoids", 1, "1d4", 9, A("1d4"), 30, "1d4", Family.Humanoid, Gold: "1d10", Align: Alignment.Evil),
        new(2, "Giant Rat", "Giant Rats", "Animal", "Animals", 1, "1d3", 8, A("1d3"), 20, "1d4", Family.Animal),
        new(3, "Orc", "Orcs", "Humanoid", "Humanoids", 1, "1d6", 9, A("1d6"), 45, "1d4", Family.Humanoid, Gold: "1d15", Align: Alignment.Evil),
        new(4, "Green Ooze", "Green Oozes", "Slime", "Slimes", 1, "1d5", 10, A("1d3"), 25, "1d4", Touch: Touch.Poison),
        new(5, "Hedge Witch", "Hedge Witches", "Woman in Rags", "Women in Rags", 2, "2d4", 10, A("1d4"), 90, "1d2", Family.Mage, Mage: 1, Gold: "2d10", Align: Alignment.Evil, Companion: 1),
        new(6, "Acolyte", "Acolytes", "Man in Robes", "Men in Robes", 2, "2d5", 9, A("1d5"), 90, "1d2", Family.Priest, Priest: 1, Gold: "2d10", Align: Alignment.Good),
        new(7, "Brigand", "Brigands", "Man in Leather", "Men in Leather", 2, "2d6", 7, A("1d6"), 70, "1d4", Family.Humanoid, Gold: "2d15", Align: Alignment.Evil),
        new(8, "Skeleton", "Skeletons", "Undead", "Undead", 2, "2d6", 8, A("1d6"), 60, "1d4", Family.Undead, ResistsSleep: true),
        new(9, "Giant Beetle", "Giant Beetles", "Insect", "Insects", 2, "2d6", 6, A("1d6"), 75, "1d3", Family.Insect),
        new(10, "Lurking Bat", "Lurking Bats", "Flying Thing", "Flying Things", 2, "1d8", 6, A("1d3"), 55, "1d5", Family.Animal),
        // Level 3-4
        new(11, "Zombie", "Zombies", "Undead", "Undead", 3, "3d6", 8, A("1d8"), 120, "1d5", Family.Undead, ResistsSleep: true),
        new(12, "Gnoll", "Gnolls", "Humanoid", "Humanoids", 3, "3d6", 6, A("1d8"), 130, "1d5", Family.Humanoid, Gold: "3d15", Align: Alignment.Evil),
        new(13, "Conjurer", "Conjurers", "Man in Robes", "Men in Robes", 3, "3d4", 8, A("1d4"), 220, "1d3", Family.Mage, Mage: 2, Gold: "3d20", Align: Alignment.Evil, Companion: 12),
        new(14, "Cave Spider", "Cave Spiders", "Insect", "Insects", 3, "3d5", 5, A("1d6"), 160, "1d4", Family.Insect, Touch: Touch.Poison),
        new(15, "Highwayman", "Highwaymen", "Man in Leather", "Men in Leather", 3, "3d6", 5, A("1d8", "1d4"), 180, "1d4", Family.Humanoid, Gold: "4d15", Align: Alignment.Evil),
        new(16, "Rotting Hound", "Rotting Hounds", "Animal", "Animals", 3, "3d6", 7, A("1d6"), 140, "1d6", Family.Undead | Family.Animal, Touch: Touch.Paralyze, ResistsSleep: true),
        new(17, "Swordsman", "Swordsmen", "Man in Armor", "Men in Armor", 4, "4d8", 4, A("1d8"), 260, "1d4", Family.Humanoid, Gold: "4d20", Align: Alignment.Good),
        new(18, "Bishop's Novice", "Bishop's Novices", "Man in Robes", "Men in Robes", 4, "4d5", 7, A("1d6"), 280, "1d3", Family.Priest, Priest: 2, Gold: "4d20", Align: Alignment.Good, Companion: 17),
        new(19, "Ghoul", "Ghouls", "Undead", "Undead", 4, "4d6", 6, A("1d4", "1d4", "1d6"), 300, "1d4", Family.Undead, Touch: Touch.Paralyze, ResistsSleep: true),
        new(20, "Gas Cloud", "Gas Clouds", "Cloud", "Clouds", 4, "3d8", 5, [], 280, "1d3", Breath: Breath.Poison, ResistsSleep: true),
        new(21, "Wererat", "Wererats", "Animal", "Animals", 4, "4d8", 6, A("1d6", "1d3"), 320, "1d4", Family.Were, Touch: Touch.Poison, Align: Alignment.Evil),
        // Level 5-6
        new(22, "Ogre", "Ogres", "Giant Humanoid", "Giant Humanoids", 5, "5d8", 5, A("2d6"), 450, "1d3", Family.Giant, Gold: "5d30", Align: Alignment.Evil),
        new(23, "Gargoyle", "Gargoyles", "Statue", "Statues", 5, "5d8", 4, A("1d6", "1d6", "1d8"), 500, "1d4", Family.Myth, ResistsSleep: true),
        new(24, "Warlock", "Warlocks", "Man in Robes", "Men in Robes", 5, "5d6", 6, A("1d6"), 600, "1d4", Family.Mage, Mage: 3, MagicResist: 10, Gold: "5d40", Align: Alignment.Evil, Companion: 22),
        new(25, "Werewolf", "Werewolves", "Animal", "Animals", 5, "5d8", 5, A("1d8", "1d6"), 550, "1d4", Family.Were, Touch: Touch.Paralyze, Regenerate: 1, Align: Alignment.Evil),
        new(26, "Basilisk", "Basilisks", "Reptile", "Reptiles", 6, "6d8", 4, A("1d10"), 800, "1d2", Family.Animal, Touch: Touch.Stone),
        new(27, "Wight", "Wights", "Undead", "Undead", 6, "6d6", 4, A("1d8"), 850, "1d3", Family.Undead, Touch: Touch.Drain, ResistsSleep: true),
        new(28, "Templar", "Templars", "Man in Armor", "Men in Armor", 6, "6d8", 2, A("1d10", "1d6"), 900, "1d4", Family.Priest, Priest: 3, Gold: "6d40", Align: Alignment.Good),
        new(29, "Giant Mantis", "Giant Mantises", "Insect", "Insects", 6, "6d8", 3, A("1d8", "1d8"), 750, "1d3", Family.Insect, Touch: Touch.Critical),
        new(30, "Troll", "Trolls", "Giant Humanoid", "Giant Humanoids", 6, "6d10", 4, A("1d8", "1d8", "2d6"), 1000, "1d3", Family.Giant, Regenerate: 3, Align: Alignment.Evil),
        new(31, "Drake", "Drakes", "Dragon", "Dragons", 6, "6d8", 3, A("1d8", "2d4"), 1100, "1d2", Family.Dragon, Breath: Breath.Fire, Gold: "6d50"),
        // Level 7-8
        new(32, "Sorcerer", "Sorcerers", "Man in Robes", "Men in Robes", 7, "7d6", 4, A("1d8"), 1500, "1d4", Family.Mage, Mage: 5, MagicResist: 20, Gold: "8d50", Align: Alignment.Evil, Companion: 30),
        new(33, "Wraith", "Wraiths", "Undead", "Undead", 7, "7d8", 3, A("1d10"), 1600, "1d3", Family.Undead, Touch: Touch.Drain | Touch.Paralyze, ResistsSleep: true, MagicResist: 15),
        new(34, "Chimera", "Chimeras", "Beast", "Beasts", 7, "7d10", 2, A("1d8", "1d8", "2d6"), 1800, "1d2", Family.Myth, Breath: Breath.Fire),
        new(35, "Hill Giant", "Hill Giants", "Giant", "Giants", 8, "8d10", 3, A("4d6"), 2200, "1d3", Family.Giant, Gold: "8d60", Align: Alignment.Evil),
        new(36, "Frost Wyvern", "Frost Wyverns", "Dragon", "Dragons", 8, "8d8", 2, A("2d6", "1d8"), 2400, "1d2", Family.Dragon, Breath: Breath.Cold, Touch: Touch.Poison),
        new(37, "Assassin", "Assassins", "Man in Black", "Men in Black", 8, "8d6", 0, A("2d6", "1d8"), 2500, "1d3", Family.Humanoid, Touch: Touch.Critical | Touch.Poison, Gold: "8d60", Align: Alignment.Evil),
        new(38, "High Priest", "High Priests", "Man in Robes", "Men in Robes", 8, "8d6", 2, A("1d8"), 2600, "1d3", Family.Priest, Priest: 5, MagicResist: 25, Gold: "8d60", Align: Alignment.Evil, Companion: 33),
        new(39, "Medusa", "Medusas", "Woman", "Women", 8, "8d8", 3, A("1d8"), 2700, "1d2", Family.Myth, Breath: Breath.Stone, Touch: Touch.Poison),
        new(Warden, "Iron Warden", "Iron Wardens", "Huge Statue", "Huge Statues", 8, "40d3", 0, A("3d6", "3d6"), 8000, "1d1", Family.Myth, ResistsSleep: true, MagicResist: 40, Unique: true),
        // Level 9-10
        new(41, "Vampire", "Vampires", "Undead", "Undead", 9, "9d10", 1, A("1d10", "1d10"), 3500, "1d3", Family.Undead, Touch: Touch.Drain, Mage: 3, MagicResist: 30, ResistsSleep: true, Regenerate: 3, Gold: "9d80", Align: Alignment.Evil),
        new(42, "Fire Giant", "Fire Giants", "Giant", "Giants", 9, "10d10", 1, A("5d6"), 3800, "1d3", Family.Giant, Breath: Breath.Fire, Gold: "9d80", Align: Alignment.Evil),
        new(43, "Lesser Demon", "Lesser Demons", "Demon", "Demons", 9, "9d10", 0, A("1d8", "1d8", "2d6"), 4000, "1d3", Family.Demon, Mage: 4, MagicResist: 40, CallsForHelp: 15, Align: Alignment.Evil),
        new(44, "Archmage", "Archmages", "Man in Robes", "Men in Robes", 10, "10d6", 1, A("1d10"), 4500, "1d3", Family.Mage, Mage: 6, MagicResist: 40, Gold: "10d90", Align: Alignment.Evil, Companion: 43),
        new(45, "Black Dragon", "Black Dragons", "Dragon", "Dragons", 10, "10d12", -1, A("2d8", "2d8", "3d8"), 6000, "1d2", Family.Dragon, Breath: Breath.Poison, MagicResist: 30, Gold: "10d150"),
        new(46, "Greater Demon", "Greater Demons", "Demon", "Demons", 11, "11d12", -3, A("3d6", "3d6"), 7500, "1d2", Family.Demon, Mage: 5, Touch: Touch.Poison, MagicResist: 60, CallsForHelp: 20, Align: Alignment.Evil),
        new(47, "Vampire Lord", "Vampire Lords", "Undead", "Undead", 12, "12d10", -2, A("2d8", "2d8"), 8000, "1d1", Family.Undead, Touch: Touch.Drain | Touch.Paralyze, Mage: 5, MagicResist: 50, ResistsSleep: true, Regenerate: 5, Gold: "10d150", Align: Alignment.Evil, Companion: 41),
        new(48, "Death Knight", "Death Knights", "Man in Armor", "Men in Armor", 11, "11d10", -4, A("3d8", "2d8"), 7000, "1d2", Family.Undead, Touch: Touch.Critical, MagicResist: 30, ResistsSleep: true, Gold: "10d100", Align: Alignment.Evil),
        new(49, "Gold Dragon", "Gold Dragons", "Dragon", "Dragons", 12, "12d12", -3, A("3d6", "3d6", "4d8"), 9000, "1d1", Family.Dragon, Breath: Breath.Fire, Priest: 4, MagicResist: 40, Gold: "12d200", Align: Alignment.Good),
        new(Morthrain, "Morthrain", "Morthrain", "Man in Robes", "Men in Robes", 14, "100d3", -5, A("2d10"), 40000, "1d1", Family.Mage, Mage: 7, Priest: 6, MagicResist: 70, ResistsSleep: true, Regenerate: 5, Align: Alignment.Evil, Companion: 47, Unique: true),
    ];

    private static readonly Dictionary<int, MonsterDef> ById = All.ToDictionary(m => m.Id);

    public static MonsterDef Get(int id) => ById[id];

    /// <summary>Wandering monsters for each dungeon level (1-10), by id.</summary>
    public static IReadOnlyList<int> ForLevel(int level) => level switch
    {
        1 => [1, 2, 3, 4, 5, 6, 10],
        2 => [3, 5, 6, 7, 8, 9, 10, 4],
        3 => [7, 8, 9, 11, 12, 13, 14, 15, 16],
        4 => [11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21],
        5 => [17, 18, 19, 20, 21, 22, 23, 24, 25],
        6 => [22, 23, 24, 25, 26, 27, 28, 29, 30, 31],
        7 => [26, 27, 28, 29, 30, 31, 32, 33, 34],
        8 => [32, 33, 34, 35, 36, 37, 38, 39],
        9 => [35, 36, 37, 38, 39, 41, 42, 43, 44],
        _ => [41, 42, 43, 44, 45, 46, 47, 48, 49],
    };
}
