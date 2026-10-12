using Wizardry.Core.Model;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Screens;

/// <summary>Where characters are made, renamed, retrained and struck from the roster.</summary>
internal sealed class TrainingState(Game game) : State(game)
{
    public override Screen Render()
    {
        var lines = new List<Line> { "The training grounds. Recruits drill in the yard.", "" };
        if (W.Roster.Count == 0) lines.Add(new("The roster is empty.", Tone.Dim));
        foreach (var c in W.Roster)
        {
            string where = W.Party.Contains(c.Id) ? "in party" : c.LeftInMaze is { } m ? $"in maze L{m.Level}" : "";
            lines.Add(new($"{c.Name,-15} {c.ClassTag}  L{c.Level,-3} {c.Status.Label(),-7} {where}", c.IsAlive ? Tone.Normal : Tone.Dim));
        }
        var free = Free();
        return new Screen
        {
            Title = "TRAINING GROUNDS",
            Place = "training",
            Lines = lines,
            Options =
            [
                new("C", "Create a character", W.Roster.Count < World.MaxRoster),
                new("I", "Inspect a character", W.Roster.Count > 0),
                new("R", "Rename a character", free.Count > 0),
                new("H", "Change class", free.Any(c => Progression.ClassChanges(c).Any() && c.IsAlive)),
                new("D", "Delete a character", free.Count > 0),
                new("ESC", "Leave"),
            ],
        };
    }

    /// <summary>Characters in town and not in the party.</summary>
    private List<Character> Free() => W.Roster.Where(c => !W.Party.Contains(c.Id) && c.LeftInMaze is null).ToList();

    public override void Key(string key)
    {
        switch (key)
        {
            case "C":
                G.Push(new InputState(G, "CREATE", ["Every adventurer needs a name."], "Name (up to 15 letters):", name =>
                {
                    if (CheckName(name) is { } error) return error;
                    G.Push(new CreationState(G, name.ToUpperInvariant()));
                    return null;
                }, place: "training"));
                break;
            case "I":
                G.Push(new PickState(G, "INSPECT", "Inspect whom?", W.Roster, c => G.Push(new SheetState(G, c, inMaze: false)), place: "training"));
                break;
            case "R":
                G.Push(new PickState(G, "RENAME", "Rename whom?", Free(), c =>
                    G.Push(new InputState(G, "RENAME", [$"A new name for {c.Name}?"], "New name:", name =>
                    {
                        if (CheckName(name) is { } error) return error;
                        c.Name = name.ToUpperInvariant();
                        return null;
                    }, place: "training")), place: "training"));
                break;
            case "H":
                G.Push(new PickState(G, "CHANGE CLASS", "Who will retrain?", Free().Where(c => c.IsAlive && Progression.ClassChanges(c).Any()).ToList(),
                    c => G.Push(new ClassChangeState(G, c)), place: "training"));
                break;
            case "D":
                G.Push(new PickState(G, "DELETE", "Strike whom from the roster?", Free(), c =>
                    G.Push(new ConfirmState(G, "DELETE", [$"Really delete {c.Name}? This can't be undone."], () =>
                    {
                        W.Roster.Remove(c);
                        G.Say($"{c.Name} has left the castle for good.");
                    }, place: "training")), place: "training"));
                break;
            case "ESC":
                G.Pop();
                break;
        }
    }

    private string? CheckName(string name)
    {
        name = name.Trim();
        if (name.Length is 0 or > 15) return "A name is 1 to 15 characters.";
        if (!name.All(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '\'')) return "Letters, digits, spaces, hyphens and apostrophes only.";
        if (W.Roster.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))) return "Someone already has that name.";
        return null;
    }
}

/// <summary>Race, alignment, bonus points, class: the classic order.</summary>
internal sealed class CreationState : State
{
    private enum Step { Race, Alignment, Points, Class }

    private readonly string _name;
    private Step _step = Step.Race;
    private Race _race;
    private Alignment _alignment;
    private int[] _stats = new int[6];
    private int _bonus;
    private readonly Stack<int> _spent = new();

    public CreationState(Game game, string name) : base(game) => _name = name;

    public override Screen Render()
    {
        var lines = new List<Line> { new($"NAME: {_name}", Tone.Gold) };
        if (_step > Step.Race) lines.Add($"RACE: {_race.ToString().ToUpperInvariant()}");
        if (_step > Step.Alignment) lines.Add($"ALIGNMENT: {_alignment.ToString().ToUpperInvariant()}");
        lines.Add("");
        var options = new List<MenuOption>();
        switch (_step)
        {
            case Step.Race:
                lines.Add("Choose a race:");
                lines.Add("");
                foreach (var (r, i) in Enum.GetValues<Race>().Select((r, i) => (r, i)))
                {
                    var b = RuleTables.BaseStats(r);
                    lines.Add(new($"{i + 1}) {r,-7} STR {b[0],2} IQ {b[1],2} PIE {b[2],2} VIT {b[3],2} AGI {b[4],2} LUC {b[5],2}", Tone.Normal));
                    options.Add(new(Keyed(i), r.ToString()));
                }
                break;
            case Step.Alignment:
                lines.Add("Choose an alignment:");
                lines.Add(new("Good: priests, bishops, samurai and lords. Evil: priests, thieves, bishops, ninjas.", Tone.Dim));
                lines.Add(new("Neutral: thieves and samurai. Fighters and mages may be anything.", Tone.Dim));
                lines.Add(new("Good and evil characters won't share a party.", Tone.Dim));
                options.Add(new("G", "Good"));
                options.Add(new("N", "Neutral"));
                options.Add(new("E", "Evil"));
                break;
            case Step.Points:
                lines.Add(new($"BONUS POINTS: {_bonus}", _bonus > 0 ? Tone.Gold : Tone.Good));
                lines.Add("");
                for (int i = 0; i < 6; i++)
                    lines.Add($"{i + 1}) {RuleTables.AllStats[i].Abbrev(),-5}{_stats[i],3}");
                lines.Add("");
                var classes = Progression.EligibleClasses(_alignment, _stats).ToList();
                lines.Add(new("Can be: " + (classes.Count == 0 ? "(nothing yet)" : string.Join(", ", classes.Select(c => c.ToString().ToUpperInvariant()))), Tone.Info));
                lines.Add(new("Spend every point, then choose a class. A poor roll? Reroll the bonus.", Tone.Dim));
                for (int i = 0; i < 6; i++)
                    options.Add(new(Keyed(i), $"+{RuleTables.AllStats[i].Abbrev()}", _bonus > 0 && _stats[i] < RuleTables.MaxStat));
                options.Add(new("U", "Undo", _spent.Count > 0));
                options.Add(new("R", "Reroll the bonus"));
                options.Add(new("ENTER", "Choose a class", (_bonus == 0 || _stats.All(s => s >= RuleTables.MaxStat)) && classes.Count > 0));
                break;
            case Step.Class:
                lines.Add("Choose a class:");
                lines.Add("");
                var eligible = Progression.EligibleClasses(_alignment, _stats).ToList();
                for (int i = 0; i < eligible.Count; i++)
                {
                    lines.Add($"{i + 1}) {eligible[i].ToString().ToUpperInvariant(),-8} {Describe(eligible[i])}");
                    options.Add(new(Keyed(i), eligible[i].ToString()));
                }
                break;
        }
        options.Add(new("ESC", _step == Step.Race ? "Cancel" : "Back"));
        return new Screen { Title = "CREATE A CHARACTER", Place = "training", Lines = lines, Options = options };
    }

    public static string Describe(CharClass c) => c switch
    {
        CharClass.Fighter => "Fights well; the best hit points.",
        CharClass.Mage => "Mage spells; weak in a fight.",
        CharClass.Priest => "Priest spells and dispelling the undead.",
        CharClass.Thief => "Opens chests and hides; a modest fighter.",
        CharClass.Bishop => "Both kinds of spells, slowly; identifies items.",
        CharClass.Samurai => "A fighter who learns mage spells.",
        CharClass.Lord => "A fighter who learns priest spells.",
        _ => "Fights best unarmed and unarmoured; deadly blows.",
    };

    public override void Key(string key)
    {
        if (key == "ESC")
        {
            if (_step == Step.Race) G.Pop();
            else _step--;
            return;
        }
        switch (_step)
        {
            case Step.Race when IndexOf(key) is { } r && r < 5:
                _race = (Race)r;
                _step = Step.Alignment;
                break;
            case Step.Alignment:
                _alignment = key switch { "G" => Alignment.Good, "E" => Alignment.Evil, _ => Alignment.Neutral };
                _stats = RuleTables.BaseStats(_race);
                _bonus = Progression.RollBonus(G.Rng);
                _spent.Clear();
                _step = Step.Points;
                break;
            case Step.Points:
                if (IndexOf(key) is { } s && s < 6 && _bonus > 0 && _stats[s] < RuleTables.MaxStat)
                {
                    _stats[s]++;
                    _bonus--;
                    _spent.Push(s);
                }
                else if (key == "U" && _spent.Count > 0)
                {
                    _stats[_spent.Pop()]--;
                    _bonus++;
                }
                else if (key == "R")
                {
                    _stats = RuleTables.BaseStats(_race);
                    _bonus = Progression.RollBonus(G.Rng);
                    _spent.Clear();
                }
                else if (key == "ENTER") _step = Step.Class;
                break;
            case Step.Class:
                var eligible = Progression.EligibleClasses(_alignment, _stats).ToList();
                if (IndexOf(key) is { } c && c < eligible.Count)
                {
                    var cls = eligible[c];
                    var ch = Progression.Create(_name, _race, _alignment, cls, _stats, G.Rng);
                    G.Pop();
                    G.Push(new ConfirmState(G, "CREATE A CHARACTER", [.. SheetState.Describe(ch, W), "", "Keep this character?"], () =>
                    {
                        W.Roster.Add(ch);
                        G.Say($"{ch.Name} joins the roster.");
                        G.RequestSave();
                    }, place: "training"));
                }
                break;
        }
    }
}

internal sealed class ClassChangeState(Game game, Character who) : State(game)
{
    public override Screen Render()
    {
        var options = Progression.ClassChanges(who).ToList();
        var lines = new List<Line>
        {
            $"{who.Name} can retrain as:",
            new("A new class starts again at level 1, with attributes reset to the race's", Tone.Dim),
            new("base and a few years older. Known spells, items and gold are kept.", Tone.Dim),
            "",
        };
        var menu = new List<MenuOption>();
        for (int i = 0; i < options.Count; i++)
        {
            lines.Add($"{i + 1}) {options[i].ToString().ToUpperInvariant(),-8} {CreationState.Describe(options[i])}");
            menu.Add(new(Keyed(i), options[i].ToString()));
        }
        menu.Add(new("ESC", "Back"));
        return new Screen { Title = "CHANGE CLASS", Place = "training", Lines = lines, Options = menu };
    }

    public override void Key(string key)
    {
        if (key == "ESC") { G.Pop(); return; }
        var options = Progression.ClassChanges(who).ToList();
        if (IndexOf(key) is not { } i || i >= options.Count) return;
        var to = options[i];
        G.Pop();
        G.Push(new ConfirmState(G, "CHANGE CLASS", [$"Retrain {who.Name} as a {to.ToString().ToUpperInvariant()}?"], () =>
        {
            Progression.ChangeClass(who, to, G.Rng);
            G.Tell("CHANGE CLASS", [new($"{who.Name} is now a level 1 {to.ToString().ToUpperInvariant()}.", Tone.Good)]);
            G.RequestSave();
        }, place: "training"));
    }
}
