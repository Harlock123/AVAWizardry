using Wizardry.Core.Maze;
using Wizardry.Core.Model;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Screens;

/// <summary>Walking the maze: forward, turn, and camp.</summary>
internal sealed class MazeState(Game game) : State(game)
{
    public override Screen Render()
    {
        var lines = new List<Line>();
        foreach (var l in G.RecentLog(3)) lines.Add(new(l, Tone.Dim));
        if (G.Lit) lines.Add(new(W.LongLight ? "A steady light surrounds the party." : "A soft light surrounds the party.", Tone.Info));
        if (G.ShowLocation) lines.Add(new($"LEVEL {W.Depth}   {W.X} EAST, {W.Y} NORTH   FACING {W.Facing.ToString().ToUpperInvariant()}", Tone.Info));
        return new Screen
        {
            Title = $"MAZE LEVEL {W.Depth}",
            Pane = Pane.Maze,
            Place = "maze",
            Lines = lines,
            Options =
            [
                new("UP", "Forward"),
                new("LEFT", "Turn left"),
                new("RIGHT", "Turn right"),
                new("DOWN", "Turn around"),
                new("C", "Camp"),
                new("S", "Search for a fight"),
            ],
        };
    }

    public override void Key(string key)
    {
        switch (key)
        {
            case "UP": G.StepForward(); break;
            case "LEFT": G.Turn(right: false); break;
            case "RIGHT": G.Turn(right: true); break;
            case "DOWN": G.TurnAround(); break;
            case "C":
                G.Push(new CampState(G));
                G.RequestSave();
                break;
            case "S": G.StartFight(roomKey: null); break;
        }
    }
}

/// <summary>Resting in the maze: inspect, equip, cast, reorder, or save and quit.</summary>
internal sealed class CampState(Game game) : State(game)
{
    public override Screen Render()
    {
        var lines = new List<Line> { "The party makes camp. Nothing will disturb you here.", "" };
        var options = new List<MenuOption>();
        var party = G.Party;
        for (int i = 0; i < party.Count; i++)
            options.Add(new(Keyed(i), $"Inspect {party[i].Name}"));
        options.Add(new("O", "Reorder the party", party.Count > 1));
        options.Add(new("Q", "Save and quit"));
        options.Add(new("ESC", "Leave camp"));
        return new Screen { Title = "CAMP", Pane = Pane.Maze, Place = "maze", Lines = lines, Options = options };
    }

    public override void Key(string key)
    {
        switch (key)
        {
            case "O": G.Push(new ReorderState(G)); return;
            case "Q": G.RequestQuit(); return;
            case "ESC": G.Pop(); return;
        }
        var party = G.Party;
        if (IndexOf(key) is { } i && i < party.Count)
            G.Push(new SheetState(G, party[i], inMaze: true));
    }
}

/// <summary>Stairs: take them or not.</summary>
internal sealed class StairsState(Game game, bool up) : State(game)
{
    public override Screen Render() => new()
    {
        Title = up ? "STAIRS UP" : "STAIRS DOWN",
        Pane = Pane.Maze,
        Place = "maze",
        Lines =
        [
            up ? (W.Depth == 1 ? "There is a ladder up to the castle here." : "There are stairs going up here.")
               : "There are stairs going down here.",
            "",
            up ? (W.Depth == 1 ? "Climb out of the maze?" : "Take them up?") : "Take them down?",
        ],
        Options = [new("Y", "Yes"), new("N", "No")],
    };

    public override void Key(string key)
    {
        G.Pop();
        if (key == "Y") G.UseStairs(up);
    }
}

/// <summary>A lift: choose a level, or stay.</summary>
internal sealed class LiftState(Game game, LiftStop lift) : State(game)
{
    private bool Works => !lift.NeedsRibbon || W.PartyHas(Content.Items.RibbonId);

    public override Screen Render()
    {
        var lines = new List<Line> { "An iron cage hangs in a shaft. A lever beside it is marked with levels:", "" };
        var options = new List<MenuOption>();
        if (!Works)
        {
            lines.Add(new("The lever won't move. A slot beside it is shaped like a ribbon.", Tone.Dim));
        }
        else
        {
            for (int d = lift.Low; d <= lift.High; d++)
                options.Add(new(d.ToString(), d == W.Depth ? $"Level {d} (here)" : $"Level {d}", d != W.Depth));
            lines.Add($"The lift serves levels {lift.Low} to {lift.High}.");
        }
        options.Add(new("ESC", "Step out"));
        return new Screen { Title = "A LIFT", Pane = Pane.Maze, Place = "maze", Lines = lines, Options = options };
    }

    public override void Key(string key)
    {
        G.Pop();
        if (key != "ESC" && int.TryParse(key, out int depth)) G.RideLift(depth);
    }
}

/// <summary>Another party's remains: carry them home?</summary>
internal sealed class BodiesState(Game game, List<Character> bodies) : State(game)
{
    public override Screen Render()
    {
        var lines = new List<Line> { "You find the remains of another party:", "" };
        foreach (var b in bodies) lines.Add($"{b.Name,-15} {b.ClassTag}  {b.Status.Label()}");
        lines.Add("");
        int room = World.MaxParty - W.Party.Count;
        lines.Add(room == 0 ? new Line("Your party has no room to carry anyone.", Tone.Dim) : new Line($"You can carry {Math.Min(room, bodies.Count)} of them."));
        return new Screen
        {
            Title = "REMAINS",
            Pane = Pane.Maze,
            Place = "maze",
            Lines = lines,
            Options = [new("P", "Pick them up", room > 0), new("ESC", "Leave them")],
        };
    }

    public override void Key(string key)
    {
        G.Pop();
        if (key != "P") return;
        foreach (var b in bodies.Take(World.MaxParty - W.Party.Count))
        {
            b.LeftInMaze = null;
            W.Party.Add(b.Id);
            G.Say($"You carry {b.Name}.");
        }
    }
}

/// <summary>PASSAR: name a place by offset and depth. Into rock means death.</summary>
internal sealed class TeleportState(Game game) : State(game)
{
    public override Screen Render() => new()
    {
        Title = "PASSAR",
        Pane = Pane.Maze,
        Place = "maze",
        Lines =
        [
            $"The party is on level {W.Depth} at {W.X} east, {W.Y} north.",
            "Where to? Give the square and level, e.g. \"3 17 5\" for 3 east,",
            "17 north on level 5. Choose a square in the maze; outside it is solid rock.",
            new("(Level 0 is the castle.)", Tone.Dim),
        ],
        Prompt = "East North Level:",
        Options = [new("ESC", "Cancel (the spell is spent)")],
    };

    public override void Key(string key) => G.Pop();

    public override void Text(string text)
    {
        var parts = text.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y) || !int.TryParse(parts[2], out int depth))
            return;
        G.Pop();
        G.Play(SoundCue.Teleport);
        if (depth == 0)
        {
            G.ReturnToCastle("The party teleports home to the castle.");
            return;
        }
        if (depth < 0 || depth > MazeBuilder.Depth || !Level.InBounds(x, y))
        {
            foreach (var c in G.Party)
            {
                c.Status = Status.Lost;
                c.Hp = 0;
            }
            G.Tell("PASSAR", [new("The party teleports into solid rock.", Tone.Bad), new("Its members are lost forever.", Tone.Bad)], () =>
            {
                W.Party.Clear();
                G.ReturnToCastle("A party was lost in the rock.");
            });
            return;
        }
        G.Teleport(depth, random: false, x, y);
        G.Say($"The party teleports to level {depth}.");
        G.Arrive();
    }
}
