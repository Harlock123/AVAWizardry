using Wizardry.Core.Model;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Screens;

/// <summary>One screen of the game's menu stack.</summary>
internal abstract class State(Game game)
{
    protected Game G { get; } = game;
    protected World W => G.World;

    public abstract Screen Render();
    public virtual void Key(string key) { }
    public virtual void Text(string text) { }

    protected static string Keyed(int index) => index < 9 ? (index + 1).ToString() : ((char)('A' + index - 9)).ToString();

    protected static int? IndexOf(string key)
    {
        if (key.Length != 1) return null;
        char c = key[0];
        if (c is >= '1' and <= '9') return c - '1';
        return null;
    }

    protected static string Money(long gold) => $"{gold:N0} GP";
}

/// <summary>Lines to read, then a key to go on.</summary>
internal sealed class MessageState(Game game, string title, List<Line> lines, Action? then, Pane pane) : State(game)
{
    public override Screen Render() => new()
    {
        Title = title,
        Lines = lines,
        Options = [new("ENTER", "Continue")],
        Pane = pane,
        Place = G.World.InMaze ? "maze" : "castle",
    };

    public override void Key(string key)
    {
        G.Pop();
        then?.Invoke();
    }
}

/// <summary>Picks one character from a list, then calls back (or goes back with ESC).</summary>
internal sealed class PickState(Game game, string title, string question, IReadOnlyList<Character> choices,
    Action<Character> picked, Func<Character, string>? describe = null, Pane pane = Pane.Castle, string place = "castle",
    bool popFirst = true) : State(game)
{
    public override Screen Render()
    {
        var lines = new List<Line> { question, "" };
        var options = new List<MenuOption>();
        for (int i = 0; i < choices.Count && i < 9; i++)
        {
            var c = choices[i];
            lines.Add($"{i + 1}) {(describe ?? Describe)(c)}");
            options.Add(new(Keyed(i), c.Name));
        }
        if (choices.Count == 0) lines.Add(new("(nobody)", Tone.Dim));
        options.Add(new("ESC", "Back"));
        return new Screen { Title = title, Lines = lines, Options = options, Pane = pane, Place = place };
    }

    private static string Describe(Character c) =>
        $"{c.Name,-15} {c.ClassTag}  L{c.Level,-3} {c.Status.Label(),-7} {c.Hp}/{c.MaxHp}";

    public override void Key(string key)
    {
        if (key == "ESC") { G.Pop(); return; }
        if (IndexOf(key) is { } i && i < choices.Count)
        {
            if (popFirst) G.Pop();
            picked(choices[i]);
        }
    }
}

/// <summary>A yes/no question.</summary>
internal sealed class ConfirmState(Game game, string title, List<Line> lines, Action yes, Action? no = null, Pane pane = Pane.Castle, string place = "castle") : State(game)
{
    public override Screen Render() => new()
    {
        Title = title,
        Lines = lines,
        Options = [new("Y", "Yes"), new("N", "No")],
        Pane = pane,
        Place = place,
    };

    public override void Key(string key)
    {
        G.Pop();
        if (key == "Y") yes();
        else no?.Invoke();
    }
}

/// <summary>Asks for a line of text.</summary>
internal sealed class InputState(Game game, string title, List<Line> lines, string prompt, Func<string, string?> accept,
    Pane pane = Pane.Castle, string place = "castle") : State(game)
{
    private string? _error;

    public override Screen Render()
    {
        var all = new List<Line>(lines);
        if (_error is not null) { all.Add(""); all.Add(new(_error, Tone.Bad)); }
        return new Screen { Title = title, Lines = all, Prompt = prompt, Options = [new("ESC", "Back")], Pane = pane, Place = place };
    }

    public override void Key(string key)
    {
        if (key == "ESC") G.Pop();
    }

    /// <summary>
    /// The callback returns an error to show, or null when it has taken the text. This screen is
    /// closed before the callback runs, so the callback can open the next one.
    /// </summary>
    public override void Text(string text)
    {
        G.Pop();
        _error = accept(text);
        if (_error is not null) G.Push(this);
    }
}
