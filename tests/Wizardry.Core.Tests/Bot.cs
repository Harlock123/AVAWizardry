using Wizardry.Core.Combat;
using Wizardry.Core.Content;
using Wizardry.Core.Maze;
using Wizardry.Core.Model;
using Wizardry.Core.Rules;
using Wizardry.Core.Screens;

namespace Wizardry.Core.Tests;

/// <summary>
/// Plays the game through its screens, pressing keys as a player would: shops, rests, explores,
/// fights, opens chests, and sets out for the Warden and Morthrain when strong enough. Used to show
/// that the game can be won, and as a smoke test of every screen it passes through.
/// </summary>
internal sealed class Bot
{
    private readonly Game _g;
    private World W => _g.World;
    private int _tripSteps;
    private bool _homeward;
    private string? _spell;
    private int _group;
    private string? _member;
    private string? _trapGuess;
    private int _inspections;
    private int _stuck;

    public Bot(Game game) => _g = game;

    public int Presses { get; private set; }
    public int Fights { get; private set; }
    public int Trips { get; private set; }
    public int Wipes { get; private set; }
    public int MaxDepth { get; private set; }
    public List<string> Trace { get; } = [];
    public List<string> LairFights { get; } = [];

    public bool Won => W.Victories > 0;

    /// <summary>The log around each of the first few deaths, for tuning.</summary>
    public List<string> Deaths { get; } = [];
    private readonly HashSet<string> _deadSeen = [];

    private void NoteDeaths()
    {
        foreach (var c in W.Members.Where(c => !c.IsAlive))
            if (_deadSeen.Add(c.Id + c.Deaths) && Deaths.Count < 600 && W.Depth >= 5)
            {
                Deaths.Add($"--- {c.Name} L{c.Level} died at depth {W.Depth}:");
                Deaths.AddRange(W.Log.TakeLast(16));
            }
    }

    public void Run(int maxPresses)
    {
        while (Presses < maxPresses && !Won)
        {
            if (!W.Members.Any() && _g.Top is CastleState or EdgeState or TavernState) Recruit();
            Act();
            NoteDeaths();
        }
    }

    private void Press(string key)
    {
        Presses++;
        var title = _g.Current.Title;
        if (!_g.Press(key))
        {
            _stuck++;
            if (_stuck > 50) throw new InvalidOperationException($"Bot stuck on '{title}' pressing {key}: " + string.Join(" | ", _g.Current.Options.Select(o => $"{o.Key}:{o.Label}:{o.Enabled}")));
            // Fall back to the first enabled option.
            var any = _g.Current.Options.FirstOrDefault(o => o.Enabled);
            if (any is not null) _g.Press(any.Key);
            return;
        }
        _stuck = 0;
        if (Trace.Count > 400) Trace.RemoveAt(0);
        Trace.Add($"{title}: {key}");
    }

    private void PressLabel(string label)
    {
        var opt = _g.Current.Options.FirstOrDefault(o => o.Enabled && o.Label == label);
        Press(opt?.Key ?? "ESC");
    }

    private void Act()
    {
        switch (_g.Top)
        {
            case MessageState:
                if (_g.Current.Title == "ALL IS LOST") Wipes++;
                Press("ENTER");
                break;
            case CombatState:
                Combat();
                break;
            case GroupPickState:
                Press((_group + 1).ToString());
                break;
            case SpellPickState:
                PressLabel(_spell ?? "");
                break;
            case PickState when _member is not null:
                PressLabel(_member);
                _member = null;
                break;
            case ChestState:
                Chest();
                break;
            case TrapNameState:
                PressLabel(_trapGuess ?? "TRAPLESS CHEST");
                break;
            case StairsState:
                Stairs();
                break;
            case LiftState:
                Lift();
                break;
            case BodiesState:
                Press(_g.Current.Offers("P") ? "P" : "ESC");
                break;
            case MazeState:
                Maze();
                break;
            case CampState:
                Press("ESC");
                break;
            case CastleState:
                Castle();
                break;
            case ConfirmState:
                Press("Y");
                break;
            default:
                // Anything unexpected: back out.
                Press(_g.Current.Offers("ESC") ? "ESC" : _g.Current.Options.First(o => o.Enabled).Key);
                break;
        }
    }

    // ----- the castle ----------------------------------------------------------------------

    private enum Errand { Temple, Rest, Shop, Equip, Leave }
    private Errand _errand = Errand.Temple;

    private void Castle()
    {
        _homeward = false;
        _tripSteps = 0;
        switch (_errand)
        {
            case Errand.Temple:
                _errand = Errand.Rest;
                Temple();
                break;
            case Errand.Rest:
                _errand = Errand.Shop;
                Rest();
                break;
            case Errand.Shop:
                _errand = Errand.Equip;
                Shop();
                break;
            case Errand.Equip:
                _errand = Errand.Leave;
                Equip();
                break;
            default:
                _errand = Errand.Temple;
                Trips++;
                Press("E");
                if (_g.Current.Offers("M")) Press("M");
                else Press("ESC");
                break;
        }
    }

    private void Temple()
    {
        // Share the gold so someone can pay, then tend everyone who needs it.
        if (W.Members.Count() > 1) { Press("T"); Press("D"); Press("ENTER"); Press("ESC"); }
        foreach (var patient in W.Members.Where(c => c.Status is Status.Dead or Status.Ashes or Status.Paralyzed or Status.Stoned).ToList())
        {
            long fee = TempleState.Fee(patient);
            var payer = W.Members.Where(c => c == patient || c.IsAlive).OrderByDescending(c => c.Gold).FirstOrDefault();
            if (payer is null) continue;
            if (payer.Gold < fee)
            {
                // Pool everything with the richest member first.
                Press("S"); Press("C"); PressLabel(payer.Name); Press("P"); Press("ENTER"); Press("ESC"); Press("ESC");
            }
            if (payer.Gold < fee) continue;
            Press("P");
            Press("H");
            PressLabel(patient.Name);
            if (_g.Top is PickState) PressLabel(payer.Name);
            while (_g.Top is MessageState) Press("ENTER");
            Press("ESC");
        }
        // The dead nobody can pay for stay behind at the temple; recruits take their places.
        foreach (var c in W.Members.Where(c => !c.IsAlive || c.Status is Status.Stoned or Status.Paralyzed).ToList())
        {
            W.Party.Remove(c.Id);
            Trace.Add($"left {c.Name} at the temple");
        }
        // A party that can't pay to raise its dead retires them and recruits anew.
        if (!W.Members.Any(c => c.Status.CanAct()))
        {
            foreach (var c in W.Members.ToList()) W.Party.Remove(c.Id);
            Trace.Add("party retired");
        }
        // Anyone lost is replaced.
        if (W.Members.Count() < World.MaxParty) Recruit();
    }

    private void Rest()
    {
        if (W.Members.Count() > 1) { Press("T"); Press("D"); Press("ENTER"); Press("ESC"); }
        long each = W.Members.Select(c => c.Gold).DefaultIfEmpty(0).Min();
        string room = each >= 4000 ? "R" : each >= 1500 ? "M" : each >= 300 ? "E" : "C";
        Press("I");
        Press("A");
        Press(room);
        while (_g.Top is MessageState) Press("ENTER");
        Press("ESC");
    }

    private void Shop()
    {
        var members = W.Members.ToList();
        foreach (var c in members)
        {
            if (!c.IsAlive) continue;
            foreach (var want in Wishlist(c))
            {
                Press("S");
                Press("C");
                PressLabel(c.Name);
                Press("B");
                // Page through to find the item.
                for (int page = 0; page < 12; page++)
                {
                    var opt = _g.Current.Options.FirstOrDefault(o => o.Label == want.Name);
                    if (opt is not null)
                    {
                        if (opt.Enabled) { Press(opt.Key); Press("ENTER"); }
                        break;
                    }
                    if (!_g.Current.Offers("N")) break;
                    Press("N");
                }
                while (_g.Top is not CastleState) Press(_g.Top is MessageState ? "ENTER" : "ESC");
            }
        }
    }

    /// <summary>Upgrades this member could buy now, keeping a reserve for the inn.</summary>
    private IEnumerable<ItemDef> Wishlist(Character c)
    {
        long budget = c.Gold - 300;
        var list = new List<ItemDef>();
        foreach (var kind in new[] { ItemKind.Weapon, ItemKind.Armor, ItemKind.Shield, ItemKind.Helm, ItemKind.Gauntlets })
        {
            if (c.Class == CharClass.Ninja && kind is ItemKind.Weapon or ItemKind.Armor or ItemKind.Shield) continue;
            var current = c.Equipped.FirstOrDefault(i => i.Def.Kind == kind)?.Def;
            double Score(ItemDef d) => kind == ItemKind.Weapon ? d.Damage.Average + d.ToHit : d.Ac;
            var best = Items.All
                .Where(d => d.Kind == kind && d.UsableBy(c.Class) && !d.Cursed && (d.Align is null || d.Align == c.Alignment)
                            && (d.Stock == -1 || W.Stock.GetValueOrDefault(d.Id) > 0) && d.Price <= budget)
                .OrderByDescending(Score).FirstOrDefault();
            if (best is null || (current is not null && Score(best) <= Score(current))) continue;
            budget -= best.Price;
            list.Add(best);
        }
        return list;
    }

    private void Equip()
    {
        foreach (var c in W.Members.ToList())
        {
            // Equip the best identified, usable thing of each kind; sell what's unknown or spare.
            Press("T"); Press("I"); PressLabel(c.Name);
            if (_g.Top is not SheetState) { while (_g.Top is not CastleState) Press("ESC"); continue; }
            if (_g.Current.Offers("E"))
            {
                Press("E");
                foreach (var kind in new[] { ItemKind.Weapon, ItemKind.Armor, ItemKind.Shield, ItemKind.Helm, ItemKind.Gauntlets, ItemKind.Misc })
                {
                    double Score(ItemDef d) => kind == ItemKind.Weapon ? d.Damage.Average + d.ToHit * 1.5 + d.ExtraSwings * 4 : d.Ac + d.Regenerate * 2;
                    var best = c.Items.Where(i => i.Def.Kind == kind && i.Identified && !i.Def.Cursed && c.CanEquip(i.Def, out _))
                        .OrderByDescending(i => Score(i.Def)).FirstOrDefault();
                    if (c.Class == CharClass.Ninja && kind is ItemKind.Armor or ItemKind.Shield) continue;
                    if (best is null || best.Equipped) continue;
                    if (c.Equipped.Any(i => i.Stuck && i.Def.Kind == kind)) continue;
                    var cur = c.Equipped.FirstOrDefault(i => i.Def.Kind == kind);
                    if (cur is not null && Score(cur.Def) >= Score(best.Def)) continue;
                    Press(((c.Items.IndexOf(best)) + 1).ToString());
                    while (_g.Top is MessageState) Press("ENTER");
                }
                Press("ESC");
            }
            while (_g.Top is not CastleState) Press(_g.Top is MessageState ? "ENTER" : "ESC");
            // Sell unknown loot (the shop pays half whatever it is) and spare gear; uncurse.
            if (c.Items.Any(i => !i.Equipped && i.Def.Kind != ItemKind.Special && (i.Def.Kind != ItemKind.Consumable || !i.Identified)) || c.Items.Any(i => i.Stuck))
            {
                Press("S"); Press("C"); PressLabel(c.Name);
                while (c.Items.FirstOrDefault(i => i.Stuck) is { } stuck && c.Gold >= ShopState.UncursePrice(stuck.Def))
                {
                    Press("U"); Press((c.Items.IndexOf(stuck) + 1).ToString()); Press("ENTER");
                }
                while (c.Items.FirstOrDefault(i => !i.Equipped && i.Def.Kind != ItemKind.Special && (i.Def.Kind != ItemKind.Consumable || !i.Identified)) is { } spare)
                {
                    Press("S"); Press((c.Items.IndexOf(spare) + 1).ToString()); Press("ENTER");
                }
                while (_g.Top is not CastleState) Press(_g.Top is MessageState ? "ENTER" : "ESC");
            }
        }
    }

    private void Recruit()
    {
        // The bot doesn't fill in the creation screens; it uses the ready-made recruits.
        var before = W.Party.Count;
        var lost = W.Party.Where(id => W.Find(id) is not { Status: not Status.Lost }).ToList();
        foreach (var id in lost) W.Party.Remove(id);
        if (W.Party.Count >= World.MaxParty) return;
        // Abandoned characters (dead, or spare recruits) are struck from the roster to keep it small.
        W.Roster.RemoveAll(c => !W.Party.Contains(c.Id));
        var fresh = Recruits.AddQuickParty(W, _g.Rng);
        // Keep the party balanced: fill the gaps with the classes it lacks.
        foreach (var c in fresh) W.Party.Remove(c.Id);
        foreach (var c in fresh.OrderBy(c => W.Members.Count(m => m.Class == c.Class)))
            if (W.Party.Count < World.MaxParty && W.Members.Count(m => m.Class == c.Class) < (c.Class == CharClass.Fighter ? 2 : 1))
                W.Party.Add(c.Id);
        foreach (var c in fresh)
            if (W.Party.Count < World.MaxParty && !W.Party.Contains(c.Id)) W.Party.Add(c.Id);
        // Fighters to the front.
        var order = W.Members.OrderBy(c => c.Class switch { CharClass.Fighter => 0, CharClass.Priest => 1, CharClass.Thief => 2, _ => 3 }).Select(c => c.Id).ToList();
        W.Party.Clear();
        W.Party.AddRange(order);
        Trace.Add($"recruited {W.Party.Count - before}");
    }

    // ----- the maze ------------------------------------------------------------------------

    private bool Ribbon => W.PartyHas(Items.RibbonId);

    /// <summary>The party's typical level: the median of its living members.</summary>
    private int PartyLevel
    {
        get
        {
            var levels = W.Members.Where(c => c.IsAlive).Select(c => c.Level).OrderBy(l => l).ToList();
            return levels.Count == 0 ? 1 : levels[levels.Count / 2];
        }
    }

    private int TargetDepth
    {
        get
        {
            int lvl = PartyLevel;
            int depth = Math.Clamp(lvl * 10 / 16, 1, 9);
            if (!W.PartyHas(Items.RibbonId)) depth = Math.Min(depth, 4);
            return depth;
        }
    }

    private bool WantsHome()
    {
        var m = W.Members.ToList();
        if (W.PartyHas(Items.AmuletId)) return true;
        if (m.Any(c => !c.IsAlive || c.Status is Status.Stoned or Status.Paralyzed)) return true;
        int hp = m.Sum(c => c.Hp), max = m.Sum(c => c.MaxHp);
        if (hp * 2 < max) return true;
        if (m.Any(c => c.Hp * 10 < c.MaxHp * 4)) return true;
        bool healerDry = !m.Any(c => c.IsAlive && c.Spells.Any(s => Spells.Get(s).Effect == SpellEffect.Heal && Magic.CanCast(c, Spells.Get(s), false, out _)));
        if (healerDry && hp * 10 < max * 7) return true;
        return _tripSteps > 1500;
    }

    private void Maze()
    {
        MaxDepth = Math.Max(MaxDepth, W.Depth);
        _tripSteps++;
        // Heal up in camp when someone is low and a healer has casts; then decide whether to go home.
        if (TryCampHeal()) return;
        if (!_homeward && WantsHome()) _homeward = true;

        var goal = Goal();
        if (goal is null)
        {
            Press("S"); // nothing to walk to: go looking for a fight
            return;
        }
        var level = _g.CurrentLevel!;
        var (gd, gx, gy) = goal.Value;
        (int X, int Y) target = gd == W.Depth ? (gx, gy)
            : BestLift(level, gd) is { } lift ? (lift.X, lift.Y)
            : gd < W.Depth ? level.StairsUp : level.StairsDown!.Value;
        Dir? dir;
        if ((W.X, W.Y) == target)
        {
            // Standing on the stairs we want (we said no earlier): step off, then come back.
            dir = Enum.GetValues<Dir>().Cast<Dir?>().FirstOrDefault(d => level.CanPass(W.X, W.Y, d!.Value, Ribbon));
        }
        else dir = FirstStep(level, (W.X, W.Y), target, Ribbon, careful: true) ?? FirstStep(level, (W.X, W.Y), target, Ribbon, careful: false);
        if (dir is null) { Press("S"); return; }
        if (dir == W.Facing) Press("UP");
        else if (dir == W.Facing.Right()) Press("RIGHT");
        else Press("LEFT");
    }

    private (int Depth, int X, int Y)? Goal()
    {
        if (_homeward) return (0, 0, 0);
        int lvl = PartyLevel;
        if (!Ribbon && lvl >= 8)
        {
            var lair = MazeBuilder.Get(4).LairCenter!.Value;
            return (4, lair.X, lair.Y);
        }
        if (Ribbon && !W.PartyHas(Items.AmuletId) && lvl >= 14)
        {
            var lair = MazeBuilder.Get(10).LairCenter!.Value;
            return (10, lair.X, lair.Y);
        }
        int depth = TargetDepth;
        if (W.Depth != depth) return (depth, 0, 0);
        // Explore the nearest unexplored square; when all is seen, wait for fights.
        var level = _g.CurrentLevel!;
        var near = Nearest(level, (W.X, W.Y), p => !W.IsExplored(W.Depth, p.X, p.Y)
            && level.FeatureAt(p.X, p.Y) is not (Feature.Chute or Feature.Teleporter or Feature.Pit or Feature.Lair), Ribbon);
        return near is { } n ? (W.Depth, n.X, n.Y) : null;
    }

    private bool _camped;

    private bool TryCampHeal()
    {
        if (_camped) { _camped = false; return false; }
        var m = W.Members.ToList();
        var wounded = m.Where(c => c.IsAlive && c.Status != Status.Stoned && c.Hp * 10 < c.MaxHp * 7).OrderBy(c => c.Hp).FirstOrDefault();
        if (wounded is null) return false;
        foreach (var healer in m.Where(c => c.Status.CanAct()))
        {
            var heal = new[] { "SANARA", "SANAR", "SANA" }.Select(Spells.Get).FirstOrDefault(s => Magic.CanCast(healer, s, false, out _));
            if (heal is null) continue;
            Press("C");
            Press((m.IndexOf(healer) + 1).ToString());
            Press("S");
            _spell = heal.Name;
            PressLabel(heal.Name);
            if (_g.Top is PickState) PressLabel(wounded.Name);
            while (_g.Top is MessageState) Press("ENTER");
            while (_g.Top is not MazeState) Press("ESC");
            _camped = true;
            return true;
        }
        return false;
    }

    /// <summary>A lift on this level that gets the party closer to <paramref name="depth"/>, if any.</summary>
    private LiftStop? BestLift(Level level, int depth)
    {
        int target = Math.Max(1, depth);
        return level.Lifts
            .Where(l => !l.NeedsRibbon || Ribbon)
            .Where(l => Math.Abs(Math.Clamp(target, l.Low, l.High) - target) < Math.Abs(W.Depth - target) - 1)
            .OrderBy(l => Math.Abs(Math.Clamp(target, l.Low, l.High) - target))
            .FirstOrDefault();
    }

    private void Lift()
    {
        var goal = Goal();
        int want = goal is { } g ? Math.Max(1, g.Depth) : W.Depth;
        var opts = _g.Current.Options.Where(o => o.Enabled && int.TryParse(o.Key, out _)).Select(o => int.Parse(o.Key)).ToList();
        if (opts.Count == 0 || want == W.Depth) { Press("ESC"); return; }
        int best = opts.OrderBy(d => Math.Abs(d - want)).First();
        Press(Math.Abs(best - want) < Math.Abs(W.Depth - want) ? best.ToString() : "ESC");
    }

    private void Stairs()
    {
        var title = _g.Current.Title;
        bool up = title == "STAIRS UP";
        var goal = Goal();
        bool want = goal is { } g && (up ? g.Depth < W.Depth : g.Depth > W.Depth);
        Press(want ? "Y" : "N");
    }

    private static Dir? FirstStep(Level level, (int X, int Y) from, (int X, int Y) to, bool ribbon, bool careful)
    {
        var prev = new Dictionary<(int, int), ((int, int) From, Dir Dir)>();
        var q = new Queue<(int X, int Y)>();
        q.Enqueue(from);
        prev[from] = (from, Dir.North);
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            if (p == to) break;
            foreach (var d in Enum.GetValues<Dir>())
            {
                if (!level.CanPass(p.X, p.Y, d, ribbon)) continue;
                var n = (p.X + d.Dx(), p.Y + d.Dy());
                // Don't wander through traps on the way.
                var f = level.FeatureAt(n.Item1, n.Item2);
                if (n != to && (careful ? f is Feature.Chute or Feature.Teleporter or Feature.Pit or Feature.Lair : f is Feature.Chute or Feature.Lair)) continue;
                if (prev.ContainsKey(n)) continue;
                prev[n] = (p, d);
                q.Enqueue(n);
            }
        }
        if (!prev.ContainsKey(to)) return null;
        var cur = to;
        Dir dir = Dir.North;
        while (cur != from)
        {
            var (f, d) = prev[cur];
            dir = d;
            cur = f;
        }
        return dir;
    }

    private static (int X, int Y)? Nearest(Level level, (int X, int Y) from, Func<(int X, int Y), bool> wanted, bool ribbon)
    {
        var seen = new HashSet<(int, int)> { from };
        var q = new Queue<(int X, int Y)>();
        q.Enqueue(from);
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            if (p != from && wanted(p)) return p;
            foreach (var d in Enum.GetValues<Dir>())
            {
                if (!level.CanPass(p.X, p.Y, d, ribbon)) continue;
                var n = (p.X + d.Dx(), p.Y + d.Dy());
                if (level.FeatureAt(n.Item1, n.Item2) is Feature.Chute or Feature.Teleporter or Feature.Pit or Feature.Lair) continue;
                if (seen.Add(n)) q.Enqueue(n);
            }
        }
        return null;
    }

    // ----- fights ------------------------------------------------------------------------------

    private void Combat()
    {
        var screen = _g.Current;
        if (screen.Offers("ENTER"))
        {
            Fights++;
            if (_g.Battle?.IsLair == true) LairFights.Add($"depth {W.Depth} outcome {_g.Battle.Outcome} round {_g.Battle.Round} levels {string.Join(",", W.Members.Select(c => c.Level))}");
            Press("ENTER");
            return;
        }
        var battle = _g.Battle!;
        string? name = screen.Lines.Select(l => l.Text).LastOrDefault(t => t.EndsWith("'s option?"))?.Replace("'s option?", "");
        var who = W.Members.First(c => c.Name == name);
        var party = W.Members.ToList();
        int biggest = Enumerable.Range(0, battle.Groups.Count).OrderByDescending(i => battle.Groups[i].AliveCount * battle.Groups[i].Def.Level).First();
        int monsters = battle.Groups.Sum(g => g.AliveCount);

        // Heal the badly wounded.
        var hurt = party.Where(c => c.IsAlive && c.Status != Status.Stoned && c.Hp * 3 < c.MaxHp).OrderBy(c => c.Hp).FirstOrDefault();
        if (hurt is not null)
        {
            var heal = new[] { "SANCTA", "SANARA", "SANAR", "SANA" }.Select(Spells.Get).FirstOrDefault(s => Magic.CanCast(who, s, true, out _));
            if (heal is not null) { Cast(heal.Name, 0, hurt.Name); return; }
        }
        // Casters first: put them to sleep.
        int casters = Enumerable.Range(0, battle.Groups.Count).FirstOrDefault(i => battle.Groups[i].Def.Mage + battle.Groups[i].Def.Priest > 0, -1);
        if (casters >= 0 && Magic.CanCast(who, Spells.Get("SOMNA"), true, out _) && !battle.Groups[casters].Def.ResistsSleep
            && battle.Groups[casters].Alive.Any(f => !f.Asleep)) { Cast("SOMNA", casters, null); return; }
        // Big spells on big fights.
        if (monsters >= 3 || battle.IsLair || battle.Groups.Any(g => g.Def.Level >= PartyLevel))
        {
            var nuke = new[] { "NOVARDO", "CAELUM", "GELUNAR", "LAMINA", "ARDORAN", "GELUN", "IGNIS", "OMARDO", "FULGA" }
                .Select(Spells.Get).FirstOrDefault(s => Magic.CanCast(who, s, true, out _));
            if (nuke is not null) { Cast(nuke.Name, biggest, null); return; }
            if (battle.Round == 0 && Spells.Get("SOMNA") is var somna && Magic.CanCast(who, somna, true, out _)
                && !battle.Groups[biggest].Def.ResistsSleep) { Cast("SOMNA", biggest, null); return; }
        }
        if (battle.IsLair && Spells.Get("MUNIRAN") is var muniran && Magic.CanCast(who, muniran, true, out _) && battle.Round == 0) { Cast("MUNIRAN", 0, null); return; }
        if (screen.Offers("F"))
        {
            Press("F");
            if (_g.Top is GroupPickState) Press(_g.Current.Options.First(o => o.Enabled).Key);
            return;
        }
        var bolt = new[] { "NOSAR", "ARDO", "NOSA" }.Select(Spells.Get).FirstOrDefault(s => Magic.CanCast(who, s, true, out _));
        if (bolt is not null && who.Level >= 3) { Cast(bolt.Name, 0, null); return; }
        Press("P");
    }

    private void Cast(string spell, int group, string? member)
    {
        _spell = spell;
        _group = group;
        _member = member;
        Press("S");
    }

    private void Chest()
    {
        var chest = _g.Chest!;
        var thief = W.Members.FirstOrDefault(c => c.Status.CanAct() && c.Class is CharClass.Thief or CharClass.Ninja);
        var finding = chest.Findings.LastOrDefault(f => f.Contains(" thinks it is: ") || f.Contains(" SPEKIA: "));
        if (_disarms >= 2) finding = null;
        if (_g.Current.Offers("C") && _inspections == 0)
        {
            _inspections++;
            Press("C");
            return;
        }
        if (thief is not null && _inspections < 2 && (finding is null || !finding.StartsWith(thief.Name)))
        {
            _inspections++;
            Press("I");
            PressLabel(thief.Name);
            return;
        }
        _inspections = 0;
        if (finding is not null)
        {
            _trapGuess = finding[(finding.IndexOf(": ") + 2)..].TrimEnd('.');
            if (_trapGuess == "TRAPLESS CHEST") { _disarms = 0; Press("O"); PressLabel((thief ?? W.Members.First(c => c.Status.CanAct())).Name); return; }
            _disarms++;
            Press("D");
            PressLabel((thief ?? W.Members.First(c => c.Status.CanAct())).Name);
            return;
        }
        _disarms = 0;
        Press("O");
        PressLabel((thief ?? W.Members.First(c => c.Status.CanAct())).Name);
    }

    private int _disarms;
}
