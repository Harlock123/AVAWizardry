using Wizardry.Core.Content;
using Wizardry.Core.Model;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Combat;

/// <summary>What a spell asks of the game beyond its direct effect.</summary>
public enum SpellOutcome { None, Locate, Teleport, Recall, IdentifyTrap }

/// <summary>
/// Spell effects, shared by camp, combat, items and the monsters' own casting. Combat spells need a
/// <see cref="Battle"/>; camp spells pass null.
/// </summary>
public static class Magic
{
    /// <summary>
    /// Can <paramref name="c"/> cast <paramref name="spell"/> now: knows it, has a cast of that level
    /// left, and is somewhere it works (combat or camp)?
    /// </summary>
    public static bool CanCast(Character c, Spell spell, bool inCombat, out string? why)
    {
        why = null;
        if (!c.Status.CanAct()) { why = $"{c.Name} can't cast right now."; return false; }
        if (!c.Knows(spell)) { why = $"{c.Name} doesn't know {spell.Name}."; return false; }
        if (c.Slots(spell.School)[spell.Level - 1] <= 0) { why = $"{c.Name} has no level {spell.Level} {(spell.School == School.Mage ? "mage" : "priest")} spells left."; return false; }
        if (inCombat && !spell.InCombat) { why = $"{spell.Name} can't be cast in combat."; return false; }
        if (!inCombat && !spell.InCamp) { why = $"{spell.Name} can only be cast in combat."; return false; }
        return true;
    }

    /// <summary>Spends the cast and applies the spell. Anti-magic squares swallow it.</summary>
    public static SpellOutcome Cast(Character caster, Spell spell, World world, Rng rng, Battle? battle,
        FoeGroup? group, Character? member, List<string> log, bool fromItem = false, bool antiMagic = false)
    {
        if (!fromItem) caster.Slots(spell.School)[spell.Level - 1]--;
        log.Add($"{caster.Name} {(fromItem ? "uses an item:" : "casts")} {spell.Name}.");
        if (antiMagic)
        {
            log.Add("The spell fizzles in the dead air!");
            return SpellOutcome.None;
        }
        return Apply(spell, world, rng, battle, group, member, log, caster);
    }

    internal static SpellOutcome Apply(Spell spell, World world, Rng rng, Battle? battle, FoeGroup? group,
        Character? member, List<string> log, Character? caster)
    {
        var party = world.Members.ToList();
        switch (spell.Effect)
        {
            case SpellEffect.Damage:
            case SpellEffect.Harm:
                foreach (var g in Targets(spell, battle, group))
                    foreach (var foe in g.Alive.ToList())
                    {
                        if (spell.Effect == SpellEffect.Damage && rng.Percent(foe.Def.MagicResist))
                        {
                            log.Add($"The {g.One} resists!");
                            continue;
                        }
                        int dmg = rng.Roll(spell.Dice);
                        battle!.DamageFoe(g, foe, dmg, log, caster);
                        if (spell.Target == SpellTarget.Monster) break;
                    }
                break;

            case SpellEffect.Heal:
                if (member is { IsAlive: true })
                {
                    int amount = rng.Roll(spell.Dice);
                    member.Hp = Math.Min(member.MaxHp, member.Hp + amount);
                    log.Add($"{member.Name} is healed{(member.Hp == member.MaxHp ? " fully" : "")}.");
                }
                break;

            case SpellEffect.FullHeal:
                if (member is { IsAlive: true })
                {
                    member.Hp = member.MaxHp;
                    member.Poisoned = false;
                    if (member.Status is Status.Asleep or Status.Paralyzed or Status.Afraid or Status.Stoned) member.Status = Status.Ok;
                    log.Add($"{member.Name} is whole again.");
                }
                break;

            case SpellEffect.Slay:
                if (Targets(spell, battle, group).FirstOrDefault()?.Alive.FirstOrDefault() is { } victim && group is not null)
                {
                    if (victim.Def.Unique || rng.Percent(victim.Def.Level * 4 + victim.Def.MagicResist)) log.Add($"The {group.One} resists!");
                    else battle!.DamageFoe(group, victim, victim.Hp, log, caster);
                }
                break;

            case SpellEffect.SlayGroup:
                foreach (var g in Targets(spell, battle, group))
                    foreach (var foe in g.Alive.ToList())
                    {
                        if (foe.Def.Unique || foe.Def.Family.HasFlag(Family.Undead) || rng.Percent(foe.Def.Level * 5 + foe.Def.MagicResist))
                            log.Add($"The {g.One} resists!");
                        else battle!.DamageFoe(g, foe, foe.Hp, log, caster);
                    }
                break;

            case SpellEffect.SlayWeak:
                foreach (var g in battle?.Groups.ToList() ?? [])
                    foreach (var foe in g.Alive.ToList())
                        if (foe.Def.Level < 8 && !foe.Def.Unique && !rng.Percent(foe.Def.MagicResist))
                            battle!.DamageFoe(g, foe, foe.Hp, log, caster);
                break;

            case SpellEffect.Wither:
                if (group?.Alive.FirstOrDefault() is { } w)
                {
                    if (rng.Percent(w.Def.MagicResist)) log.Add($"The {group.One} resists!");
                    else
                    {
                        w.Hp = Math.Min(w.Hp, rng.Range(1, 8));
                        log.Add($"The {group.One} withers!");
                    }
                }
                break;

            case SpellEffect.DispelUndead:
                if (group?.Alive.FirstOrDefault() is { } u)
                {
                    if (!u.Def.Family.HasFlag(Family.Undead)) log.Add($"The {group.One} is not undead.");
                    else if (u.Def.Unique || rng.Percent(u.Def.MagicResist / 2)) log.Add($"The {group.One} resists!");
                    else battle!.DamageFoe(group, u, u.Hp, log, caster);
                }
                break;

            case SpellEffect.Sleep:
            case SpellEffect.Hold:
            case SpellEffect.Silence:
            case SpellEffect.Fear:
            case SpellEffect.Weaken:
                foreach (var g in Targets(spell, battle, group))
                {
                    int affected = 0;
                    foreach (var foe in g.Alive)
                        if (Affects(spell.Effect, foe, rng))
                        {
                            affected++;
                            switch (spell.Effect)
                            {
                                case SpellEffect.Sleep: foe.Asleep = true; break;
                                case SpellEffect.Hold: foe.Held = true; break;
                                case SpellEffect.Silence: foe.Silenced = true; break;
                                case SpellEffect.Fear: foe.Afraid = true; break;
                                default: foe.AcPenalty += 2; break;
                            }
                        }
                    string what = spell.Effect switch
                    {
                        SpellEffect.Sleep => "fall asleep",
                        SpellEffect.Hold => "are held fast",
                        SpellEffect.Silence => "are silenced",
                        SpellEffect.Fear => "are afraid",
                        _ => "are blinded",
                    };
                    log.Add(affected == 0 ? $"The {g.Name(2)} are not affected." : $"{affected} {g.Name(affected)} {(affected == 1 ? Singular(what) : what)}!");
                }
                break;

            case SpellEffect.Ward:
                if (spell.Target == SpellTarget.Party)
                {
                    foreach (var m in party.Where(m => m.IsAlive)) m.FightWard += spell.Value;
                    log.Add("The party is shielded.");
                }
                else if (caster is not null)
                {
                    caster.FightWard += spell.Value;
                    log.Add($"{caster.Name} is shielded.");
                }
                break;

            case SpellEffect.TripWard:
                world.TripWard = Math.Max(world.TripWard, spell.Value);
                log.Add("A lasting blessing settles on the party.");
                break;

            case SpellEffect.Light:
                world.LightSteps = Math.Max(world.LightSteps, 60);
                log.Add("A soft light fills the maze.");
                break;

            case SpellEffect.LongLight:
                world.LongLight = true;
                log.Add("A steady light fills the maze.");
                break;

            case SpellEffect.Locate:
                return SpellOutcome.Locate;

            case SpellEffect.IdentifyTrap:
                return SpellOutcome.IdentifyTrap;

            case SpellEffect.IdentifyFoes:
                foreach (var g in battle?.Groups ?? []) g.Identified = true;
                log.Add("The foes are known.");
                break;

            case SpellEffect.CurePoison:
                if (member is not null)
                {
                    member.Poisoned = false;
                    log.Add($"{member.Name} is no longer poisoned.");
                }
                break;

            case SpellEffect.CureParalysis:
                if (member is { Status: Status.Paralyzed or Status.Asleep })
                {
                    member.Status = Status.Ok;
                    log.Add($"{member.Name} can move again.");
                }
                else if (member is not null) log.Add("Nothing happens.");
                break;

            case SpellEffect.Raise:
                if (member is { Status: Status.Dead })
                {
                    if (rng.Percent(Math.Clamp(50 + member.Vit * 3, 50, 95)))
                    {
                        member.Status = Status.Ok;
                        member.Hp = 1;
                        member.Stats[3] = Math.Max(3, member.Stats[3] - 1);
                        log.Add($"{member.Name} lives!");
                    }
                    else
                    {
                        member.Status = Status.Ashes;
                        log.Add($"{member.Name} crumbles to ashes!");
                    }
                }
                else if (member is not null) log.Add("Nothing happens.");
                break;

            case SpellEffect.RaiseAshes:
                if (member is { Status: Status.Dead or Status.Ashes })
                {
                    if (member.Status == Status.Dead || rng.Percent(Math.Clamp(40 + member.Vit * 3, 40, 90)))
                    {
                        member.Status = Status.Ok;
                        member.Hp = member.MaxHp;
                        member.Stats[3] = Math.Max(3, member.Stats[3] - 1);
                        log.Add($"{member.Name} lives!");
                    }
                    else
                    {
                        member.Status = Status.Lost;
                        log.Add($"{member.Name} is lost forever!");
                    }
                }
                else if (member is not null) log.Add("Nothing happens.");
                break;

            case SpellEffect.Teleport:
                return SpellOutcome.Teleport;

            case SpellEffect.Recall:
                return SpellOutcome.Recall;
        }
        return SpellOutcome.None;
    }

    private static string Singular(string verb) => verb switch
    {
        "fall asleep" => "falls asleep",
        "are held fast" => "is held fast",
        "are silenced" => "is silenced",
        "are afraid" => "is afraid",
        _ => "is blinded",
    };

    private static bool Affects(SpellEffect effect, Foe foe, Rng rng)
    {
        if (foe.Def.Unique && effect != SpellEffect.Weaken) return false;
        if (rng.Percent(foe.Def.MagicResist)) return false;
        int level = foe.Def.Level;
        return effect switch
        {
            SpellEffect.Sleep => !foe.Def.ResistsSleep && rng.Percent(85 - level * 7),
            SpellEffect.Hold => rng.Percent(75 - level * 6),
            SpellEffect.Silence => rng.Percent(80 - level * 5),
            SpellEffect.Fear => rng.Percent(70 - level * 5),
            _ => true,
        };
    }

    private static IEnumerable<FoeGroup> Targets(Spell spell, Battle? battle, FoeGroup? group)
    {
        if (battle is null) return [];
        if (spell.Target == SpellTarget.AllMonsters) return battle.Groups.ToList();
        return group is null ? [] : [group];
    }

    /// <summary>The spells a monster of a given type picks from.</summary>
    public static IReadOnlyList<Spell> MonsterSpells(MonsterDef def)
    {
        var list = new List<Spell>();
        string[] mage = ["ARDO", "SOMNA", "ARDO", "FULGA", "OMARDO", "GELUN", "ARDORAN", "GELUNAR", "NOVARDO"];
        string[] priest = ["NOSA", "STASA", "NOSAR", "IGNIS", "NOSARA", "LAMINA"];
        foreach (var n in mage)
        {
            var s = Spells.Get(n);
            if (s.Level <= def.Mage) list.Add(s);
        }
        foreach (var n in priest)
        {
            var s = Spells.Get(n);
            if (s.Level <= def.Priest) list.Add(s);
        }
        return list;
    }
}
