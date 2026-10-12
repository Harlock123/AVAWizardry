using Wizardry.Core.Rules;

namespace Wizardry.Core.Content;

public enum ItemKind { Weapon, Armor, Shield, Helm, Gauntlets, Misc, Consumable, Special }

/// <summary>Monster families a weapon can be especially deadly against.</summary>
[Flags]
public enum Family
{
    None = 0,
    Undead = 1,
    Dragon = 2,
    Demon = 4,
    Giant = 8,
    Animal = 16,
    Insect = 32,
    Were = 64,
    Mage = 128,
    Priest = 256,
    Myth = 512,
    Humanoid = 1024,
}

/// <summary>
/// An item type. <see cref="Classes"/> lists who can equip it as class letters (F M P T B S L N);
/// an item someone can't use can still be carried and sold.
/// </summary>
public sealed record ItemDef(
    int Id,
    string Name,
    string UnknownName,
    ItemKind Kind,
    int Price,
    string Classes = "FMPTBSLN",
    Dice Damage = default,
    int ToHit = 0,
    int Ac = 0,
    bool Cursed = false,
    Alignment? Align = null,
    /// <summary>Spell cast when used (potions and scrolls always; some weapons and rings too).</summary>
    string? Spell = null,
    /// <summary>Chance in 100 that using it uses it up (100 for potions and scrolls).</summary>
    int BreakChance = 0,
    /// <summary>Extra swings per round (on top of the class's own).</summary>
    int ExtraSwings = 0,
    /// <summary>Hit points regained (or, if negative, lost) per step in the maze while equipped.</summary>
    int Regenerate = 0,
    Family Slays = Family.None,
    bool Critical = false,
    /// <summary>Dungeon level from which it can appear in treasure (0: shop only).</summary>
    int Tier = 0,
    /// <summary>How many the trading post has at the start (-1: never runs out; 0: none).</summary>
    int Stock = 0,
    string Notes = "")
{
    public bool UsableBy(CharClass c) => Classes.Contains(Letter(c));

    public bool Equippable => Kind is ItemKind.Weapon or ItemKind.Armor or ItemKind.Shield or ItemKind.Helm or ItemKind.Gauntlets or ItemKind.Misc;

    public static char Letter(CharClass c) => c switch
    {
        CharClass.Fighter => 'F',
        CharClass.Mage => 'M',
        CharClass.Priest => 'P',
        CharClass.Thief => 'T',
        CharClass.Bishop => 'B',
        CharClass.Samurai => 'S',
        CharClass.Lord => 'L',
        _ => 'N',
    };
}

public static class Items
{
    // Shorthands for class lists.
    private const string Any = "FMPTBSLN";
    private const string Martial = "FSLN";
    private const string Blades = "FTSLN";
    private const string Blunt = "FPBSLN";
    private const string Armoured = "FPSLN";
    private const string Light = "FPTBSLN";
    private const string NotMage = "FPTBSLN";

    public const int AmuletId = 900;
    public const int RibbonId = 901;

    public static readonly IReadOnlyList<ItemDef> All =
    [
        // Weapons
        new(1, "Long Sword", "?SWORD", ItemKind.Weapon, 25, Martial, new(1, 8), Tier: 1, Stock: -1),
        new(2, "Short Sword", "?SWORD", ItemKind.Weapon, 15, Blades, new(1, 6), Tier: 1, Stock: -1),
        new(3, "Anointed Mace", "?MACE", ItemKind.Weapon, 30, Blunt, new(2, 3), Tier: 1, Stock: -1),
        new(4, "Anointed Flail", "?MACE", ItemKind.Weapon, 150, "FPSLN", new(1, 7), Tier: 1, Stock: -1),
        new(5, "Staff", "?STAFF", ItemKind.Weapon, 10, Any, new(1, 5), Tier: 1, Stock: -1),
        new(6, "Dagger", "?DAGGER", ItemKind.Weapon, 5, "FMTSLN", new(1, 4), Tier: 1, Stock: -1),
        new(7, "Hand Axe", "?AXE", ItemKind.Weapon, 20, "FSLN", new(1, 6), ToHit: 1, Tier: 1, Stock: -1),
        new(8, "Battle Axe", "?AXE", ItemKind.Weapon, 60, "FSLN", new(2, 5), Tier: 2, Stock: 4),
        new(10, "Long Sword +1", "?SWORD", ItemKind.Weapon, 10000, Martial, new(1, 8, 1), ToHit: 1, Tier: 2, Stock: 1),
        new(11, "Short Sword +1", "?SWORD", ItemKind.Weapon, 9000, Blades, new(1, 6, 1), ToHit: 1, Tier: 2),
        new(12, "Mace +1", "?MACE", ItemKind.Weapon, 9500, Blunt, new(2, 4, 1), ToHit: 1, Tier: 2),
        new(13, "Staff +1", "?STAFF", ItemKind.Weapon, 2500, Any, new(1, 6, 1), ToHit: 1, Tier: 2),
        new(14, "Dagger +1", "?DAGGER", ItemKind.Weapon, 1500, "FMTSLN", new(1, 4, 1), ToHit: 1, Tier: 2),
        new(15, "Long Sword -1", "?SWORD", ItemKind.Weapon, 1000, Martial, new(1, 8, -1), ToHit: -1, Cursed: true, Tier: 1),
        new(16, "Mace -1", "?MACE", ItemKind.Weapon, 1000, Blunt, new(2, 3, -1), ToHit: -1, Cursed: true, Tier: 1),
        new(20, "Long Sword +2", "?SWORD", ItemKind.Weapon, 30000, Martial, new(1, 10, 2), ToHit: 2, Tier: 4),
        new(21, "Short Sword +2", "?SWORD", ItemKind.Weapon, 25000, Blades, new(1, 6, 2), ToHit: 2, Tier: 4),
        new(22, "Mace +2", "?MACE", ItemKind.Weapon, 25000, Blunt, new(2, 4, 2), ToHit: 2, Tier: 4),
        new(23, "Dagger of Speed", "?DAGGER", ItemKind.Weapon, 32000, "MTN", new(1, 4), ToHit: 1, ExtraSwings: 1, Tier: 5),
        new(24, "Staff of Embers", "?STAFF", ItemKind.Weapon, 35000, "MB", new(1, 6, 1), Spell: "ARDO", BreakChance: 10, Tier: 4, Notes: "Casts ARDO when used."),
        new(25, "Mace of Mending", "?MACE", ItemKind.Weapon, 40000, Blunt, new(2, 4, 2), ToHit: 2, Spell: "SANAR", BreakChance: 15, Tier: 5, Notes: "Casts SANAR when used."),
        new(26, "Wyrmbane Sword", "?SWORD", ItemKind.Weapon, 60000, Martial, new(2, 6, 1), ToHit: 2, Slays: Family.Dragon, Tier: 5),
        new(27, "Ghostbane Mace", "?MACE", ItemKind.Weapon, 55000, Blunt, new(2, 5, 1), ToHit: 2, Slays: Family.Undead, Tier: 5),
        new(28, "Long Sword +3", "?SWORD", ItemKind.Weapon, 100000, Martial, new(1, 10, 3), ToHit: 3, Tier: 7),
        new(29, "Dagger -2", "?DAGGER", ItemKind.Weapon, 2000, "FMTSLN", new(1, 3), ToHit: -2, Cursed: true, Tier: 3),
        new(30, "Moonblade Katana", "?SWORD", ItemKind.Weapon, 200000, "S", new(2, 6, 4), ToHit: 4, Critical: true, Tier: 8, Notes: "Can sever a foe's head."),
        new(31, "Sword of the Warden", "?SWORD", ItemKind.Weapon, 250000, "L", new(3, 6, 2), ToHit: 4, Align: Alignment.Good, Regenerate: 1, Slays: Family.Demon | Family.Undead, Tier: 9, Notes: "Heals its bearer as they walk."),
        new(32, "Thieving Dagger", "?DAGGER", ItemKind.Weapon, 150000, "TN", new(2, 4, 2), ToHit: 3, Critical: true, Tier: 8, Notes: "Can sever a foe's head."),
        new(33, "Stormbrand", "?SWORD", ItemKind.Weapon, 150000, Martial, new(3, 5, 2), ToHit: 3, Spell: "FULGA", BreakChance: 5, Tier: 8, Notes: "Casts FULGA when used."),
        new(34, "Hungry Blade", "?SWORD", ItemKind.Weapon, 5000, Martial, new(2, 4, 3), ToHit: 2, Cursed: true, Regenerate: -1, Tier: 6, Notes: "Feeds on its bearer."),

        // Armour
        new(40, "Robes", "?ROBE", ItemKind.Armor, 15, Any, Ac: 1, Tier: 1, Stock: -1),
        new(41, "Leather Armor", "?ARMOR", ItemKind.Armor, 50, Light, Ac: 2, Tier: 1, Stock: -1),
        new(42, "Chain Mail", "?ARMOR", ItemKind.Armor, 90, Armoured, Ac: 3, Tier: 1, Stock: -1),
        new(43, "Breastplate", "?ARMOR", ItemKind.Armor, 200, Armoured, Ac: 4, Tier: 1, Stock: -1),
        new(44, "Plate Mail", "?ARMOR", ItemKind.Armor, 750, Martial, Ac: 5, Tier: 2, Stock: -1),
        new(45, "Leather +1", "?ARMOR", ItemKind.Armor, 1500, Light, Ac: 3, Tier: 2, Stock: 2),
        new(46, "Chain Mail +1", "?ARMOR", ItemKind.Armor, 1500, Armoured, Ac: 4, Tier: 2, Stock: 1),
        new(47, "Plate Mail +1", "?ARMOR", ItemKind.Armor, 15000, Martial, Ac: 6, Tier: 3),
        new(48, "Chain Mail -1", "?ARMOR", ItemKind.Armor, 1500, Armoured, Ac: 2, Cursed: true, Tier: 2),
        new(49, "Robes +1", "?ROBE", ItemKind.Armor, 2000, Any, Ac: 2, Tier: 3),
        new(50, "Chain Mail +2", "?ARMOR", ItemKind.Armor, 30000, Armoured, Ac: 5, Tier: 4),
        new(51, "Plate Mail +2", "?ARMOR", ItemKind.Armor, 50000, Martial, Ac: 7, Tier: 5),
        new(52, "Elven Chain", "?ARMOR", ItemKind.Armor, 60000, "FPTSLN", Ac: 6, Tier: 6),
        new(53, "Lords' Garb", "?ARMOR", ItemKind.Armor, 300000, "L", Ac: 10, Align: Alignment.Good, Regenerate: 1, Tier: 9, Notes: "Heals its wearer as they walk."),
        new(54, "Plate Mail +3", "?ARMOR", ItemKind.Armor, 150000, Martial, Ac: 8, Tier: 7),

        // Shields, helms, gauntlets
        new(60, "Small Shield", "?SHIELD", ItemKind.Shield, 20, "FPTBSLN", Ac: 2, Tier: 1, Stock: -1),
        new(61, "Large Shield", "?SHIELD", ItemKind.Shield, 40, Armoured, Ac: 3, Tier: 1, Stock: -1),
        new(62, "Shield +1", "?SHIELD", ItemKind.Shield, 1500, Armoured, Ac: 3, Tier: 2, Stock: 1),
        new(63, "Shield +2", "?SHIELD", ItemKind.Shield, 7000, Armoured, Ac: 4, Tier: 4),
        new(64, "Shield -1", "?SHIELD", ItemKind.Shield, 1000, Armoured, Ac: 1, Cursed: true, Tier: 2),
        new(65, "Shield +3", "?SHIELD", ItemKind.Shield, 25000, Armoured, Ac: 5, Tier: 7),
        new(70, "Helm", "?HELM", ItemKind.Helm, 100, Armoured, Ac: 1, Tier: 1, Stock: -1),
        new(71, "Helm +1", "?HELM", ItemKind.Helm, 3000, Armoured, Ac: 2, Tier: 3),
        new(72, "Helm of Clarity", "?HELM", ItemKind.Helm, 50000, Any, Ac: 2, Spell: "SOLVA", BreakChance: 20, Tier: 6, Notes: "Casts SOLVA when used."),
        new(73, "Iron Gloves", "?GLOVES", ItemKind.Gauntlets, 6000, Armoured, Ac: 1, Tier: 2, Stock: 1),
        new(74, "Silver Gloves", "?GLOVES", ItemKind.Gauntlets, 60000, Armoured, Ac: 3, Tier: 6),

        // Rings and other wearables
        new(80, "Ring of Warding", "?RING", ItemKind.Misc, 20000, Any, Ac: 1, Tier: 3),
        new(81, "Ring of Mending", "?RING", ItemKind.Misc, 200000, Any, Regenerate: 1, Tier: 7, Notes: "Heals its wearer as they walk."),
        new(82, "Ring of Ruin", "?RING", ItemKind.Misc, 5000, Any, Cursed: true, Regenerate: -2, Tier: 5, Notes: "Drains its wearer as they walk."),
        new(83, "Ring of Cinders", "?RING", ItemKind.Misc, 75000, Any, Spell: "ARDORAN", BreakChance: 25, Tier: 6, Notes: "Casts ARDORAN when used."),
        new(84, "Amulet of Ward", "?AMULET", ItemKind.Misc, 40000, Any, Ac: 2, Tier: 5),

        // Potions and scrolls
        new(100, "Potion of Healing", "?POTION", ItemKind.Consumable, 300, Any, Spell: "SANA", BreakChance: 100, Tier: 1, Stock: -1),
        new(101, "Potion of Antidote", "?POTION", ItemKind.Consumable, 300, Any, Spell: "PURGA", BreakChance: 100, Tier: 1, Stock: -1),
        new(102, "Potion of Restoring", "?POTION", ItemKind.Consumable, 1500, Any, Spell: "SANAR", BreakChance: 100, Tier: 3),
        new(103, "Scroll of Embers", "?SCROLL", ItemKind.Consumable, 500, Any, Spell: "ARDO", BreakChance: 100, Tier: 1, Stock: 10),
        new(104, "Scroll of Slumber", "?SCROLL", ItemKind.Consumable, 500, Any, Spell: "SOMNA", BreakChance: 100, Tier: 1, Stock: 10),
        new(105, "Scroll of Light", "?SCROLL", ItemKind.Consumable, 300, Any, Spell: "LUMA", BreakChance: 100, Tier: 1, Stock: 10),
        new(106, "Scroll of Shielding", "?SCROLL", ItemKind.Consumable, 500, Any, Spell: "SKUDA", BreakChance: 100, Tier: 1),
        new(107, "Scroll of Fireball", "?SCROLL", ItemKind.Consumable, 2500, Any, Spell: "OMARDO", BreakChance: 100, Tier: 3),
        new(108, "Scroll of Dread", "?SCROLL", ItemKind.Consumable, 3000, Any, Spell: "TIMORA", BreakChance: 100, Tier: 4),
        new(109, "Scroll of Return", "?SCROLL", ItemKind.Consumable, 10000, Any, Spell: "REDIRA", BreakChance: 100, Tier: 5),
        new(110, "Potion of Life", "?POTION", ItemKind.Consumable, 25000, Any, Spell: "VITA", BreakChance: 100, Tier: 6),
        new(111, "Potion of Sanctity", "?POTION", ItemKind.Consumable, 30000, Any, Spell: "SANCTA", BreakChance: 100, Tier: 7),
        new(112, "Scroll of Frost", "?SCROLL", ItemKind.Consumable, 8000, Any, Spell: "GELUN", BreakChance: 100, Tier: 5),

        // Quest items
        new(RibbonId, "Warden's Ribbon", "?RIBBON", ItemKind.Special, 0, Any, Notes: "Opens the sealed stair on the fourth level."),
        new(AmuletId, "Amulet of Thal", "?AMULET", ItemKind.Special, 0, Any, Spell: "SANCTA", BreakChance: 0, Notes: "The prize of the Proving Grounds. Casts SANCTA when used."),
    ];

    private static readonly Dictionary<int, ItemDef> ById = All.ToDictionary(i => i.Id);

    public static ItemDef Get(int id) => ById[id];

    public static bool Exists(int id) => ById.ContainsKey(id);

    public static IEnumerable<ItemDef> TreasureFor(int dungeonLevel) =>
        All.Where(i => i.Tier > 0 && i.Tier <= dungeonLevel && i.Kind != ItemKind.Special);
}
