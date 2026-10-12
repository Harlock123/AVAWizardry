using Wizardry.Core.Combat;
using Wizardry.Core.Content;
using Wizardry.Core.Maze;
using Wizardry.Core.Model;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Screens;

/// <summary>A fight: orders for each member, then the round plays out.</summary>
internal sealed class CombatState : State
{
    private readonly Battle _battle;
    private readonly string? _roomKey;
    private List<string> _lastRound;
    private readonly List<PartyAction> _orders = [];

    public CombatState(Game game, Battle battle, string? roomKey, List<string> opening) : base(game)
    {
        _battle = battle;
        _roomKey = roomKey;
        _lastRound = opening;
        G.Battle = battle;
        foreach (var l in opening) G.Say(l);
        if (battle.Outcome != BattleOutcome.Ongoing) _pendingEnd = true;
    }

    private bool _pendingEnd;

    /// <summary>Members who still need orders this round.</summary>
    private List<Character> Actors => G.Party.Where(c => c.Status.CanAct()).ToList();

    private Character? CurrentActor => Actors.ElementAtOrDefault(_orders.Count);

    public override Screen Render()
    {
        var lines = new List<Line>();
        for (int i = 0; i < _battle.Groups.Count; i++)
            lines.Add(new($"{i + 1}) {_battle.Groups[i].Label}", Tone.Bad));
        lines.Add("");
        foreach (var l in _lastRound.TakeLast(14)) lines.Add(new(l, ToneOf(l)));
        var options = new List<MenuOption>();
        if (_pendingEnd || _battle.Outcome != BattleOutcome.Ongoing)
        {
            options.Add(new("ENTER", "Continue"));
            return new Screen { Title = "COMBAT", Pane = Pane.Combat, Place = "maze", Lines = lines, Options = options };
        }
        if (CurrentActor is not { } who)
        {
            lines.Add("");
            lines.Add(new("Nobody in the party can act!", Tone.Bad));
            options.Add(new("ENTER", "Continue"));
            return new Screen { Title = $"COMBAT - ROUND {_battle.Round + 1}", Pane = Pane.Combat, Place = "maze", Lines = lines, Options = options };
        }
        lines.Add("");
        lines.Add(new($"{who.Name}'s option?", Tone.Title));
        bool fights = _battle.FightTargets(who).Any();
        options.Add(new("F", "Fight", fights && who.Status != Status.Afraid));
        options.Add(new("P", "Parry"));
        options.Add(new("S", "Spell", who.Spells.Any(s => Magic.CanCast(who, Spells.Get(s), true, out _))));
        options.Add(new("U", "Use", who.Items.Any(i => i.Def.Spell is not null && Spells.Get(i.Def.Spell).InCombat)));
        options.Add(new("D", "Dispel", who.Class is CharClass.Priest or CharClass.Bishop or CharClass.Lord));
        options.Add(new("H", "Hide", who.Class is CharClass.Thief or CharClass.Ninja && !_battle.IsHidden(who)));
        options.Add(new("R", "Run"));
        options.Add(new("B", "Back", _orders.Count > 0));
        return new Screen { Title = $"COMBAT - ROUND {_battle.Round + 1}", Pane = Pane.Combat, Place = "maze", Lines = lines, Options = options };
    }

    private static Tone ToneOf(string line) =>
        line.Contains("killed", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("The ") ? Tone.Bad
        : line.StartsWith("The ") && line.Contains("killed") ? Tone.Good
        : line.Contains("misses") ? Tone.Dim
        : Tone.Normal;

    public override void Key(string key)
    {
        if (_pendingEnd || _battle.Outcome != BattleOutcome.Ongoing)
        {
            _pendingEnd = false;
            End();
            return;
        }
        if (CurrentActor is not { } who)
        {
            // Everyone is asleep or held: the monsters have their way this round.
            PlayRound();
            return;
        }
        switch (key)
        {
            case "F":
                var targets = _battle.FightTargets(who).ToList();
                if (targets.Count == 1) Order(new PartyAction(who, ActionKind.Fight, targets[0]));
                else G.Push(new GroupPickState(G, _battle, "FIGHT", "Attack which group?", targets, g => Order(new PartyAction(who, ActionKind.Fight, g))));
                break;
            case "P":
                Order(new PartyAction(who, ActionKind.Parry));
                break;
            case "S":
                G.Push(new SpellPickState(G, who, inCombat: true, spell => Aim(who, spell, null), Pane.Combat));
                break;
            case "U":
                G.Push(new ItemPickState(G, who, "USE", "Use which item?", i => i.Def.Spell is not null && Spells.Get(i.Def.Spell).InCombat, null,
                    item => Aim(who, Spells.Get(item.Def.Spell!), item), Pane.Combat, "maze"));
                break;
            case "D":
                var all = Enumerable.Range(0, _battle.Groups.Count).ToList();
                if (all.Count == 1) Order(new PartyAction(who, ActionKind.Dispel, 0));
                else G.Push(new GroupPickState(G, _battle, "DISPEL", "Dispel which group?", all, g => Order(new PartyAction(who, ActionKind.Dispel, g))));
                break;
            case "H":
                Order(new PartyAction(who, ActionKind.Hide));
                break;
            case "R":
                _orders.Clear();
                Order(new PartyAction(who, ActionKind.Run), runNow: true);
                break;
            case "B":
                if (_orders.Count > 0) _orders.RemoveAt(_orders.Count - 1);
                break;
        }
    }

    /// <summary>Asks for a spell's (or item's) target, then gives the order.</summary>
    private void Aim(Character who, Spell spell, OwnedItem? item)
    {
        var kind = item is null ? ActionKind.Spell : ActionKind.Use;
        switch (spell.Target)
        {
            case SpellTarget.Monster or SpellTarget.Group when _battle.Groups.Count > 1:
                G.Push(new GroupPickState(G, _battle, spell.Name, "At which group?", [.. Enumerable.Range(0, _battle.Groups.Count)],
                    g => Order(new PartyAction(who, kind, g, spell, null, item))));
                break;
            case SpellTarget.Member:
                G.Push(new PickState(G, spell.Name, "On whom?", G.Party.Where(c => c.IsAlive).ToList(),
                    c => Order(new PartyAction(who, kind, 0, spell, c, item)), pane: Pane.Combat, place: "maze"));
                break;
            default:
                Order(new PartyAction(who, kind, 0, spell, null, item));
                break;
        }
    }

    private void Order(PartyAction action, bool runNow = false)
    {
        _orders.Add(action);
        if (runNow || _orders.Count >= Actors.Count) PlayRound();
    }

    private void PlayRound()
    {
        var orders = _orders.ToList();
        _orders.Clear();
        _lastRound = _battle.PlayRound(orders);
        foreach (var l in _lastRound) G.Say(l);
        if (_lastRound.Any(l => l.Contains("casts"))) G.Play(SoundCue.Spell);
        if (_lastRound.Any(l => l.Contains("breathes"))) G.Play(SoundCue.Breath);
        var names = G.Party.Select(c => c.Name).ToList();
        bool partyHurt = _lastRound.Any(l => l.Contains(" hits ") && names.Any(n => l.Contains($"attacks {n} ")));
        if (_lastRound.Any(l => l.Contains(" is killed!") && !l.StartsWith("The "))) G.Play(SoundCue.Death);
        else if (_lastRound.Any(l => l.StartsWith("The ") && l.Contains("killed"))) G.Play(SoundCue.Kill);
        else if (partyHurt) G.Play(SoundCue.Hurt);
        else if (_lastRound.Any(l => l.Contains(" hits "))) G.Play(SoundCue.Hit);
        else G.Play(SoundCue.Miss);
        if (_lastRound.Any(l => l.Contains("paralyzed") || l.Contains("held fast"))) G.Play(SoundCue.Paralyzed);
        if (_lastRound.Any(l => l.Contains("drained"))) G.Play(SoundCue.Drain);
    }

    /// <summary>The fight is over: rewards, the chest, or the consequences.</summary>
    private void End()
    {
        foreach (var c in G.Party)
        {
            c.FightWard = 0;
            if (c.Status == Status.Afraid) c.Status = Status.Ok;
        }
        G.Battle = null;
        G.Pop();
        switch (_battle.Outcome)
        {
            case BattleOutcome.Won:
                Won();
                break;
            case BattleOutcome.Wiped:
                G.PartyWiped();
                break;
            case BattleOutcome.Fled:
                G.Say("The party escapes.");
                break;
            case BattleOutcome.Teleported:
                G.Teleport(W.Depth, random: true);
                G.Say("The party reappears somewhere else on the level.");
                break;
            case BattleOutcome.Recalled:
                foreach (var c in G.Party) c.Gold = 0;
                G.ReturnToCastle("The party is whisked back to the castle, leaving its gold behind.");
                break;
        }
    }

    private void Won()
    {
        G.ReorderFallen();
        var survivors = G.Party.Where(c => c.IsAlive && c.Status != Status.Stoned).ToList();
        var lines = new List<Line> { new("FOR WINNING THE BATTLE:", Tone.Title), "" };
        if (survivors.Count > 0)
        {
            long xp = _battle.ExperiencePool / survivors.Count;
            long gold = _battle.GoldPool / survivors.Count;
            foreach (var c in survivors)
            {
                c.Experience += xp;
                c.Gold += gold;
            }
            lines.Add($"Each survivor gets {xp:N0} experience points.");
            if (gold > 0) lines.Add($"Each survivor gets {gold:N0} gold pieces.");
        }
        if (_roomKey is not null) W.ClearedRooms.Add(_roomKey);
        Chest? chest = null;
        if (_battle.UniquesSlain.Contains(Monsters.Warden))
        {
            W.WardenDefeated = true;
            chest = Chest.Roll(W.Depth, G.Rng, lair: true);
            chest.Items.Clear();
            chest.Items.Add(Items.RibbonId);
            chest.Trap = TrapKind.None;
            lines.Add("");
            lines.Add(new("The Iron Warden topples. Among the wreckage lies a chest.", Tone.Gold));
        }
        else if (_battle.UniquesSlain.Contains(Monsters.Morthrain))
        {
            W.MorthrainDefeated = true;
            chest = Chest.Roll(W.Depth, G.Rng, lair: true);
            chest.Items.Insert(0, Items.AmuletId);
            chest.Trap = TrapKind.None;
            lines.Add("");
            lines.Add(new("Morthrain falls, and with a last curse he is gone. The Amulet of Thal lies on the floor.", Tone.Gold));
        }
        else if (_battle.HasChest)
            chest = Chest.Roll(W.Depth, G.Rng, lair: false);
        if (chest is not null) G.Chest = chest;
        G.Tell("VICTORY", lines, () =>
        {
            if (chest is not null) G.Push(new ChestState(G, chest));
        }, Pane.Combat);
    }
}

/// <summary>Picks a monster group.</summary>
internal sealed class GroupPickState(Game game, Battle battle, string title, string question, List<int> groups, Action<int> picked) : State(game)
{
    public override Screen Render()
    {
        var lines = new List<Line> { question, "" };
        var options = new List<MenuOption>();
        foreach (var g in groups)
        {
            lines.Add($"{g + 1}) {battle.Groups[g].Label}");
            options.Add(new(Keyed(g), battle.Groups[g].Name(2)));
        }
        options.Add(new("ESC", "Back"));
        return new Screen { Title = title, Lines = lines, Options = options, Pane = Pane.Combat, Place = "maze" };
    }

    public override void Key(string key)
    {
        if (key == "ESC") { G.Pop(); return; }
        if (IndexOf(key) is { } i && groups.Contains(i))
        {
            G.Pop();
            picked(i);
        }
    }
}

/// <summary>A chest: inspect, disarm, open, or leave it be.</summary>
internal sealed class ChestState(Game game, Chest chest) : State(game)
{
    public override Screen Render()
    {
        var lines = new List<Line> { "A chest! Will you open it?", "" };
        foreach (var f in chest.Findings) lines.Add(new(f, Tone.Info));
        var able = G.Party.Where(c => c.Status.CanAct()).ToList();
        bool spekia = able.Any(c => Magic.CanCast(c, Spells.Get("SPEKIA"), inCombat: false, out _) || (c.Knows(Spells.Get("SPEKIA")) && c.PriestSlots[1] > 0));
        return new Screen
        {
            Title = "A CHEST",
            Pane = Pane.Chest,
            Place = "maze",
            Lines = lines,
            Options =
            [
                new("O", "Open it", able.Count > 0),
                new("I", "Inspect it", able.Count > 0),
                new("D", "Disarm the trap", able.Count > 0),
                new("C", "Cast SPEKIA", spekia),
                new("ESC", "Leave it"),
            ],
        };
    }

    public override void Key(string key)
    {
        var able = G.Party.Where(c => c.Status.CanAct()).ToList();
        switch (key)
        {
            case "O":
                G.Push(new PickState(G, "OPEN", "Who will open it?", able, Open, pane: Pane.Chest, place: "maze"));
                break;
            case "I":
                G.Push(new PickState(G, "INSPECT", "Who will inspect it?", able, Inspect, pane: Pane.Chest, place: "maze"));
                break;
            case "D":
                G.Push(new PickState(G, "DISARM", "Who will try to disarm it?", able, who =>
                    G.Push(new TrapNameState(G, who, name => Disarm(who, name))), pane: Pane.Chest, place: "maze"));
                break;
            case "C":
                var caster = able.First(c => c.Knows(Spells.Get("SPEKIA")) && c.PriestSlots[1] > 0);
                caster.PriestSlots[1]--;
                G.Play(SoundCue.Spell);
                var guess = G.Rng.Percent(95) ? chest.Trap : (TrapKind)G.Rng.Next(Enum.GetValues<TrapKind>().Length);
                chest.Findings.Add($"{caster.Name} casts SPEKIA: {Traps.Name(guess)}.");
                break;
            case "ESC":
                G.Chest = null;
                G.Pop();
                break;
        }
    }

    private static bool Skilled(Character c) => c.Class is CharClass.Thief or CharClass.Ninja;

    private void Inspect(Character who)
    {
        int chance = Skilled(who) ? Math.Min(95, 50 + who.Agi * 2 + who.Level * 2) : Math.Min(40, who.Agi + who.Level);
        if (G.Rng.Percent(chance))
            chest.Findings.Add($"{who.Name} thinks it is: {Traps.Name(chest.Trap)}.");
        else
        {
            if (chest.Trap != TrapKind.None && G.Rng.Percent(Skilled(who) ? 5 : 15))
            {
                Spring(who, "Oops! The trap goes off.");
                return;
            }
            var wrong = (TrapKind)G.Rng.Next(Enum.GetValues<TrapKind>().Length);
            chest.Findings.Add($"{who.Name} thinks it is: {Traps.Name(wrong)}.");
        }
    }

    private void Disarm(Character who, TrapKind named)
    {
        if (named != chest.Trap)
        {
            Spring(who, $"{who.Name} fumbles with the wrong mechanism...");
            return;
        }
        if (chest.Trap == TrapKind.None)
        {
            chest.Findings.Add("There was no trap to disarm.");
            return;
        }
        int chance = Skilled(who) ? Math.Min(95, 60 + who.Level * 3 + who.Agi) : Math.Min(50, 20 + who.Level * 2);
        if (G.Rng.Percent(chance))
        {
            chest.Trap = TrapKind.None;
            chest.Findings.Add(new($"{who.Name} disarms the trap!"));
            G.Play(SoundCue.Treasure);
            Open(who, disarmed: true);
        }
        else if (G.Rng.Percent(50)) Spring(who, $"{who.Name} slips. The trap goes off!");
        else chest.Findings.Add($"{who.Name} fails to disarm it, but nothing happens.");
    }

    private void Open(Character who) => Open(who, disarmed: false);

    private void Open(Character who, bool disarmed)
    {
        if (!disarmed && chest.Trap != TrapKind.None)
        {
            Spring(who, $"{who.Name} opens the chest... a {Traps.Name(chest.Trap)}!");
            return;
        }
        Loot();
    }

    /// <summary>The trap goes off, then the chest's contents are there for the taking.</summary>
    private void Spring(Character who, string opening)
    {
        var trap = chest.Trap;
        chest.Trap = TrapKind.None;
        G.Play(SoundCue.Trap);
        var lines = new List<Line> { new(opening, Tone.Bad), new(Traps.Name(trap) + "!", Tone.Bad), "" };
        var party = G.Party.Where(c => c.IsAlive && c.Status != Status.Stoned).ToList();
        int depth = Math.Max(1, W.Depth);
        bool teleported = false, alarm = false;
        switch (trap)
        {
            case TrapKind.PoisonNeedle:
                who.Poisoned = true;
                lines.Add($"{who.Name} is poisoned!");
                break;
            case TrapKind.GasBomb:
                foreach (var c in party.Where(c => !G.Rng.Percent(c.Luc * 3)))
                {
                    c.Poisoned = true;
                    lines.Add($"{c.Name} is poisoned!");
                }
                break;
            case TrapKind.CrossbowBolt:
                Wound(who, G.Rng.Roll(new Dice(depth, 8)), lines);
                break;
            case TrapKind.ExplodingBox:
                foreach (var c in party)
                    Wound(c, G.Rng.Roll(new Dice(Math.Max(1, depth / 2 + 1), 8)) / (G.Rng.Percent(c.Luc * 3) ? 2 : 1), lines);
                break;
            case TrapKind.Stunner:
                who.Status = Status.Paralyzed;
                lines.Add($"{who.Name} is paralyzed!");
                break;
            case TrapKind.Teleporter:
                teleported = true;
                lines.Add("The party is teleported!");
                break;
            case TrapKind.MageBlaster:
            case TrapKind.PriestBlaster:
                var school = trap == TrapKind.MageBlaster ? School.Mage : School.Priest;
                foreach (var c in party.Where(c => c.CanCast(school) && !G.Rng.Percent(c.Luc * 3)))
                {
                    bool stone = G.Rng.Percent(25);
                    c.Status = stone ? Status.Stoned : Status.Paralyzed;
                    lines.Add($"{c.Name} is {(stone ? "turned to stone" : "paralyzed")}!");
                }
                break;
            case TrapKind.Alarm:
                alarm = true;
                lines.Add("A bell clangs! Monsters are coming!");
                break;
        }
        G.Tell("TRAP!", lines, () =>
        {
            if (!G.Party.Any(c => c.IsAlive && c.Status is not (Status.Stoned or Status.Paralyzed)))
            {
                G.Pop();
                G.PartyWiped();
                return;
            }
            Loot();
            if (teleported) G.Teleport(W.Depth, random: true);
            if (alarm) G.StartFight(roomKey: null);
        }, Pane.Chest);
    }

    private void Wound(Character c, int dmg, List<Line> lines)
    {
        dmg = Math.Max(1, dmg);
        c.Hp -= dmg;
        lines.Add($"{c.Name} takes {dmg} damage.");
        if (c.Hp <= 0)
        {
            c.Hp = 0;
            c.Status = Status.Dead;
            c.Poisoned = false;
            c.Deaths++;
            lines.Add(new($"{c.Name} is killed!", Tone.Bad));
        }
    }

    private void Loot()
    {
        chest.Opened = true;
        G.Chest = null;
        G.Pop();
        G.Play(SoundCue.Treasure);
        var lines = new List<Line>();
        var takers = G.Party.Where(c => c.IsAlive).ToList();
        if (takers.Count > 0 && chest.Gold > 0)
        {
            long each = chest.Gold / takers.Count;
            foreach (var c in takers) c.Gold += each;
            lines.Add($"Each member finds {each:N0} gold pieces.");
        }
        foreach (var id in chest.Items)
        {
            var def = Items.Get(id);
            bool special = def.Kind == ItemKind.Special;
            var owner = G.Party.Where(c => c.Items.Count < Character.MaxItems).OrderBy(_ => G.Rng.Next(100)).FirstOrDefault()
                ?? (special ? G.Party.FirstOrDefault() : null);
            if (owner is null)
            {
                lines.Add(new($"Nobody can carry the {def.UnknownName}; it is left behind.", Tone.Dim));
                continue;
            }
            var item = new OwnedItem { ItemId = id, Identified = special };
            owner.Items.Add(item);
            lines.Add(new($"{owner.Name} found a {item.DisplayName}.", special ? Tone.Gold : Tone.Normal));
        }
        if (lines.Count == 0) lines.Add("The chest is empty.");
        G.Tell("TREASURE", lines, pane: Pane.Chest);
    }
}

/// <summary>Names the trap to disarm, from the list of all traps.</summary>
internal sealed class TrapNameState(Game game, Character who, Action<TrapKind> picked) : State(game)
{
    private static readonly TrapKind[] Kinds = Enum.GetValues<TrapKind>();

    public override Screen Render()
    {
        var lines = new List<Line> { $"{who.Name} will disarm the trap. Which trap is it?", "" };
        var options = new List<MenuOption>();
        for (int i = 0; i < Kinds.Length; i++)
        {
            lines.Add($"{Keyed(i)}) {Traps.Name(Kinds[i])}");
            options.Add(new(Keyed(i), Traps.Name(Kinds[i])));
        }
        options.Add(new("ESC", "Back"));
        return new Screen { Title = "DISARM", Lines = lines, Options = options, Pane = Pane.Chest, Place = "maze" };
    }

    public override void Key(string key)
    {
        if (key == "ESC") { G.Pop(); return; }
        int? i = key.Length == 1 && char.IsLetter(key[0]) ? key[0] - 'A' + 9 : IndexOf(key);
        if (i is { } k && k < Kinds.Length)
        {
            G.Pop();
            picked(Kinds[k]);
        }
    }
}
