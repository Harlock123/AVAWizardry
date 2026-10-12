using Wizardry.Core.Combat;
using Wizardry.Core.Content;
using Wizardry.Core.Model;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Screens;

/// <summary>A character's sheet, with what can be done with their gear and spells.</summary>
internal sealed class SheetState(Game game, Character who, bool inMaze) : State(game)
{
    public static List<Line> Describe(Character c, World w)
    {
        var lines = new List<Line>
        {
            new($"{c.Name}{(c.Honors > 0 ? " >" : "")}   {c.Alignment.ToString().ToUpperInvariant()} {c.Race.ToString().ToUpperInvariant()} {c.Class.ToString().ToUpperInvariant()}", Tone.Gold),
            "",
            $"STR {c.Str,2}   LEVEL {c.Level,-4} AGE {c.AgeYears,-3}  AC {c.ArmorClass(w.TripWard),3}",
            $"I.Q.{c.Iq,3}   EXP {c.Experience,-12:N0} NEXT {c.ExperienceToNext:N0}",
            $"PIE {c.Pie,2}   HITS {c.Hp}/{c.MaxHp}",
            $"VIT {c.Vit,2}   GOLD {c.Gold:N0}",
            $"AGI {c.Agi,2}   STATUS {c.Status.Label()}{(c.Poisoned ? " (POISONED)" : "")}",
            $"LUCK{c.Luc,3}   SWINGS {c.Swings}  DAMAGE {c.Damage}",
        };
        if (c.CanCast(School.Mage) || c.CanCast(School.Priest))
        {
            lines.Add("");
            if (c.CanCast(School.Mage)) lines.Add($"MAGE   {string.Join('/', c.MageSlots)}");
            if (c.CanCast(School.Priest)) lines.Add($"PRIEST {string.Join('/', c.PriestSlots)}");
        }
        lines.Add("");
        if (c.Items.Count == 0) lines.Add(new("(no items)", Tone.Dim));
        for (int i = 0; i < c.Items.Count; i++)
            lines.Add($"{i + 1}) {ItemLine(c, c.Items[i])}");
        return lines;
    }

    /// <summary>"*" equipped, "-" cursed and stuck, "?" unidentified, "#" can't be used by this class.</summary>
    public static string ItemLine(Character c, OwnedItem item)
    {
        char mark = item.Stuck ? '-' : item.Equipped ? '*' : !item.Identified ? '?' : !item.Def.UsableBy(c.Class) && item.Def.Equippable ? '#' : ' ';
        return $"{mark}{item.DisplayName}";
    }

    public override Screen Render()
    {
        bool able = who.Status.CanAct();
        bool party = W.Party.Contains(who.Id);
        bool canCast = able && who.Spells.Count > 0 && (inMaze || !W.InMaze);
        return new Screen
        {
            Title = "CHARACTER",
            Place = inMaze ? "maze" : "tavern",
            Pane = inMaze ? Pane.Maze : Pane.Castle,
            Lines = Describe(who, W),
            Options =
            [
                new("E", "Equip", who.Items.Any(i => i.Def.Equippable && who.CanEquip(i.Def, out _))),
                new("U", "Use an item", able && who.Items.Any(i => i.Def.Spell is not null && Spells.Get(i.Def.Spell).InCamp)),
                new("S", "Cast a spell", canCast && who.Spells.Any(s => Spells.Get(s).InCamp)),
                new("R", "Read the spellbook", who.Spells.Count > 0),
                new("T", "Trade", party && G.Party.Count > 1 && (who.Items.Count > 0 || who.Gold > 0)),
                new("D", "Drop an item", who.Items.Any(i => !i.Stuck)),
                new("I", "Identify an item", who.Class == CharClass.Bishop && able && who.Items.Any(i => !i.Identified)),
                new("ESC", "Leave"),
            ],
        };
    }

    public override void Key(string key)
    {
        var pane = inMaze ? Pane.Maze : Pane.Castle;
        string place = inMaze ? "maze" : "tavern";
        switch (key)
        {
            case "E":
                G.Push(new EquipState(G, who, pane, place));
                break;
            case "U":
                G.Push(new ItemPickState(G, who, "USE", "Use which item?",
                    i => i.Def.Spell is not null && Spells.Get(i.Def.Spell).InCamp, null, item => UseInCamp(item), pane, place));
                break;
            case "S":
                G.Push(new SpellPickState(G, who, inCombat: false, spell => CastInCamp(spell), pane));
                break;
            case "R":
                G.Push(new MessageState(G, "SPELLBOOK", Spellbook(who), null, pane));
                break;
            case "T":
                Trade();
                break;
            case "D":
                G.Push(new ItemPickState(G, who, "DROP", "Drop which item?", i => !i.Stuck, null, item =>
                {
                    if (item.Def.Kind == ItemKind.Special)
                    {
                        G.Tell("DROP", ["Something tells you to hold on to that."]);
                        return;
                    }
                    who.Items.Remove(item);
                    G.Say($"{who.Name} drops the {item.DisplayName}.");
                }, pane, place));
                break;
            case "I":
                G.Push(new ItemPickState(G, who, "IDENTIFY", "Identify which item?", i => !i.Identified, null, Identify, pane, place));
                break;
            case "ESC":
                G.Pop();
                break;
        }
    }

    public static List<Line> Spellbook(Character c)
    {
        var lines = new List<Line>();
        foreach (var school in new[] { School.Mage, School.Priest })
        {
            var known = Spells.All.Where(s => s.School == school && c.Knows(s)).ToList();
            if (known.Count == 0) continue;
            lines.Add(new(school == School.Mage ? "MAGE SPELLS" : "PRIEST SPELLS", Tone.Title));
            foreach (var s in known)
                lines.Add($"L{s.Level} {s.Name,-9} {s.Summary}{(s.InCamp && !s.InCombat ? " (camp)" : s.InCamp ? " (camp, combat)" : "")}");
            lines.Add("");
        }
        if (lines.Count == 0) lines.Add(new("No spells known.", Tone.Dim));
        return lines;
    }

    private void Identify(OwnedItem item)
    {
        int chance = Math.Min(95, 15 + who.Level * 5);
        var lines = new List<Line>();
        if (G.Rng.Percent(chance))
        {
            item.Identified = true;
            lines.Add(new($"Success! It is a {item.Def.Name}.", Tone.Good));
            if (item.Def.Notes.Length > 0) lines.Add(item.Def.Notes);
        }
        else lines.Add("The item keeps its secrets.");
        // A careless touch can bind a cursed item to the bishop.
        if (item.Def.Cursed && G.Rng.Percent(Math.Max(5, 35 - who.Level * 2)) && who.CanEquip(item.Def, out _))
        {
            foreach (var other in who.Items.Where(o => o.Equipped && o.Def.Kind == item.Def.Kind && !o.Stuck)) other.Equipped = false;
            item.Equipped = true;
            item.Identified = true;
            lines.Add(new($"Oops! The {item.Def.Name} is cursed, and it clings to {who.Name}!", Tone.Bad));
        }
        G.Tell("IDENTIFY", lines, pane: inMaze ? Pane.Maze : Pane.Castle);
    }

    private void Trade()
    {
        var others = G.Party.Where(c => c != who).ToList();
        G.Push(new PickState(G, "TRADE", "Trade with whom?", others, to =>
            G.Push(new TradeState(G, who, to, inMaze)), pane: inMaze ? Pane.Maze : Pane.Castle, place: inMaze ? "maze" : "tavern"));
    }

    private void UseInCamp(OwnedItem item)
    {
        var spell = Spells.Get(item.Def.Spell!);
        WithTarget(spell, target =>
        {
            var log = new List<string>();
            var outcome = Magic.Cast(who, spell, W, G.Rng, null, null, target, log, fromItem: true, antiMagic: AntiMagicHere());
            if (G.Rng.Percent(item.Def.BreakChance))
            {
                who.Items.Remove(item);
                if (item.Def.Kind != ItemKind.Consumable) log.Add($"The {item.DisplayName} crumbles to dust.");
            }
            item.Identified = true;
            Finish(log, outcome);
        });
    }

    private void CastInCamp(Spell spell)
    {
        if (!Magic.CanCast(who, spell, inCombat: false, out var why))
        {
            G.Tell("CAST", [new(why!, Tone.Bad)], pane: inMaze ? Pane.Maze : Pane.Castle);
            return;
        }
        if (!inMaze && spell.Effect is SpellEffect.Teleport or SpellEffect.Recall or SpellEffect.Locate or SpellEffect.Light or SpellEffect.LongLight or SpellEffect.TripWard)
        {
            G.Tell("CAST", ["That spell is no use in the castle."]);
            return;
        }
        WithTarget(spell, target =>
        {
            var log = new List<string>();
            var outcome = Magic.Cast(who, spell, W, G.Rng, null, null, target, log, antiMagic: AntiMagicHere());
            G.Play(SoundCue.Spell);
            Finish(log, outcome);
        });
    }

    private bool AntiMagicHere() => G.CurrentLevel?.FeatureAt(W.X, W.Y) == Maze.Feature.AntiMagic;

    private void WithTarget(Spell spell, Action<Character?> go)
    {
        if (spell.Target == SpellTarget.Member)
        {
            var candidates = spell.Effect is SpellEffect.Raise or SpellEffect.RaiseAshes
                ? G.Party.Where(c => c.Status is Status.Dead or Status.Ashes).ToList()
                : G.Party.Where(c => c.IsAlive).ToList();
            G.Push(new PickState(G, spell.Name, "On whom?", candidates, c => go(c), pane: inMaze ? Pane.Maze : Pane.Castle, place: inMaze ? "maze" : "tavern"));
        }
        else go(null);
    }

    private void Finish(List<string> log, SpellOutcome outcome)
    {
        var pane = inMaze ? Pane.Maze : Pane.Castle;
        switch (outcome)
        {
            case SpellOutcome.Locate:
                G.ShowLocation = true;
                log.Add($"The party is on level {W.Depth}, {W.X} east and {W.Y} north of the corner, facing {W.Facing.ToString().ToUpperInvariant()}.");
                break;
            case SpellOutcome.Recall:
                foreach (var c in G.Party) c.Gold = 0;
                G.ReturnToCastle("The party is whisked back to the castle, leaving its gold behind.");
                return;
            case SpellOutcome.Teleport:
                G.Pop();
                G.Push(new TeleportState(G));
                G.Tell("PASSAR", log.Select(l => new Line(l)), pane: pane);
                return;
            case SpellOutcome.IdentifyTrap:
                log.Add("There is no chest here.");
                break;
        }
        G.Tell("CAST", log.Select(l => new Line(l)), pane: pane);
    }
}

/// <summary>Equipping: pick an item to put on (or take off).</summary>
internal sealed class EquipState(Game game, Character who, Pane pane, string place) : State(game)
{
    public override Screen Render()
    {
        var lines = new List<Line> { $"{who.Name}: choose an item to equip or remove.", new("* worn   - cursed   ? unidentified   # can't use", Tone.Dim), "" };
        var options = new List<MenuOption>();
        for (int i = 0; i < who.Items.Count; i++)
        {
            var item = who.Items[i];
            bool ok = item.Def.Equippable && !item.Stuck && who.CanEquip(item.Def, out _);
            lines.Add(new($"{i + 1}) {SheetState.ItemLine(who, item),-24} {Stats(item)}", ok ? Tone.Normal : Tone.Dim));
            options.Add(new(Keyed(i), item.DisplayName, ok));
        }
        lines.Add("");
        lines.Add($"AC {who.ArmorClass(W.TripWard)}   SWINGS {who.Swings}   DAMAGE {who.Damage}");
        options.Add(new("ESC", "Done"));
        return new Screen { Title = "EQUIP", Lines = lines, Options = options, Pane = pane, Place = place };
    }

    private static string Stats(OwnedItem item)
    {
        if (!item.Identified) return "";
        var d = item.Def;
        return d.Kind switch
        {
            ItemKind.Weapon => $"{d.Damage}{(d.ToHit != 0 ? $" {d.ToHit:+0;-0} hit" : "")}",
            ItemKind.Consumable or ItemKind.Special => "",
            _ => d.Ac != 0 ? $"AC {-d.Ac:+0;-0}" : "",
        };
    }

    public override void Key(string key)
    {
        if (key == "ESC") { G.Pop(); return; }
        if (IndexOf(key) is not { } i || i >= who.Items.Count) return;
        var item = who.Items[i];
        if (item.Equipped)
        {
            item.Equipped = false;
            return;
        }
        var kind = item.Def.Kind;
        if (who.Items.Any(o => o.Stuck && o.Def.Kind == kind))
        {
            G.Tell("EQUIP", [new("A cursed item already holds that place.", Tone.Bad)], pane: pane);
            return;
        }
        foreach (var other in who.Items.Where(o => o.Equipped && o.Def.Kind == kind)) other.Equipped = false;
        item.Equipped = true;
        if (item.Def.Cursed)
        {
            item.Identified = true;
            G.Play(SoundCue.Trap);
            G.Tell("EQUIP", [new($"Oops! The {item.Def.Name} is cursed. It won't come off!", Tone.Bad)], pane: pane);
        }
    }
}

/// <summary>Hands an item or gold to another member.</summary>
internal sealed class TradeState(Game game, Character from, Character to, bool inMaze) : State(game)
{
    public override Screen Render()
    {
        var lines = new List<Line> { $"{from.Name} trades with {to.Name}.", $"{from.Name} has {Money(from.Gold)}.", "" };
        var options = new List<MenuOption>();
        for (int i = 0; i < from.Items.Count; i++)
        {
            var item = from.Items[i];
            bool ok = !item.Stuck && to.Items.Count < Character.MaxItems;
            lines.Add(new($"{i + 1}) {SheetState.ItemLine(from, item)}", ok ? Tone.Normal : Tone.Dim));
            options.Add(new(Keyed(i), item.DisplayName, ok));
        }
        options.Add(new("G", "Give all gold", from.Gold > 0));
        options.Add(new("ESC", "Done"));
        return new Screen { Title = "TRADE", Lines = lines, Options = options, Pane = inMaze ? Pane.Maze : Pane.Castle, Place = inMaze ? "maze" : "tavern" };
    }

    public override void Key(string key)
    {
        if (key == "ESC") { G.Pop(); return; }
        if (key == "G")
        {
            to.Gold += from.Gold;
            from.Gold = 0;
            return;
        }
        if (IndexOf(key) is not { } i || i >= from.Items.Count) return;
        var item = from.Items[i];
        from.Items.RemoveAt(i);
        item.Equipped = false;
        to.Items.Add(item);
        G.Say($"{from.Name} gives the {item.DisplayName} to {to.Name}.");
    }
}

/// <summary>Picks a known spell (castable ones first).</summary>
internal sealed class SpellPickState(Game game, Character who, bool inCombat, Action<Spell> picked, Pane pane) : State(game)
{
    private List<Spell> Choices() => Spells.All
        .Where(s => who.Knows(s) && (inCombat ? s.InCombat : s.InCamp))
        .OrderBy(s => s.School).ThenBy(s => s.Level)
        .ToList();

    public override Screen Render()
    {
        var spells = Choices();
        var lines = new List<Line>
        {
            $"{who.Name}'s spells. Casts left by level:",
            $"MAGE {string.Join('/', who.MageSlots)}   PRIEST {string.Join('/', who.PriestSlots)}",
            "",
        };
        var options = new List<MenuOption>();
        for (int i = 0; i < spells.Count && i < 35; i++)
        {
            var s = spells[i];
            bool ok = Magic.CanCast(who, s, inCombat, out _);
            lines.Add(new($"{Keyed(i)}) L{s.Level} {s.Name,-9} {s.Summary}", ok ? Tone.Normal : Tone.Dim));
            options.Add(new(Keyed(i), s.Name, ok));
        }
        if (spells.Count == 0) lines.Add(new("No spells that work here.", Tone.Dim));
        options.Add(new("ESC", "Back"));
        return new Screen { Title = "CAST A SPELL", Lines = lines, Options = options, Pane = pane, Place = W.InMaze ? "maze" : "tavern" };
    }

    public override void Key(string key)
    {
        if (key == "ESC") { G.Pop(); return; }
        var spells = Choices();
        int? index = key.Length == 1 && char.IsLetter(key[0]) ? key[0] - 'A' + 9 : IndexOf(key);
        if (index is not { } i || i >= spells.Count) return;
        G.Pop();
        picked(spells[i]);
    }

    public override void Text(string text)
    {
        if (Spells.Find(text) is not { } spell || !who.Knows(spell)) return;
        G.Pop();
        picked(spell);
    }
}
