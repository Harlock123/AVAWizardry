using Wizardry.Core.Combat;
using Wizardry.Core.Content;
using Wizardry.Core.Maze;
using Wizardry.Core.Model;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Screens;

/// <summary>
/// A game in progress: the world, its randomness, and a stack of screens. Hosts call
/// <see cref="Press"/> and <see cref="Submit"/> and redraw <see cref="Current"/>.
/// </summary>
public sealed partial class Game
{
    private readonly Stack<State> _states = new();

    public Game(World world)
    {
        World = world;
        Rng = new Rng(world.RngState);
        if (world.InMaze && world.Members.Any())
            _states.Push(new MazeState(this));
        else
        {
            world.Depth = 0;
            _states.Push(new CastleState(this));
        }
    }

    public World World { get; }
    public Rng Rng { get; }

    /// <summary>The fight in progress (for the picture pane), if any.</summary>
    public Battle? Battle { get; internal set; }
    /// <summary>The chest on screen, if any.</summary>
    public Chest? Chest { get; internal set; }
    /// <summary>When set, the maze pane shows the party's coordinates (LOKAR).</summary>
    public bool ShowLocation { get; internal set; }

    public event Action<SoundCue>? Sound;
    /// <summary>The player chose to leave the game (it has been saved by then).</summary>
    public event Action? QuitRequested;
    /// <summary>The host should save now (entering the castle, camping, quitting).</summary>
    public event Action? SaveRequested;

    public Screen Current => _states.Peek().Render();

    internal State Top => _states.Peek();

    public IReadOnlyList<string> RecentLog(int count) => World.Log.Skip(Math.Max(0, World.Log.Count - count)).ToList();

    /// <summary>Presses a key. Unknown or disabled keys are ignored. Returns whether anything happened.</summary>
    public bool Press(string key)
    {
        key = key.ToUpperInvariant();
        var screen = Current;
        if (!screen.Offers(key)) return false;
        Top.Key(key);
        World.RngState = Rng.State;
        return true;
    }

    /// <summary>Submits typed text to a screen with a prompt.</summary>
    public bool Submit(string text)
    {
        if (Current.Prompt is null) return false;
        Top.Text(text.Trim());
        World.RngState = Rng.State;
        return true;
    }

    internal void Push(State s) => _states.Push(s);

    internal void Pop()
    {
        if (_states.Count > 1) _states.Pop();
    }

    internal void Replace(State s)
    {
        if (_states.Count > 0) _states.Pop();
        _states.Push(s);
    }

    /// <summary>Clears every screen and shows <paramref name="s"/>.</summary>
    internal void Reset(State s)
    {
        _states.Clear();
        _states.Push(s);
    }

    internal void Play(SoundCue cue) => Sound?.Invoke(cue);

    internal void RequestSave()
    {
        World.RngState = Rng.State;
        SaveRequested?.Invoke();
    }

    internal void RequestQuit()
    {
        RequestSave();
        QuitRequested?.Invoke();
    }

    internal void Say(string text) => World.Say(text);

    /// <summary>Shows lines and waits for a key, then runs <paramref name="then"/> (or just returns).</summary>
    internal void Tell(string title, IEnumerable<Line> lines, Action? then = null, Pane? pane = null) =>
        Push(new MessageState(this, title, [.. lines], then, pane ?? Top.Render().Pane));

    // ----- party ---------------------------------------------------------------------------

    public List<Character> Party => World.Members.ToList();

    /// <summary>A party can't mix good and evil members.</summary>
    public bool CanJoin(Character c, out string? why)
    {
        why = null;
        if (World.Party.Count >= World.MaxParty) { why = "The party is full."; return false; }
        if (World.Party.Contains(c.Id)) { why = $"{c.Name} is already in the party."; return false; }
        if (c.LeftInMaze is not null) { why = $"{c.Name} is out in the maze."; return false; }
        if (c.Status == Status.Lost) { why = $"{c.Name} is lost."; return false; }
        if (c.Alignment != Alignment.Neutral && Party.Any(m => m.Alignment != Alignment.Neutral && m.Alignment != c.Alignment))
        {
            why = "Good and evil characters won't travel together.";
            return false;
        }
        return true;
    }

    /// <summary>Dead, ashen and stoned members drift to the back after a fight.</summary>
    internal void ReorderFallen()
    {
        var ordered = Party.OrderBy(c => c.IsAlive && c.Status != Status.Stoned ? 0 : 1).Select(c => c.Id).ToList();
        World.Party.Clear();
        World.Party.AddRange(ordered);
    }

    // ----- moving about the maze --------------------------------------------------------------

    public Level? CurrentLevel => World.InMaze ? MazeBuilder.Get(World.Depth) : null;

    public bool Lit => World.LongLight || World.LightSteps > 0;

    internal void EnterMaze()
    {
        World.Depth = 1;
        var up = MazeBuilder.Get(1).StairsUp;
        (World.X, World.Y) = up;
        World.Facing = Dir.North;
        World.ClearedRooms.Clear();
        foreach (var c in Party) c.LeftInMaze = null;
        World.MarkExplored(1, World.X, World.Y);
        Say("The party enters the Proving Grounds.");
        Play(SoundCue.Stairs);
    }

    /// <summary>Back to the castle: the light, the trip's blessing and the rooms' chests reset.</summary>
    internal void ReturnToCastle(string why)
    {
        World.Depth = 0;
        World.LightSteps = 0;
        World.LongLight = false;
        World.TripWard = 0;
        World.ClearedRooms.Clear();
        ShowLocation = false;
        foreach (var c in Party)
        {
            c.FightWard = 0;
            c.LeftInMaze = null;
            c.Poisoned = false;
            if (c.Status is Status.Asleep or Status.Afraid) c.Status = Status.Ok;
        }
        // The lost are gone; the rest of the party carries on.
        World.Party.RemoveAll(id => World.Find(id) is not { Status: not Status.Lost });
        Say(why);
        Battle = null;
        Chest = null;
        // The amulet only counts when someone walks it in.
        bool won = World.PartyHas(Items.AmuletId) && Party.Any(c => c.IsAlive);
        Reset(new CastleState(this));
        if (won) Victory();
        RequestSave();
    }

    /// <summary>The amulet comes home: honours for the party, and the game goes on.</summary>
    private void Victory()
    {
        World.Victories++;
        var lines = new List<Line>
        {
            new("THE AMULET OF THAL IS RETURNED!", Tone.Title),
            "",
            "The Overlord Kessar takes the amulet from your hands and holds it to the light.",
            "\"So Morthrain has fallen at last. The Proving Grounds have their champions.\"",
            "",
        };
        foreach (var c in Party.Where(c => c.IsAlive))
        {
            c.Honors++;
            c.Experience += 50000;
            lines.Add(new($"{c.Name} is named a Champion of the Proving Grounds (>).", Tone.Gold));
        }
        foreach (var c in Party)
            c.Items.RemoveAll(i => i.ItemId == Items.AmuletId);
        lines.Add("");
        lines.Add("Each champion is rewarded with 50,000 experience points.");
        lines.Add("Morthrain will rise again in time, and the maze waits for any who would try.");
        Play(SoundCue.Victory);
        Push(new MessageState(this, "VICTORY", lines, null, Pane.Victory));
    }

    /// <summary>Takes one step forward. Returns false (and bumps) if the way is blocked.</summary>
    internal bool StepForward()
    {
        var level = CurrentLevel!;
        var edge = level.EdgeAt(World.X, World.Y, World.Facing);
        bool ribbon = World.PartyHas(Items.RibbonId);
        if (edge == Edge.Sealed && !ribbon)
        {
            Play(SoundCue.Bump);
            Tell("A SEALED DOOR", ["A door of black iron, without handle or keyhole.", "In its centre is a hollow shaped like a ribbon."]);
            return false;
        }
        if (!level.CanPass(World.X, World.Y, World.Facing, ribbon))
        {
            Play(SoundCue.Bump);
            Say("OUCH!");
            return false;
        }
        if (edge == Edge.Sealed) Say("The ribbon glows, and the sealed door swings open.");
        if (edge is Edge.Door or Edge.Sealed) Play(SoundCue.Door);
        World.X += World.Facing.Dx();
        World.Y += World.Facing.Dy();
        World.MarkExplored(World.Depth, World.X, World.Y);
        Play(SoundCue.Step);
        AfterStep();
        return true;
    }

    internal void Turn(bool right)
    {
        World.Facing = right ? World.Facing.Right() : World.Facing.Left();
        Play(SoundCue.Turn);
    }

    internal void TurnAround()
    {
        World.Facing = World.Facing.Back();
        Play(SoundCue.Turn);
    }

    /// <summary>Poison, regeneration and light tick with every step; then the square's feature.</summary>
    private void AfterStep()
    {
        if (World.LightSteps > 0) World.LightSteps--;
        foreach (var c in Party.Where(c => c.IsAlive && c.Status != Status.Stoned))
        {
            // Poison bites with every step, and now and then it runs its course.
            if (c.Poisoned && Rng.Percent(8))
            {
                c.Poisoned = false;
                Say($"{c.Name} is no longer poisoned.");
            }
            int change = c.Regeneration - (c.Poisoned && Rng.OneIn(2) ? 1 : 0);
            if (change == 0) continue;
            c.Hp = Math.Min(c.MaxHp, c.Hp + change);
            if (c.Hp <= 0)
            {
                c.Hp = 0;
                c.Status = Status.Dead;
                c.Poisoned = false;
                c.Deaths++;
                Say($"{c.Name} has died!");
                Play(SoundCue.Death);
            }
        }
        if (!Party.Any(c => c.IsAlive && c.Status is not (Status.Stoned or Status.Paralyzed)))
        {
            PartyWiped();
            return;
        }
        Arrive();
    }

    /// <summary>What happens on arriving at a square (by walking, falling or teleporting).</summary>
    internal void Arrive()
    {
        var level = CurrentLevel!;
        int x = World.X, y = World.Y;
        // Bodies of an earlier party?
        var bodies = World.Roster.Where(c => c.LeftInMaze == new MazeSpot(World.Depth, x, y)).ToList();
        if (bodies.Count > 0)
            Push(new BodiesState(this, bodies));

        switch (level.FeatureAt(x, y))
        {
            case Feature.Spinner:
                World.Facing = (Dir)Rng.Next(4);
                Play(SoundCue.Spinner);
                break;
            case Feature.Teleporter:
                Play(SoundCue.Teleport);
                Say("You feel a strange sensation...");
                Teleport(World.Depth, random: true);
                return;
            case Feature.Pit:
                Pit();
                return;
            case Feature.Chute:
                Play(SoundCue.Fall);
                Tell("A CHUTE!", ["The floor tilts and the party slides down into the dark!"], () =>
                {
                    World.Depth++;
                    World.MarkExplored(World.Depth, World.X, World.Y);
                    Arrive();
                });
                return;
            case Feature.Message:
                Tell("ON THE WALL", [level.MessageAt(x, y) ?? ""]);
                break;
            case Feature.Lift:
                Push(new LiftState(this, level.Lifts.First(l => l.X == x && l.Y == y)));
                return;
            case Feature.StairsUp:
                Push(new StairsState(this, up: true));
                return;
            case Feature.StairsDown:
                Push(new StairsState(this, up: false));
                return;
            case Feature.Lair:
                // The Warden stands guard again whenever nobody holds its ribbon.
                if (World.Depth == 4 && !World.Roster.Any(c => c.Status != Status.Lost && c.Items.Any(i => i.ItemId == Items.RibbonId)))
                {
                    StartLairFight();
                    return;
                }
                if (World.Depth == MazeBuilder.Depth && !World.PartyHas(Items.AmuletId))
                {
                    Tell("MORTHRAIN'S SANCTUM", [
                        new("A voice like grinding stone: \"So. The Overlord sends more meat.\"", Tone.Bad),
                        "Morthrain the Archmage rises from his throne, the Amulet of Thal at his throat.",
                    ], StartLairFight);
                    return;
                }
                break;
            case Feature.Room:
                var key = $"{World.Depth}:{level.RoomAt(x, y)}";
                if (!World.ClearedRooms.Contains(key))
                {
                    StartFight(roomKey: key);
                    return;
                }
                break;
        }

        // Wandering monsters.
        int chance = level.FeatureAt(x, y) == Feature.Dark ? 7 : 4;
        if (Rng.Percent(chance)) StartFight(roomKey: null);
    }

    private void Pit()
    {
        Play(SoundCue.Fall);
        var lines = new List<Line> { new("A PIT!", Tone.Bad) };
        foreach (var c in Party.Where(c => c.IsAlive && c.Status != Status.Stoned))
        {
            if (Rng.Percent(c.Agi * 2 + c.Luc)) { lines.Add($"{c.Name} leaps clear."); continue; }
            int dmg = Rng.Roll(new Dice(World.Depth, 6));
            c.Hp -= dmg;
            lines.Add($"{c.Name} falls in and takes {dmg} damage.");
            if (c.Hp <= 0)
            {
                c.Hp = 0;
                c.Status = Status.Dead;
                c.Poisoned = false;
                c.Deaths++;
                lines.Add(new($"{c.Name} is killed!", Tone.Bad));
            }
        }
        Tell("A PIT!", lines, () =>
        {
            if (!Party.Any(c => c.IsAlive && c.Status is not (Status.Stoned or Status.Paralyzed))) PartyWiped();
        });
    }

    /// <summary>Moves the party: to a random open square of a level, or to a chosen spot.</summary>
    internal void Teleport(int depth, bool random, int x = 0, int y = 0)
    {
        if (random)
        {
            var level = MazeBuilder.Get(depth);
            do
            {
                x = Rng.Next(Level.Size);
                y = Rng.Next(Level.Size);
            }
            while (level.FeatureAt(x, y) is not (Feature.None or Feature.Dark));
        }
        World.Depth = depth;
        World.X = x;
        World.Y = y;
        World.MarkExplored(depth, x, y);
    }

    internal void RideLift(int depth)
    {
        Play(SoundCue.Stairs);
        World.Depth = depth;
        World.MarkExplored(depth, World.X, World.Y);
        Say($"The lift carries the party to level {depth}.");
    }

    internal void UseStairs(bool up)
    {
        Play(SoundCue.Stairs);
        if (up && World.Depth == 1)
        {
            ReturnToCastle("The party climbs back to the castle.");
            return;
        }
        if (up)
        {
            World.Depth--;
            var down = MazeBuilder.Get(World.Depth).StairsDown!.Value;
            (World.X, World.Y) = down;
        }
        else
        {
            World.Depth++;
            (World.X, World.Y) = MazeBuilder.Get(World.Depth).StairsUp;
        }
        World.MarkExplored(World.Depth, World.X, World.Y);
        Say(up ? $"The party climbs to level {World.Depth}." : $"The party descends to level {World.Depth}.");
    }

    // ----- fights ------------------------------------------------------------------------------

    private void StartLairFight()
    {
        var groups = new List<FoeGroup>();
        if (World.Depth == 4)
            groups.Add(FoeGroup.Spawn(Monsters.Get(Monsters.Warden), 1, Rng, identified: true));
        else
        {
            groups.Add(FoeGroup.Spawn(Monsters.Get(Monsters.Morthrain), 1, Rng, identified: true));
            groups.Add(FoeGroup.Spawn(Monsters.Get(47), 1, Rng, identified: true));
            groups.Add(FoeGroup.Spawn(Monsters.Get(41), Rng.Range(2, 3), Rng, identified: false));
        }
        BeginBattle(groups, lair: true, chest: true, roomKey: null);
    }

    internal void StartFight(string? roomKey)
    {
        var groups = RollEncounter(World.Depth);
        BeginBattle(groups, lair: false, chest: roomKey is not null || Rng.Percent(20), roomKey);
    }

    /// <summary>One to four groups for a wandering (or room) encounter at this depth.</summary>
    internal List<FoeGroup> RollEncounter(int depth)
    {
        var table = Monsters.ForLevel(depth);
        int partyLevel = Party.Where(c => c.IsAlive).Select(c => c.Level).DefaultIfEmpty(1).Max();
        bool Identify() => Rng.Percent(Math.Min(90, 25 + partyLevel * 5));
        var first = Monsters.Get(table[Rng.Next(table.Count)]);
        var groups = new List<FoeGroup> { FoeGroup.Spawn(first, Rng.Roll(first.Group), Rng, Identify()) };
        if (first.Companion != 0 && Rng.Percent(50))
        {
            var mate = Monsters.Get(first.Companion);
            groups.Add(FoeGroup.Spawn(mate, Rng.Roll(mate.Group), Rng, Identify()));
        }
        while (groups.Count < Battle.MaxGroups && depth > 1 && Rng.Percent(depth * 3))
        {
            var more = Monsters.Get(table[Rng.Next(table.Count)]);
            if (groups.Any(g => g.Def.Id == more.Id)) break;
            groups.Add(FoeGroup.Spawn(more, Rng.Roll(more.Group), Rng, Identify()));
        }
        return groups;
    }

    internal void BeginBattle(List<FoeGroup> groups, bool lair, bool chest, string? roomKey)
    {
        var level = CurrentLevel!;
        bool antiMagic = level.FeatureAt(World.X, World.Y) == Feature.AntiMagic;
        Battle = new Battle(World, Rng, groups, lair, chest, antiMagic);
        Play(SoundCue.Encounter);
        var opening = Battle.Begin();
        Push(new CombatState(this, Battle, roomKey, opening));
    }

    /// <summary>Every member down: back to the castle by patrol, or left where they fell.</summary>
    internal void PartyWiped()
    {
        Play(SoundCue.Death);
        var party = Party;
        var lines = new List<Line> { new("YOUR PARTY HAS PERISHED.", Tone.Bad), "" };
        if (World.Options.RescuePatrols)
        {
            lines.Add("Days later, a castle patrol finds what is left of you,");
            lines.Add("and carries it to the Temple.");
            foreach (var c in party)
                c.Gold /= 2;
            lines.Add("(The patrol keeps half of your gold for its trouble.)");
            ReturnToCastle("A patrol brings the fallen party home.");
            Push(new MessageState(this, "ALL IS LOST", lines, null, Pane.Castle));
        }
        else
        {
            var spot = new MazeSpot(World.Depth, World.X, World.Y);
            foreach (var c in party) c.LeftInMaze = spot;
            lines.Add($"Their bodies lie on level {World.Depth}, waiting for another party to bring them home.");
            World.Party.Clear();
            Battle = null;
            Chest = null;
            World.Depth = 0;
            World.LightSteps = 0;
            World.LongLight = false;
            World.TripWard = 0;
            Reset(new CastleState(this));
            Push(new MessageState(this, "ALL IS LOST", lines, null, Pane.Castle));
            RequestSave();
        }
    }

    // ----- money -------------------------------------------------------------------------------

    /// <summary>Gathers every member's gold into one character's purse.</summary>
    internal void PoolGold(Character into)
    {
        foreach (var c in Party.Where(c => c != into))
        {
            into.Gold += c.Gold;
            c.Gold = 0;
        }
    }

    /// <summary>Shares the party's gold evenly.</summary>
    internal void DivvyGold()
    {
        var party = Party;
        if (party.Count == 0) return;
        long total = party.Sum(c => c.Gold);
        long each = total / party.Count;
        foreach (var c in party) c.Gold = each;
        party[0].Gold += total - each * party.Count;
    }
}
