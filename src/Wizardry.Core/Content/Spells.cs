using Wizardry.Core.Rules;

namespace Wizardry.Core.Content;

/// <summary>What a spell is aimed at; the screen asks for a target only when it needs one.</summary>
public enum SpellTarget { Caster, Party, Member, Monster, Group, AllMonsters, None }

public enum SpellEffect
{
    Damage,         // dice of damage to the target(s)
    Heal,           // dice of healing
    FullHeal,       // all hit points and every ailment short of death
    Harm,           // dice of damage that ignores most resistance (priestly)
    Slay,           // kills one monster outright unless it resists
    SlayGroup,      // kills a group outright unless they resist (suffocation)
    SlayWeak,       // kills every monster below level 8
    Wither,         // a monster is left with 1d8 hit points
    DispelUndead,   // destroys undead
    Sleep,          // puts a group to sleep
    Hold,           // paralyses a group
    Silence,        // stops a group casting
    Fear,           // makes a group (or all) afraid
    Weaken,         // makes a group easier to hit (AC + 2)
    Ward,           // improves armour class (Value) for the fight
    TripWard,       // improves the party's armour class until it leaves the maze
    Light,          // lights the maze: see further and see secret doors
    LongLight,      // a light that lasts until leaving the maze
    Locate,         // shows the party's position
    IdentifyTrap,   // names a chest's trap
    IdentifyFoes,   // names every monster group
    CurePoison,
    CureParalysis,  // and sleep
    Raise,          // dead to alive (may fail and leave ashes)
    RaiseAshes,     // ashes to alive (may fail and leave the character lost)
    Teleport,       // moves the party within the maze
    Recall,         // returns the party to the castle, at the cost of its gold
}

/// <summary>One spell. Names are this game's own; the effects follow the 1981 spell list.</summary>
public sealed record Spell(
    string Name,
    School School,
    int Level,
    SpellTarget Target,
    SpellEffect Effect,
    string Summary,
    Dice Dice = default,
    int Value = 0,
    bool InCombat = true,
    bool InCamp = false);

public static class Spells
{
    public static readonly IReadOnlyList<Spell> All =
    [
        // Mage, level 1
        new("ARDO", School.Mage, 1, SpellTarget.Monster, SpellEffect.Damage, "A dart of fire: 1d8 to one foe.", new(1, 8)),
        new("SKUDA", School.Mage, 1, SpellTarget.Caster, SpellEffect.Ward, "Shields the caster: AC -2 for the fight.", Value: 2),
        new("SOMNA", School.Mage, 1, SpellTarget.Group, SpellEffect.Sleep, "Puts a group of foes to sleep."),
        new("LOKAR", School.Mage, 1, SpellTarget.None, SpellEffect.Locate, "Shows where the party stands in the maze.", InCombat: false, InCamp: true),
        // Mage, level 2
        new("VELKA", School.Mage, 2, SpellTarget.Group, SpellEffect.Weaken, "Clouds a group's sight: they are easier to hit (AC +2)."),
        new("SKUDAR", School.Mage, 2, SpellTarget.Caster, SpellEffect.Ward, "A stronger shield: caster AC -4 for the fight.", Value: 4),
        // Mage, level 3
        new("OMARDO", School.Mage, 3, SpellTarget.Group, SpellEffect.Damage, "A ball of fire: 4d6 to a group.", new(4, 6)),
        new("FULGA", School.Mage, 3, SpellTarget.Group, SpellEffect.Damage, "Forked lightning: 3d6 to a group.", new(3, 6)),
        // Mage, level 4
        new("TIMORA", School.Mage, 4, SpellTarget.Group, SpellEffect.Fear, "Fills a group with dread: they may flee."),
        new("GELUN", School.Mage, 4, SpellTarget.Group, SpellEffect.Damage, "A blast of frost: 6d6 to a group.", new(6, 6)),
        new("ARDORAN", School.Mage, 4, SpellTarget.Group, SpellEffect.Damage, "A firestorm: 6d6 to a group.", new(6, 6)),
        // Mage, level 5
        new("OMTIMORA", School.Mage, 5, SpellTarget.AllMonsters, SpellEffect.Fear, "Dread on every foe."),
        new("NEKRA", School.Mage, 5, SpellTarget.AllMonsters, SpellEffect.SlayWeak, "Kills every foe below level 8."),
        new("GELUNAR", School.Mage, 5, SpellTarget.Group, SpellEffect.Damage, "A killing frost: 8d8 to a group.", new(8, 8)),
        // Mage, level 6
        new("VAKUA", School.Mage, 6, SpellTarget.Group, SpellEffect.SlayGroup, "Draws the air from a group: they die unless they resist."),
        new("MORTIS", School.Mage, 6, SpellTarget.Monster, SpellEffect.DispelUndead, "Unmakes one undead foe."),
        new("OMSKUDA", School.Mage, 6, SpellTarget.Party, SpellEffect.Ward, "Shields the whole party: AC -4 for the fight.", Value: 4),
        // Mage, level 7
        new("PASSAR", School.Mage, 7, SpellTarget.None, SpellEffect.Teleport, "Moves the party to a chosen place in the maze.", InCamp: true),
        new("NOVARDO", School.Mage, 7, SpellTarget.AllMonsters, SpellEffect.Damage, "A star falls: 10d15 to every foe.", new(10, 15)),

        // Priest, level 1
        new("MUNIR", School.Priest, 1, SpellTarget.Party, SpellEffect.Ward, "Blesses the party: AC -1 for the fight.", Value: 1),
        new("SANA", School.Priest, 1, SpellTarget.Member, SpellEffect.Heal, "Heals 1d8.", new(1, 8), InCamp: true),
        new("NOSA", School.Priest, 1, SpellTarget.Monster, SpellEffect.Harm, "Wounds one foe: 1d8.", new(1, 8)),
        new("LUMA", School.Priest, 1, SpellTarget.None, SpellEffect.Light, "A light: see further, and see secret doors, for a while.", InCombat: false, InCamp: true),
        new("SKUTA", School.Priest, 1, SpellTarget.Caster, SpellEffect.Ward, "Shields the caster: AC -4 for the fight.", Value: 4),
        // Priest, level 2
        new("OMMUNIR", School.Priest, 2, SpellTarget.Party, SpellEffect.Ward, "Blesses the party: AC -2 for the fight.", Value: 2),
        new("SPEKIA", School.Priest, 2, SpellTarget.None, SpellEffect.IdentifyTrap, "Names the trap on a chest (most of the time).", InCombat: false),
        new("STASA", School.Priest, 2, SpellTarget.Group, SpellEffect.Hold, "Holds a group fast (paralysis)."),
        new("SILEN", School.Priest, 2, SpellTarget.Group, SpellEffect.Silence, "Silences a group: they can't cast."),
        // Priest, level 3
        new("LUMAR", School.Priest, 3, SpellTarget.None, SpellEffect.LongLight, "A lasting light, until the party leaves the maze.", InCombat: false, InCamp: true),
        new("SOLVA", School.Priest, 3, SpellTarget.Member, SpellEffect.CureParalysis, "Frees one member from paralysis or sleep.", InCamp: true),
        new("NOMEN", School.Priest, 3, SpellTarget.None, SpellEffect.IdentifyFoes, "Names every group of foes."),
        new("MUNIRAN", School.Priest, 3, SpellTarget.Party, SpellEffect.Ward, "A great blessing: party AC -4 for the fight.", Value: 4),
        // Priest, level 4
        new("SANAR", School.Priest, 4, SpellTarget.Member, SpellEffect.Heal, "Heals 2d8.", new(2, 8), InCamp: true),
        new("NOSAR", School.Priest, 4, SpellTarget.Monster, SpellEffect.Harm, "Wounds one foe: 2d8.", new(2, 8)),
        new("PURGA", School.Priest, 4, SpellTarget.Member, SpellEffect.CurePoison, "Cures one member's poison.", InCamp: true),
        new("OMSKUTA", School.Priest, 4, SpellTarget.Party, SpellEffect.TripWard, "Party AC -2 until it leaves the maze.", Value: 2, InCombat: false, InCamp: true),
        // Priest, level 5
        new("SANARA", School.Priest, 5, SpellTarget.Member, SpellEffect.Heal, "Heals 3d8.", new(3, 8), InCamp: true),
        new("NOSARA", School.Priest, 5, SpellTarget.Monster, SpellEffect.Harm, "Wounds one foe: 3d8.", new(3, 8)),
        new("IGNIS", School.Priest, 5, SpellTarget.Group, SpellEffect.Damage, "A column of holy flame: 3d8 to a group.", new(3, 8)),
        new("VITA", School.Priest, 5, SpellTarget.Member, SpellEffect.Raise, "Raises the dead (it can fail, leaving ashes).", InCombat: false, InCamp: true),
        new("MORTA", School.Priest, 5, SpellTarget.Monster, SpellEffect.Slay, "Slays one foe unless it resists."),
        // Priest, level 6
        new("LAMINA", School.Priest, 6, SpellTarget.Group, SpellEffect.Damage, "Whirling blades: 6d6 to a group.", new(6, 6)),
        new("SANCTA", School.Priest, 6, SpellTarget.Member, SpellEffect.FullHeal, "Heals fully and cures every ailment short of death.", InCamp: true),
        new("EXSANA", School.Priest, 6, SpellTarget.Monster, SpellEffect.Wither, "Leaves one foe with 1d8 hit points."),
        new("REDIRA", School.Priest, 6, SpellTarget.None, SpellEffect.Recall, "Returns the party to the castle. Its gold stays behind.", InCamp: true),
        // Priest, level 7
        new("CAELUM", School.Priest, 7, SpellTarget.AllMonsters, SpellEffect.Damage, "Wrath of the heavens: 12d6 to every foe.", new(12, 6)),
        new("VITARA", School.Priest, 7, SpellTarget.Member, SpellEffect.RaiseAshes, "Raises from ashes (or death) and heals fully.", InCombat: false, InCamp: true),
    ];

    private static readonly Dictionary<string, Spell> ByName = All.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

    public static Spell Get(string name) => ByName[name];

    public static Spell? Find(string name) => ByName.GetValueOrDefault(name.Trim());

    public static IEnumerable<Spell> Of(School school, int level) => All.Where(s => s.School == school && s.Level == level);
}
