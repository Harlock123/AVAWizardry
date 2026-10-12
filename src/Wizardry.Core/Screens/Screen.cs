namespace Wizardry.Core.Screens;

/// <summary>How a line of screen text is coloured.</summary>
public enum Tone { Normal, Dim, Title, Good, Bad, Gold, Info }

public sealed record Line(string Text, Tone Tone = Tone.Normal)
{
    public static implicit operator Line(string text) => new(text);
}

/// <summary>A choice on a screen. <see cref="Key"/> is what the player presses: a letter, a digit, or ENTER/ESC/UP/DOWN/LEFT/RIGHT.</summary>
public sealed record MenuOption(string Key, string Label, bool Enabled = true);

/// <summary>What the picture pane shows alongside the text.</summary>
public enum Pane { Title, Castle, Maze, Combat, Chest, Victory }

/// <summary>
/// Everything one screen shows: a heading, lines of text, the choices, and optionally a text box.
/// The desktop app only draws these; tests drive the game by pressing their keys.
/// </summary>
public sealed class Screen
{
    public string Title { get; init; } = "";
    public List<Line> Lines { get; init; } = [];
    public List<MenuOption> Options { get; init; } = [];
    /// <summary>Set when the screen wants typed text (a name, a spell, coordinates).</summary>
    public string? Prompt { get; init; }
    public Pane Pane { get; init; } = Pane.Castle;
    /// <summary>Which castle place to picture (tavern, inn, shop, temple, edge, training, castle).</summary>
    public string Place { get; init; } = "castle";

    public bool Offers(string key) => Options.Any(o => o.Enabled && o.Key == key);
}

/// <summary>Sounds the game asks for; the host decides how they sound.</summary>
public enum SoundCue
{
    Step, Bump, Turn, Door, Encounter, Hit, Miss, Hurt, Kill, Spell, Treasure, Trap, Teleport,
    Fall, Stairs, Inn, LevelUp, Death, Temple, Victory, Breath, Paralyzed, Drain, Spinner, Coins,
}
