using Wizardry.Core.Content;
using Wizardry.Core.Model;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Combat;

public enum ActionKind { Fight, Parry, Spell, Use, Dispel, Hide, Run }

/// <summary>One party member's order for the round.</summary>
public sealed record PartyAction(
    Character Who,
    ActionKind Kind,
    int Group = 0,
    Spell? Spell = null,
    Character? Target = null,
    OwnedItem? Item = null);

public enum BattleOutcome { Ongoing, Won, Fled, Wiped, Teleported, Recalled }

/// <summary>
/// A fight between the party and up to four groups of monsters, a round at a time: the party
/// gives its orders, then everyone acts in order of initiative.
/// </summary>
public sealed class Battle
{
    public const int MaxGroups = 4;
    /// <summary>Only the first three living members can melee, and be hit in melee.</summary>
    public const int FrontRank = 3;

    private readonly World _world;
    private readonly Rng _rng;
    private readonly HashSet<string> _hidden = [];
    private readonly Dictionary<string, int> _parry = [];

    public Battle(World world, Rng rng, IEnumerable<FoeGroup> groups, bool lair, bool chest, bool antiMagic)
    {
        _world = world;
        _rng = rng;
        Groups = [.. groups.Take(MaxGroups)];
        IsLair = lair;
        HasChest = chest;
        AntiMagic = antiMagic;
    }

    public List<FoeGroup> Groups { get; }
    public bool IsLair { get; }
    public bool HasChest { get; }
    public bool AntiMagic { get; }
    public int Round { get; private set; }
    public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
    /// <summary>Experience from monsters slain so far.</summary>
    public long ExperiencePool { get; private set; }
    public long GoldPool { get; private set; }
    /// <summary>Unique monsters slain (the Warden, Morthrain).</summary>
    public List<int> UniquesSlain { get; } = [];
    /// <summary>The monsters took the party by surprise (they get a free round first).</summary>
    public bool PartySurprised { get; private set; }
    /// <summary>The party took the monsters by surprise (it gets a free round; monsters don't act).</summary>
    public bool MonstersSurprised { get; private set; }

    public List<Character> Party => _world.Members.ToList();

    /// <summary>The members who can be hit in melee and can swing at the first groups.</summary>
    public List<Character> Front => Party.Where(c => c.IsAlive).Take(FrontRank).ToList();

    public bool InFront(Character c) => Front.Contains(c);

    public bool IsHidden(Character c) => _hidden.Contains(c.Id);

    /// <summary>Groups a member can melee: the first two from the front rank (any, from hiding).</summary>
    public IEnumerable<int> FightTargets(Character c)
    {
        if (IsHidden(c)) return Enumerable.Range(0, Groups.Count);
        return InFront(c) ? Enumerable.Range(0, Math.Min(2, Groups.Count)) : [];
    }

    /// <summary>Starts the fight; maybe one side surprises the other. Returns the opening lines.</summary>
    public List<string> Begin()
    {
        var log = new List<string>();
        int roll = _rng.Next(100);
        int partyAgi = (int)Party.Where(c => c.Status.CanAct()).Select(c => c.Agi).DefaultIfEmpty(10).Average();
        if (!IsLair && roll < 6 + RuleTables.AgilityBonus(partyAgi) * 2)
        {
            MonstersSurprised = true;
            log.Add("You surprised the monsters!");
        }
        else if (!IsLair && roll >= 94)
        {
            PartySurprised = true;
            log.Add("The monsters surprised you!");
            log.AddRange(MonstersAct(freeRound: true));
            CheckEnd(log);
        }
        return log;
    }

    /// <summary>Plays a round with the party's orders (one per member who can act).</summary>
    public List<string> PlayRound(IReadOnlyList<PartyAction> actions)
    {
        var log = new List<string>();
        if (Outcome != BattleOutcome.Ongoing) return log;
        Round++;
        _parry.Clear();

        // Running away is the whole party's choice, decided before anyone else acts.
        if (actions.Any(a => a.Kind == ActionKind.Run))
        {
            int monsters = Groups.Sum(g => g.ActiveCount);
            int chance = IsLair ? 0 : Math.Clamp(70 - monsters * 3 + RuleTables.AgilityBonus(Party.Max(c => c.Agi)) * 5, 10, 95);
            if (MonstersSurprised || _rng.Percent(chance))
            {
                log.Add("You run away!");
                Outcome = BattleOutcome.Fled;
                return log;
            }
            log.Add(IsLair ? "There is no escape!" : "You couldn't get away!");
            log.AddRange(MonstersAct(freeRound: true));
            EndOfRound(log);
            return log;
        }

        var turns = new List<(int Init, Action Act)>();
        foreach (var a in actions)
        {
            if (!a.Who.Status.CanAct()) continue;
            if (a.Kind == ActionKind.Parry) _parry[a.Who.Id] = 2;
            int init = _rng.Range(1, 10) + RuleTables.AgilityBonus(a.Who.Agi) * 2;
            var act = a;
            turns.Add((init, () => PartyActs(act, log)));
        }
        if (!MonstersSurprised)
        {
            foreach (var g in Groups)
                foreach (var foe in g.Members)
                {
                    var (fg, f) = (g, foe);
                    turns.Add((_rng.Range(1, 10) + Math.Min(5, foe.Def.Level / 2), () => FoeActs(fg, f, log)));
                }
        }
        MonstersSurprised = false;
        PartySurprised = false;

        foreach (var (_, act) in turns.OrderByDescending(t => t.Init))
        {
            if (Outcome != BattleOutcome.Ongoing) break;
            act();
            if (Outcome == BattleOutcome.Ongoing) CheckEnd(log, midRound: true);
        }
        if (Outcome == BattleOutcome.Ongoing) EndOfRound(log);
        return log;
    }

    private void EndOfRound(List<string> log)
    {
        foreach (var g in Groups)
            foreach (var foe in g.Alive)
            {
                if (foe.Def.Regenerate > 0) foe.Hp = Math.Min(foe.MaxHp, foe.Hp + foe.Def.Regenerate);
                if (foe.Asleep && _rng.Percent(25)) foe.Asleep = false;
                if (foe.Held && _rng.Percent(15)) foe.Held = false;
            }
        // Afraid monsters may slip away (and take their experience with them).
        foreach (var g in Groups)
            foreach (var foe in g.Alive.Where(f => f.Afraid && !f.Held && !f.Asleep).ToList())
                if (_rng.Percent(40))
                {
                    foe.Hp = 0;
                    log.Add($"A{(Vowel(g.One) ? "n" : "")} {g.One} flees!");
                }
        // Wounded groups may call for help.
        foreach (var g in Groups.ToList())
            if (g.Def.CallsForHelp > 0 && g.AliveCount is > 0 and < 9 && g.Members.Any(m => m.Dead || m.Hp < m.MaxHp)
                && _rng.Percent(g.Def.CallsForHelp))
            {
                g.Members.Add(new Foe(g.Def, Math.Max(1, _rng.Roll(g.Def.Hp))));
                log.Add($"The {g.Name(2)} call for help, and another arrives!");
            }
        foreach (var c in Party)
        {
            if (c.Status == Status.Asleep && _rng.Percent(50)) { c.Status = Status.Ok; log.Add($"{c.Name} wakes up."); }
            if (c.Poisoned && c.IsAlive && c.Status != Status.Stoned) Hurt(c, 1, log, "poison");
        }
        Groups.RemoveAll(g => g.AliveCount == 0);
        CheckEnd(log);
    }

    private void CheckEnd(List<string> log, bool midRound = false)
    {
        Groups.RemoveAll(g => g.AliveCount == 0);
        if (Groups.Count == 0)
        {
            Outcome = BattleOutcome.Won;
            return;
        }
        if (!Party.Any(c => c.IsAlive && c.Status is not (Status.Stoned or Status.Paralyzed)) && !midRound)
        {
            Outcome = BattleOutcome.Wiped;
            log.Add("Your party has fallen.");
        }
        else if (!Party.Any(c => c.IsAlive && c.Status is not (Status.Stoned or Status.Paralyzed)))
        {
            Outcome = BattleOutcome.Wiped;
            log.Add("Your party has fallen.");
        }
    }

    // ----- the party's turns -------------------------------------------------------------------

    private void PartyActs(PartyAction a, List<string> log)
    {
        var c = a.Who;
        if (!c.Status.CanAct()) return;
        switch (a.Kind)
        {
            case ActionKind.Fight:
                if (Groups.Count == 0) return;
                var g = Groups.ElementAtOrDefault(a.Group) is { AliveCount: > 0 } chosen ? chosen : Groups.First(x => x.AliveCount > 0);
                if (c.Status == Status.Afraid) { log.Add($"{c.Name} is too afraid to fight!"); return; }
                Melee(c, g, log);
                _hidden.Remove(c.Id);
                break;
            case ActionKind.Parry:
                break;
            case ActionKind.Hide:
                if (_rng.Percent(Math.Clamp(40 + c.Agi * 2 + c.Level * 2, 40, 95)))
                {
                    _hidden.Add(c.Id);
                    log.Add($"{c.Name} slips into the shadows.");
                }
                else log.Add($"{c.Name} fails to hide.");
                break;
            case ActionKind.Dispel:
                Dispel(c, Groups.ElementAtOrDefault(a.Group), log);
                break;
            case ActionKind.Spell when a.Spell is not null:
                if (!Magic.CanCast(c, a.Spell, inCombat: true, out var why)) { log.Add(why!); return; }
                var target = Groups.ElementAtOrDefault(a.Group) ?? Groups.FirstOrDefault();
                ResolveSpellOutcome(Magic.Cast(c, a.Spell, _world, _rng, this, target, a.Target, log, antiMagic: AntiMagic), log);
                break;
            case ActionKind.Use when a.Item is not null:
                UseItem(c, a.Item, Groups.ElementAtOrDefault(a.Group) ?? Groups.FirstOrDefault(), a.Target, log);
                break;
        }
    }

    private void ResolveSpellOutcome(SpellOutcome outcome, List<string> log)
    {
        if (outcome == SpellOutcome.Teleport) { Outcome = BattleOutcome.Teleported; log.Add("The party vanishes!"); }
        else if (outcome == SpellOutcome.Recall) { Outcome = BattleOutcome.Recalled; log.Add("The party is whisked away!"); }
        else if (outcome == SpellOutcome.Locate) log.Add("Nothing happens.");
    }

    private void UseItem(Character c, OwnedItem item, FoeGroup? group, Character? member, List<string> log)
    {
        if (!c.Items.Contains(item) || item.Def.Spell is not { } name) { log.Add("Nothing happens."); return; }
        var spell = Spells.Get(name);
        var outcome = Magic.Cast(c, spell, _world, _rng, this, group, member ?? c, log, fromItem: true, antiMagic: AntiMagic);
        if (_rng.Percent(item.Def.BreakChance))
        {
            c.Items.Remove(item);
            if (item.Def.Kind != ItemKind.Consumable) log.Add($"The {item.DisplayName} crumbles to dust.");
        }
        ResolveSpellOutcome(outcome, log);
    }

    private void Melee(Character c, FoeGroup g, List<string> log)
    {
        bool ambush = IsHidden(c);
        int hits = 0, damage = 0;
        Foe? target = null;
        for (int swing = 0; swing < c.Swings; swing++)
        {
            target = g.Alive.FirstOrDefault(f => f == target) ?? PickFoe(g);
            if (target is null) break;
            int roll = _rng.Range(1, 20);
            int bonus = c.HitSkill + (target.Asleep || target.Held ? 5 : 0) + (ambush ? 5 : 0);
            bool hit = roll == 20 || (roll != 1 && roll + bonus + target.Ac >= 20);
            if (!hit) continue;
            hits++;
            int dmg = Math.Max(1, _rng.Roll(c.Damage) + RuleTables.StrengthBonus(c.Str));
            if ((c.Slays & target.Def.Family) != 0) dmg *= 2;
            if (ambush) dmg *= 2;
            if (c.CriticalChance > 0 && !target.Def.Unique && _rng.Percent(c.CriticalChance) && _rng.Percent(100 - target.Def.Level * 3))
            {
                log.Add($"{c.Name} strikes a mortal blow!");
                dmg = target.Hp;
            }
            damage += dmg;
            target.Asleep = false;
            DamageFoe(g, target, dmg, null, c, quiet: true);
            if (target.Dead) target = null;
        }
        string who = $"{c.Name} {(ambush ? "leaps from hiding at" : "attacks")} a{(Vowel(g.One) ? "n" : "")} {g.One}";
        log.Add(hits == 0 ? $"{who} and misses." : $"{who} and hits {Times(hits)} for {damage} damage!");
        foreach (var dead in g.Members.Where(f => f.Dead && !_reported.Contains(f)).ToList())
        {
            _reported.Add(dead);
            log.Add($"The {g.One} is killed!");
        }
    }

    private readonly HashSet<Foe> _reported = [];

    private Foe? PickFoe(FoeGroup g)
    {
        var alive = g.Alive.ToList();
        return alive.Count == 0 ? null : alive[_rng.Next(alive.Count)];
    }

    private void Dispel(Character c, FoeGroup? g, List<string> log)
    {
        if (g is null) return;
        if (!g.Def.Family.HasFlag(Family.Undead) || g.Def.Unique)
        {
            log.Add($"{c.Name} tries to dispel the {g.Name(2)}, but nothing happens.");
            return;
        }
        int chance = 50 + 5 * (c.Level - g.Def.Level) - (c.Class == CharClass.Priest ? 0 : 15);
        int gone = 0;
        foreach (var foe in g.Alive.ToList())
            if (_rng.Percent(Math.Clamp(chance, 5, 95)))
            {
                foe.Hp = 0;
                gone++;
                ExperiencePool += foe.Def.Experience / 2;
                _reported.Add(foe);
            }
        log.Add(gone == 0 ? $"{c.Name} fails to dispel the {g.Name(2)}." : $"{c.Name} dispels {gone} {g.Name(gone)}!");
    }

    /// <summary>Wounds a monster; on its death adds its experience and gold to the pools.</summary>
    internal void DamageFoe(FoeGroup g, Foe foe, int dmg, List<string>? log, Character? by, bool quiet = false)
    {
        if (foe.Dead) return;
        foe.Hp -= dmg;
        foe.Asleep = false;
        if (!quiet) log?.Add($"The {g.One} takes {dmg} damage{(foe.Dead ? " and is killed!" : ".")}");
        if (foe.Dead)
        {
            if (!quiet) _reported.Add(foe);
            ExperiencePool += foe.Def.Experience;
            GoldPool += foe.Def.Gold.Count > 0 ? _rng.Roll(foe.Def.Gold) * 2 : 0;
            if (foe.Def.Unique) UniquesSlain.Add(foe.Def.Id);
        }
    }

    // ----- the monsters' turns -----------------------------------------------------------------

    private List<string> MonstersAct(bool freeRound)
    {
        var log = new List<string>();
        foreach (var g in Groups.ToList())
            foreach (var foe in g.Members.ToList())
            {
                if (Outcome != BattleOutcome.Ongoing) return log;
                FoeActs(g, foe, log);
            }
        return log;
    }

    private void FoeActs(FoeGroup g, Foe foe, List<string> log)
    {
        if (!foe.CanAct || Outcome != BattleOutcome.Ongoing) return;
        if (foe.Afraid) return;
        var targets = Front.Where(c => !IsHidden(c)).ToList();
        if (targets.Count == 0) targets = Front;
        if (targets.Count == 0) return;
        var def = foe.Def;

        if (def.Breath != Breath.None && _rng.Percent(def.Unique ? 25 : 35))
        {
            Breathe(g, foe, log);
            return;
        }
        if ((def.Mage > 0 || def.Priest > 0) && !foe.Silenced && _rng.Percent(def.Unique ? 45 : Math.Min(45, 10 + def.Level * 5)))
        {
            CastAtParty(g, foe, log);
            return;
        }
        if (def.Attacks.Length == 0) return;
        var victim = targets[_rng.Next(targets.Count)];
        int hits = 0, damage = 0;
        foreach (var dice in def.Attacks)
        {
            if (!victim.IsAlive) break;
            int roll = _rng.Range(1, 20);
            int ac = victim.ArmorClass(_world.TripWard) - _parry.GetValueOrDefault(victim.Id);
            bool hit = roll == 20 || (roll != 1 && roll + def.Level + ac >= 21);
            if (!hit) continue;
            hits++;
            damage += Math.Max(1, _rng.Roll(dice));
        }
        string who = $"A{(Vowel(g.One) ? "n" : "")} {g.One} attacks {victim.Name}";
        if (hits == 0) { log.Add($"{who} and misses."); return; }
        log.Add($"{who} and hits {Times(hits)} for {damage} damage!");
        Hurt(victim, damage, log, null);
        if (victim.IsAlive) ApplyTouch(def, victim, log);
    }

    private void ApplyTouch(MonsterDef def, Character c, List<string> log)
    {
        int save = c.Luc * 2 + c.Level;
        if (def.Touch.HasFlag(Touch.Poison) && !c.Poisoned && !_rng.Percent(save))
        {
            c.Poisoned = true;
            log.Add($"{c.Name} is poisoned!");
        }
        if (def.Touch.HasFlag(Touch.Paralyze) && c.Status == Status.Ok && !_rng.Percent(save + 10))
        {
            c.Status = Status.Paralyzed;
            log.Add($"{c.Name} is paralyzed!");
        }
        if (def.Touch.HasFlag(Touch.Sleep) && c.Status == Status.Ok && !_rng.Percent(save))
        {
            c.Status = Status.Asleep;
            log.Add($"{c.Name} falls asleep!");
        }
        if (def.Touch.HasFlag(Touch.Stone) && c.Status.IsAlive() && c.Status != Status.Stoned && !_rng.Percent(save + 20))
        {
            c.Status = Status.Stoned;
            log.Add($"{c.Name} is turned to stone!");
        }
        if (def.Touch.HasFlag(Touch.Drain) && !_rng.Percent(save))
        {
            Progression.Drain(c);
            log.Add($"{c.Name} is drained of a level!");
        }
        if (def.Touch.HasFlag(Touch.Critical) && _rng.Percent(Math.Max(2, 12 - c.Luc / 2)))
        {
            log.Add($"{c.Name} is decapitated!");
            Kill(c, log);
        }
    }

    private void Breathe(FoeGroup g, Foe foe, List<string> log)
    {
        string kind = foe.Def.Breath switch
        {
            Breath.Fire => "fire",
            Breath.Cold => "frost",
            Breath.Poison => "poison",
            _ => "a petrifying gaze",
        };
        log.Add($"The {g.One} breathes {kind}!");
        foreach (var c in Party.Where(c => c.IsAlive && c.Status != Status.Stoned))
        {
            if (foe.Def.Breath == Breath.Stone)
            {
                if (!_rng.Percent(c.Luc * 3 + c.Level * 2 + 25)) { c.Status = Status.Stoned; log.Add($"{c.Name} is turned to stone!"); }
                continue;
            }
            int dmg = Math.Max(1, foe.Hp / 2);
            if (_rng.Percent(c.Luc * 2)) dmg /= 2;
            Hurt(c, dmg, log, null);
            if (foe.Def.Breath == Breath.Poison && c.IsAlive && !_rng.Percent(c.Luc * 2)) c.Poisoned = true;
        }
    }

    private void CastAtParty(FoeGroup g, Foe foe, List<string> log)
    {
        var spells = Magic.MonsterSpells(foe.Def);
        if (spells.Count == 0) return;
        // Stronger casters prefer their stronger spells.
        var spell = spells.Count > 2 && _rng.Percent(40) ? spells.OrderByDescending(s => s.Level).First() : spells[_rng.Next(spells.Count)];
        log.Add($"A{(Vowel(g.One) ? "n" : "")} {g.One} casts {spell.Name}!");
        if (AntiMagic) { log.Add("The spell fizzles in the dead air!"); return; }
        var living = Party.Where(c => c.IsAlive && c.Status != Status.Stoned).ToList();
        if (living.Count == 0) return;
        switch (spell.Effect)
        {
            case SpellEffect.Damage or SpellEffect.Harm:
                var hit = spell.Target == SpellTarget.Monster ? [living[_rng.Next(living.Count)]]
                    : spell.Target == SpellTarget.AllMonsters ? living
                    : living.Take(_rng.Range(2, 4)).ToList();
                foreach (var c in hit)
                {
                    int dmg = _rng.Roll(spell.Dice);
                    if (_rng.Percent(c.Luc * 2)) dmg /= 2;
                    Hurt(c, Math.Max(1, dmg), log, null);
                }
                break;
            case SpellEffect.Sleep:
                foreach (var c in living.OrderBy(_ => _rng.Next(100)).Take(_rng.Range(1, 3)).Where(c => c.Status == Status.Ok && !_rng.Percent(c.Luc * 3 + c.Level * 2)))
                {
                    c.Status = Status.Asleep;
                    log.Add($"{c.Name} falls asleep!");
                }
                break;
            case SpellEffect.Hold:
                foreach (var c in living.Take(3).Where(c => c.Status == Status.Ok && !_rng.Percent(c.Luc * 3 + c.Level * 2 + 10)))
                {
                    c.Status = Status.Paralyzed;
                    log.Add($"{c.Name} is held fast!");
                }
                break;
        }
    }

    // ----- harm to the party -------------------------------------------------------------------

    private void Hurt(Character c, int dmg, List<string> log, string? cause)
    {
        if (!c.IsAlive) return;
        c.Hp -= dmg;
        if (cause is not null) log.Add($"{c.Name} suffers {dmg} damage from {cause}.");
        if (c.Status == Status.Asleep) c.Status = Status.Ok;
        if (c.Hp <= 0) Kill(c, log);
    }

    private void Kill(Character c, List<string> log)
    {
        c.Hp = 0;
        c.Status = Status.Dead;
        c.Poisoned = false;
        c.Deaths++;
        _hidden.Remove(c.Id);
        log.Add($"{c.Name} is killed!");
    }

    private static bool Vowel(string s) => s.Length > 0 && "AEIOU".Contains(char.ToUpperInvariant(s[0]));

    private static string Times(int n) => n == 1 ? "once" : n == 2 ? "twice" : $"{n} times";
}
