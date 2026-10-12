using Wizardry.Core.Content;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Model;

/// <summary>An item a character carries. Items found in the maze start unidentified.</summary>
public sealed class OwnedItem
{
    public int ItemId { get; set; }
    public bool Equipped { get; set; }
    public bool Identified { get; set; }

    public ItemDef Def => Items.Get(ItemId);

    /// <summary>A cursed item, once equipped, can't be taken off until uncursed at the trading post.</summary>
    public bool Stuck => Equipped && Def.Cursed;

    public string DisplayName => Identified ? Def.Name : Def.UnknownName;

    public static OwnedItem Known(int id) => new() { ItemId = id, Identified = true };
}

/// <summary>Where a character is when not in the active party.</summary>
public sealed record MazeSpot(int Level, int X, int Y);

public sealed class Character
{
    public const int MaxItems = 8;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public Race Race { get; set; }
    public CharClass Class { get; set; }
    public Alignment Alignment { get; set; }
    /// <summary>STR IQ PIE VIT AGI LUC.</summary>
    public int[] Stats { get; set; } = new int[6];
    public int Level { get; set; } = 1;
    /// <summary>Highest level reached (energy drain lowers <see cref="Level"/> only).</summary>
    public int MaxLevel { get; set; } = 1;
    public long Experience { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public long Gold { get; set; }
    public int AgeWeeks { get; set; }
    public Status Status { get; set; }
    public bool Poisoned { get; set; }
    public List<OwnedItem> Items { get; set; } = [];
    public int[] MageSlots { get; set; } = new int[7];
    public int[] PriestSlots { get; set; } = new int[7];
    public HashSet<string> Spells { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Times this character has brought back the Amulet of Thal.</summary>
    public int Honors { get; set; }
    public int Deaths { get; set; }
    /// <summary>Set when the character was left behind in the maze (a party that perished).</summary>
    public MazeSpot? LeftInMaze { get; set; }

    /// <summary>Armour class bonus from spells for the current fight (not saved).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int FightWard { get; set; }

    public int Str => Stats[0];
    public int Iq => Stats[1];
    public int Pie => Stats[2];
    public int Vit => Stats[3];
    public int Agi => Stats[4];
    public int Luc => Stats[5];

    public int Stat(Stat s) => Stats[(int)s];

    public int AgeYears => AgeWeeks / 52;

    public bool IsAlive => Status.IsAlive();

    public IEnumerable<OwnedItem> Equipped => Items.Where(i => i.Equipped);

    public OwnedItem? Weapon => Items.FirstOrDefault(i => i.Equipped && i.Def.Kind == ItemKind.Weapon);

    public string ClassTag => $"{Alignment.Letter()}-{Class.Abbrev()}";

    /// <summary>Armour class: 10 unarmoured, lower is better.</summary>
    public int ArmorClass(int partyWard = 0)
    {
        int ac = 10 - Equipped.Sum(i => i.Def.Ac) - FightWard - partyWard;
        // A ninja fights best unarmoured.
        if (Class == CharClass.Ninja && !Equipped.Any(i => i.Def.Kind is ItemKind.Armor or ItemKind.Shield))
            ac -= Level / 3 + 2;
        return ac;
    }

    /// <summary>Added to a d20 roll when attacking.</summary>
    public int HitSkill
    {
        get
        {
            int skill = Class switch
            {
                CharClass.Fighter or CharClass.Samurai or CharClass.Lord or CharClass.Ninja => 2 + Level / 2,
                CharClass.Priest or CharClass.Thief or CharClass.Bishop => Level / 3,
                _ => Level / 5,
            };
            return skill + RuleTables.StrengthBonus(Str) + Equipped.Sum(i => i.Def.ToHit);
        }
    }

    /// <summary>Swings per round.</summary>
    public int Swings
    {
        get
        {
            int swings = Class switch
            {
                CharClass.Fighter or CharClass.Samurai or CharClass.Lord => 1 + Level / 5,
                CharClass.Ninja => Weapon is null ? 2 + Level / 5 : 1 + Level / 5,
                _ => 1,
            };
            return Math.Min(10, swings + Equipped.Sum(i => i.Def.ExtraSwings));
        }
    }

    /// <summary>Damage per hit: the weapon's dice, or fists (1d2; a ninja's are 1d8+level/4).</summary>
    public Dice Damage
    {
        get
        {
            if (Weapon is { } w) return w.Def.Damage;
            return Class == CharClass.Ninja ? new Dice(1, 8, Level / 4) : new Dice(1, 2);
        }
    }

    /// <summary>Chance in 100 of a killing blow (ninjas barehanded, or a weapon that severs heads).</summary>
    public int CriticalChance
    {
        get
        {
            if (Weapon?.Def.Critical == true) return Math.Min(35, 10 + Level);
            if (Class == CharClass.Ninja && Weapon is null) return Math.Min(50, 2 * Level);
            return 0;
        }
    }

    public Family Slays => Equipped.Aggregate(Family.None, (f, i) => f | i.Def.Slays);

    public int Regeneration => Equipped.Sum(i => i.Def.Regenerate);

    public bool CanCast(School school) => school == School.Mage
        ? Class is CharClass.Mage or CharClass.Bishop or CharClass.Samurai
        : Class is CharClass.Priest or CharClass.Bishop or CharClass.Lord;

    public int[] Slots(School school) => school == School.Mage ? MageSlots : PriestSlots;

    public int MaxSlots(School school, int spellLevel)
    {
        int byLevel = RuleTables.SpellSlots(Class, school, spellLevel, Level);
        // Spells already known keep at least one cast each, even after a class change.
        int known = Content.Spells.Of(school, spellLevel).Count(s => Spells.Contains(s.Name));
        return Math.Min(9, Math.Max(byLevel, known));
    }

    public bool Knows(Spell s) => Spells.Contains(s.Name);

    public void RestoreSpells()
    {
        for (int l = 1; l <= 7; l++)
        {
            MageSlots[l - 1] = MaxSlots(School.Mage, l);
            PriestSlots[l - 1] = MaxSlots(School.Priest, l);
        }
    }

    public long ExperienceToNext => Math.Max(0, RuleTables.ExperienceFor(Class, Level + 1) - Experience);

    public bool CanEquip(ItemDef def, out string? why)
    {
        why = null;
        if (!def.Equippable) { why = "That can't be equipped."; return false; }
        if (!def.UsableBy(Class)) { why = $"A {Class.ToString().ToLowerInvariant()} can't use that."; return false; }
        if (def.Align is { } a && a != Alignment) { why = "It refuses your hand."; return false; }
        return true;
    }

    public override string ToString() => $"{Name} ({ClassTag} L{Level})";
}
