using Wizardry.Core.Content;
using Wizardry.Core.Model;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Screens;

/// <summary>The castle's market street: every service is a door off it.</summary>
internal sealed class CastleState(Game game) : State(game)
{
    public override Screen Render() => new()
    {
        Title = "CASTLE",
        Place = "castle",
        Lines =
        [
            "The castle of the Overlord Kessar. Beneath its walls lie the",
            "Proving Grounds, where the mad archmage Morthrain hides with the",
            "stolen Amulet of Thal.",
            "",
            new("Where will you go?", Tone.Dim),
        ],
        Options =
        [
            new("T", "The Gilded Flagon (tavern)"),
            new("I", "The Wayfarer's Rest (inn)"),
            new("S", "Harrow's Trading Post"),
            new("P", "Temple of Saint Aldric"),
            new("E", "Edge of Town"),
        ],
    };

    public override void Key(string key)
    {
        switch (key)
        {
            case "T": G.Push(new TavernState(G)); break;
            case "I": G.Push(new InnState(G)); break;
            case "S": G.Push(new ShopState(G)); break;
            case "P": G.Push(new TempleState(G)); break;
            case "E": G.Push(new EdgeState(G)); break;
        }
    }
}

/// <summary>Where parties are put together.</summary>
internal sealed class TavernState(Game game) : State(game)
{
    public override Screen Render()
    {
        var party = G.Party;
        return new Screen
        {
            Title = "THE GILDED FLAGON",
            Place = "tavern",
            Lines =
            [
                "Adventurers crowd the long tables, boasting and nursing their wounds.",
                "",
                new(party.Count == 0 ? "You have no party yet. Add up to six characters." : $"Your party has {party.Count} member{(party.Count == 1 ? "" : "s")}.", Tone.Dim),
            ],
            Options =
            [
                new("A", "Add a member", party.Count < World.MaxParty),
                new("R", "Remove a member", party.Count > 0),
                new("I", "Inspect a member", party.Count > 0),
                new("O", "Reorder the party", party.Count > 1),
                new("D", "Divvy gold", party.Count > 1),
                new("ESC", "Leave"),
            ],
        };
    }

    public override void Key(string key)
    {
        switch (key)
        {
            case "A":
                var candidates = W.Roster.Where(c => G.CanJoin(c, out _)).ToList();
                if (candidates.Count == 0)
                {
                    G.Tell("THE GILDED FLAGON", [W.Roster.Count == 0
                        ? "Nobody here is looking for a party. Create characters at the Training Grounds (Edge of Town)."
                        : "Nobody else here can join this party."], pane: Pane.Castle);
                    return;
                }
                G.Push(new PickState(G, "ADD A MEMBER", "Who will join?", candidates, c =>
                {
                    W.Party.Add(c.Id);
                    G.Say($"{c.Name} joins the party.");
                }, place: "tavern"));
                break;
            case "R":
                G.Push(new PickState(G, "REMOVE A MEMBER", "Who will leave?", G.Party, c =>
                {
                    W.Party.Remove(c.Id);
                    G.Say($"{c.Name} leaves the party.");
                }, place: "tavern"));
                break;
            case "I":
                G.Push(new PickState(G, "INSPECT", "Inspect whom?", G.Party, c => G.Push(new SheetState(G, c, inMaze: false)), place: "tavern"));
                break;
            case "O":
                G.Push(new ReorderState(G));
                break;
            case "D":
                G.DivvyGold();
                G.Tell("THE GILDED FLAGON", ["The party's gold is shared out evenly."]);
                break;
            case "ESC":
                G.Pop();
                break;
        }
    }
}

/// <summary>Picks the party's new order one member at a time.</summary>
internal sealed class ReorderState(Game game) : State(game)
{
    private readonly List<string> _order = [];

    public override Screen Render()
    {
        var party = G.Party;
        var remaining = party.Where(c => !_order.Contains(c.Id)).ToList();
        var lines = new List<Line> { "Choose the members in their new order, front rank first.", "" };
        for (int i = 0; i < _order.Count; i++) lines.Add(new($"{i + 1}. {W.Find(_order[i])!.Name}", Tone.Good));
        lines.Add("");
        var options = new List<MenuOption>();
        for (int i = 0; i < remaining.Count; i++)
        {
            lines.Add($"{i + 1}) {remaining[i].Name}");
            options.Add(new(Keyed(i), remaining[i].Name));
        }
        options.Add(new("ESC", "Cancel"));
        return new Screen { Title = "REORDER", Lines = lines, Options = options, Place = W.InMaze ? "maze" : "tavern", Pane = W.InMaze ? Pane.Maze : Pane.Castle };
    }

    public override void Key(string key)
    {
        if (key == "ESC") { G.Pop(); return; }
        var remaining = G.Party.Where(c => !_order.Contains(c.Id)).ToList();
        if (IndexOf(key) is not { } i || i >= remaining.Count) return;
        _order.Add(remaining[i].Id);
        if (_order.Count == W.Party.Count)
        {
            W.Party.Clear();
            W.Party.AddRange(_order);
            G.Pop();
        }
    }
}

/// <summary>Rest heals, restores spells and is where levels are gained, at the price of time.</summary>
internal sealed class InnState(Game game) : State(game)
{
    public sealed record Room(string Key, string Name, int Cost, int Heal);

    public static readonly Room[] Rooms =
    [
        new("S", "The Stables", 0, 0),
        new("C", "A Cot", 10, 2),
        new("E", "Economy Room", 50, 6),
        new("M", "Merchant Suite", 200, 15),
        new("R", "Royal Suite", 500, 30),
    ];

    public override Screen Render() => new()
    {
        Title = "THE WAYFARER'S REST",
        Place = "inn",
        Lines =
        [
            "\"Rooms by the week! Rest heals and restores your spells, and",
            "you'll learn what your deeds have taught you.\"",
            "",
            new("Stables: free, no healing.  Cot: 10 GP, 2 HP a week.", Tone.Dim),
            new("Economy: 50 GP, 6 HP.  Merchant: 200 GP, 15 HP.  Royal: 500 GP, 30 HP.", Tone.Dim),
        ],
        Options =
        [
            new("1", "Rest one member", G.Party.Count > 0),
            new("A", "Rest the whole party", G.Party.Count > 0),
            new("ESC", "Leave"),
        ],
    };

    public override void Key(string key)
    {
        switch (key)
        {
            case "1":
                G.Push(new PickState(G, "WHO WILL REST?", "Who will stay at the inn?", G.Party, c => G.Push(new RoomState(G, [c])), place: "inn"));
                break;
            case "A":
                G.Push(new RoomState(G, G.Party));
                break;
            case "ESC":
                G.Pop();
                break;
        }
    }

    /// <summary>Stays until healed (or the money runs out), then checks for a new level.</summary>
    public static List<Line> Stay(Game g, Character c, Room room) => Stay(g, c, room, out _);

    /// <summary>Stays until healed (or the money runs out); <paramref name="weeksStayed"/> is how long.</summary>
    public static List<Line> Stay(Game g, Character c, Room room, out int weeksStayed)
    {
        weeksStayed = 0;
        var lines = new List<Line>();
        if (!c.IsAlive || c.Status == Status.Stoned)
        {
            lines.Add(new($"The innkeeper won't rent a room to {c.Name} in that state. Try the temple.", Tone.Bad));
            return lines;
        }
        int weeks = 0;
        long spent = 0;
        if (room.Heal == 0) weeks = 1;
        else
        {
            while (c.Hp < c.MaxHp && c.Gold - spent >= room.Cost)
            {
                weeks++;
                spent += room.Cost;
                c.Hp = Math.Min(c.MaxHp, c.Hp + room.Heal);
            }
            if (weeks == 0) weeks = 1;
        }
        c.Gold -= spent;
        Progression.Age(c, weeks);
        weeksStayed = weeks;
        c.Status = Status.Ok;
        c.Poisoned = false;
        c.RestoreSpells();
        lines.Add(new($"{c.Name} stays {weeks} week{(weeks == 1 ? "" : "s")} in {room.Name}" + (spent > 0 ? $" for {spent:N0} GP." : "."), Tone.Normal));
        if (c.Hp < c.MaxHp && room.Heal > 0) lines.Add(new($"{c.Name} runs out of money with {c.Hp}/{c.MaxHp} HP.", Tone.Dim));
        var notes = Progression.TryLevelUp(c, g.Rng);
        if (notes.Count == 0)
            lines.Add(new($"{c.Name} needs {c.ExperienceToNext:N0} more experience points to make the next level.", Tone.Dim));
        else
        {
            g.Play(SoundCue.LevelUp);
            foreach (var n in notes) lines.Add(new(n, n.Contains("lost") || n.Contains("died") ? Tone.Bad : Tone.Good));
            c.RestoreSpells();
        }
        return lines;
    }
}

internal sealed class RoomState(Game game, List<Character> guests) : State(game)
{
    public override Screen Render()
    {
        var who = guests.Count == 1 ? guests[0].Name : "the party";
        var options = InnState.Rooms.Select(r => new MenuOption(r.Key, r.Cost == 0 ? $"{r.Name} (free)" : $"{r.Name} ({r.Cost} GP/week)")).ToList();
        options.Add(new("ESC", "Back"));
        var lines = new List<Line> { $"Which room for {who}?", "" };
        foreach (var c in guests) lines.Add(new($"{c.Name,-15} {c.Hp,4}/{c.MaxHp,-4} HP  {Money(c.Gold)}", Tone.Dim));
        return new Screen { Title = "CHOOSE A ROOM", Place = "inn", Lines = lines, Options = options };
    }

    public override void Key(string key)
    {
        if (key == "ESC") { G.Pop(); return; }
        var room = InnState.Rooms.First(r => r.Key == key);
        G.Pop();
        G.Play(SoundCue.Inn);
        var lines = new List<Line>();
        int longest = 0;
        foreach (var c in guests)
        {
            lines.AddRange(InnState.Stay(G, c, room, out int weeks));
            longest = Math.Max(longest, weeks);
            lines.Add("");
        }
        // The party's stays run side by side: the calendar moves on by the longest.
        W.Week += longest;
        G.Tell("THE WAYFARER'S REST", lines);
        G.RequestSave();
    }
}

/// <summary>Healing beyond what rest can do, for a price.</summary>
internal sealed class TempleState(Game game) : State(game)
{
    public static long Fee(Character c) => c.Status switch
    {
        Status.Paralyzed => 100L * c.Level,
        Status.Stoned => 200L * c.Level,
        Status.Dead => 250L * c.Level,
        Status.Ashes => 500L * c.Level,
        _ => 0,
    };

    public override Screen Render()
    {
        var patients = Patients();
        return new Screen
        {
            Title = "TEMPLE OF SAINT ALDRIC",
            Place = "temple",
            Lines =
            [
                "Candles burn before the altar. A priest in grey looks up.",
                "\"We tend the paralysed, the stoned, the dead, and even the ashes",
                "of the dead, for a tithe. Saint Aldric's mercy is not certain.\"",
                "",
                new("Paralysed: 100 GP a level. Stoned: 200. Dead: 250. Ashes: 500.", Tone.Dim),
                new(patients.Count == 0 ? "Nobody needs the temple's help." : $"{patients.Count} need help.", Tone.Dim),
            ],
            Options = [new("H", "Help someone", patients.Count > 0), new("ESC", "Leave")],
        };
    }

    private List<Character> Patients() => W.Roster
        .Where(c => c.LeftInMaze is null && c.Status is Status.Paralyzed or Status.Stoned or Status.Dead or Status.Ashes)
        .ToList();

    public override void Key(string key)
    {
        if (key == "ESC") { G.Pop(); return; }
        G.Push(new PickState(G, "WHO NEEDS HELP?", "Who needs the temple's help?", Patients(), patient =>
        {
            long fee = Fee(patient);
            // The patient's own purse counts: the priests take the tithe from the body's belt.
            var payers = W.Roster.Where(c => c.LeftInMaze is null && c.Gold >= fee && (c.IsAlive || c == patient)).ToList();
            if (payers.Count == 0)
            {
                G.Tell("TEMPLE OF SAINT ALDRIC", [$"The tithe for {patient.Name} is {fee:N0} GP. Nobody can pay it.", new("(Pool the party's gold at the trading post, or divvy it at the tavern.)", Tone.Dim)]);
                return;
            }
            G.Push(new PickState(G, "WHO WILL PAY?", $"The tithe is {fee:N0} GP. Who will pay?", payers, payer => Heal(patient, payer, fee),
                c => $"{c.Name,-15} {Money(c.Gold)}", place: "temple"));
        }, c => $"{c.Name,-15} {c.Status.Label(),-7} tithe {Fee(c):N0} GP", place: "temple"));
    }

    private void Heal(Character patient, Character payer, long fee)
    {
        payer.Gold -= fee;
        G.Play(SoundCue.Temple);
        var lines = new List<Line> { $"{payer.Name} pays {fee:N0} GP.", "The priests murmur and chant...", "" };
        switch (patient.Status)
        {
            case Status.Paralyzed or Status.Stoned:
                patient.Status = Status.Ok;
                lines.Add(new($"{patient.Name} is well again.", Tone.Good));
                break;
            case Status.Dead:
                if (G.Rng.Percent(Math.Clamp(50 + patient.Vit * 3, 50, 95)))
                {
                    patient.Status = Status.Ok;
                    patient.Hp = 1;
                    lines.Add(new($"{patient.Name} is alive and well!", Tone.Good));
                }
                else
                {
                    patient.Status = Status.Ashes;
                    lines.Add(new($"Oops! {patient.Name} needs more help: only ashes remain.", Tone.Bad));
                }
                break;
            case Status.Ashes:
                if (G.Rng.Percent(Math.Clamp(40 + patient.Vit * 3, 40, 90)))
                {
                    patient.Status = Status.Ok;
                    patient.Hp = patient.MaxHp;
                    lines.Add(new($"{patient.Name} rises from the ashes!", Tone.Good));
                }
                else
                {
                    patient.Status = Status.Lost;
                    lines.Add(new($"{patient.Name} is lost forever. The priests are very sorry.", Tone.Bad));
                }
                break;
        }
        patient.Poisoned = false;
        G.Tell("TEMPLE OF SAINT ALDRIC", lines);
        G.RequestSave();
    }
}

/// <summary>The road out: to the maze, the training grounds, or home.</summary>
internal sealed class EdgeState(Game game) : State(game)
{
    public override Screen Render()
    {
        bool ready = G.Party.Any(c => c.Status.CanAct());
        return new Screen
        {
            Title = "EDGE OF TOWN",
            Place = "edge",
            Lines =
            [
                "The castle wall ends here. To one side, the training grounds;",
                "ahead, the stair down into the Proving Grounds.",
                "",
                new(ready ? "Your party is ready." : "You need a party (from the tavern) to enter the maze.", Tone.Dim),
            ],
            Options =
            [
                new("M", "Enter the maze", ready),
                new("T", "Training Grounds"),
                new("Q", "Leave the game (saves)"),
                new("ESC", "Back to the castle"),
            ],
        };
    }

    public override void Key(string key)
    {
        switch (key)
        {
            case "M":
                G.EnterMaze();
                G.Reset(new MazeState(G));
                G.RequestSave();
                break;
            case "T":
                G.Push(new TrainingState(G));
                break;
            case "Q":
                G.RequestQuit();
                break;
            case "ESC":
                G.Pop();
                break;
        }
    }
}
