using Wizardry.Core.Content;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Combat;

/// <summary>One monster in a fight.</summary>
public sealed class Foe(MonsterDef def, int hp)
{
    public MonsterDef Def { get; } = def;
    public int Hp { get; set; } = hp;
    public int MaxHp { get; } = hp;
    public bool Asleep { get; set; }
    public bool Held { get; set; }
    public bool Silenced { get; set; }
    public bool Afraid { get; set; }
    /// <summary>Armour class worsened by spells (VELKA).</summary>
    public int AcPenalty { get; set; }
    public bool Dead => Hp <= 0;
    public int Ac => Def.Ac + AcPenalty;
    public bool CanAct => !Dead && !Asleep && !Held;
}

/// <summary>A group of the same kind of monster, as the party sees it ("3 Kobolds (2)").</summary>
public sealed class FoeGroup(MonsterDef def, List<Foe> members, bool identified)
{
    public MonsterDef Def { get; } = def;
    public List<Foe> Members { get; } = members;
    public bool Identified { get; set; } = identified;
    public IEnumerable<Foe> Alive => Members.Where(m => !m.Dead);
    public int AliveCount => Members.Count(m => !m.Dead);
    /// <summary>Monsters able to act this round (awake, not held).</summary>
    public int ActiveCount => Members.Count(m => m.CanAct);
    public string Name(int count) => Def.NameFor(count, Identified);
    public string Label => $"{AliveCount} {Name(AliveCount)} ({ActiveCount})";
    public string One => Def.NameFor(1, Identified);

    public static FoeGroup Spawn(MonsterDef def, int count, Rng rng, bool identified) =>
        new(def, [.. Enumerable.Range(0, Math.Clamp(count, 1, 9)).Select(_ => new Foe(def, Math.Max(1, rng.Roll(def.Hp))))], identified);
}

public enum TrapKind
{
    None,
    PoisonNeedle,
    GasBomb,
    CrossbowBolt,
    ExplodingBox,
    Stunner,
    Teleporter,
    MageBlaster,
    PriestBlaster,
    Alarm,
}

public static class Traps
{
    public static string Name(TrapKind t) => t switch
    {
        TrapKind.None => "TRAPLESS CHEST",
        TrapKind.PoisonNeedle => "POISON NEEDLE",
        TrapKind.GasBomb => "GAS BOMB",
        TrapKind.CrossbowBolt => "CROSSBOW BOLT",
        TrapKind.ExplodingBox => "EXPLODING BOX",
        TrapKind.Stunner => "STUNNER",
        TrapKind.Teleporter => "TELEPORTER",
        TrapKind.MageBlaster => "MAGE BLASTER",
        TrapKind.PriestBlaster => "PRIEST BLASTER",
        _ => "ALARM",
    };

    /// <summary>Deeper chests carry nastier traps.</summary>
    public static TrapKind Roll(int depth, Rng rng)
    {
        if (rng.Percent(Math.Max(10, 35 - depth * 3))) return TrapKind.None;
        var pool = new List<TrapKind> { TrapKind.PoisonNeedle, TrapKind.CrossbowBolt, TrapKind.GasBomb, TrapKind.Alarm };
        if (depth >= 2) pool.AddRange([TrapKind.Stunner, TrapKind.ExplodingBox]);
        if (depth >= 3) pool.Add(TrapKind.Teleporter);
        if (depth >= 4) pool.AddRange([TrapKind.MageBlaster, TrapKind.PriestBlaster]);
        return rng.Pick(pool);
    }
}

/// <summary>The chest left behind after some fights.</summary>
public sealed class Chest
{
    public TrapKind Trap { get; set; }
    public long Gold { get; set; }
    public List<int> Items { get; set; } = [];
    /// <summary>Trap names the party has guessed by inspecting (right or wrong).</summary>
    public List<string> Findings { get; } = [];
    public bool Opened { get; set; }

    public static Chest Roll(int depth, Rng rng, bool lair)
    {
        var chest = new Chest
        {
            Trap = Traps.Roll(depth, rng),
            Gold = rng.Roll(new Dice(depth + 1, 60 * depth, depth * 50)) * (lair ? 3 : 1),
        };
        int itemChance = lair ? 100 : 30 + depth * 4;
        if (rng.Percent(itemChance))
        {
            // Mostly items from about this deep; now and then something from deeper.
            int tier = Math.Clamp(depth + (rng.OneIn(6) ? 1 : 0), 1, 10);
            var pool = Content.Items.TreasureFor(tier).Where(i => i.Tier >= Math.Max(1, tier - 2)).ToList();
            if (pool.Count > 0) chest.Items.Add(rng.Pick(pool).Id);
        }
        return chest;
    }
}
