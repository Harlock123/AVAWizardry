using Avalonia.Input;

namespace Wizardry.Desktop.Input;

/// <summary>Turns keyboard keys into the game's key names (letters, digits, UP, ENTER, ESC...).</summary>
public static class KeyMap
{
    public static string? Name(Key key) => key switch
    {
        Key.Up or Key.NumPad8 => "UP",
        Key.Down or Key.NumPad2 => "DOWN",
        Key.Left or Key.NumPad4 => "LEFT",
        Key.Right or Key.NumPad6 => "RIGHT",
        Key.Enter or Key.Space => "ENTER",
        Key.Escape or Key.Back => "ESC",
        >= Key.A and <= Key.Z => ((char)('A' + (key - Key.A))).ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (key - Key.NumPad0))).ToString(),
        _ => null,
    };

    /// <summary>
    /// In the maze the WASD-style letters walk too: W forward, A and D turn, X turns around
    /// (only where the screen doesn't use the letter itself).
    /// </summary>
    public static string? MazeAlias(string key) => key switch
    {
        "W" => "UP",
        "A" => "LEFT",
        "D" => "RIGHT",
        "X" => "DOWN",
        _ => null,
    };

    /// <summary>How a key is shown on its button.</summary>
    public static string Label(string key) => key switch
    {
        "UP" => "↑",
        "DOWN" => "↓",
        "LEFT" => "←",
        "RIGHT" => "→",
        "ENTER" => "Enter",
        "ESC" => "Esc",
        _ => key,
    };
}
