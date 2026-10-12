using System.Text.Json;
using System.Text.Json.Serialization;
using Wizardry.Infrastructure.Storage;

namespace Wizardry.Infrastructure.Settings;

/// <summary>Per-user preferences. Rule-affecting options are stored with each save instead.</summary>
public sealed class GameSettings
{
    /// <summary>New games: a patrol brings a fallen party home (off: the classic rule, bodies stay in the maze).</summary>
    public bool RescuePatrols { get; set; } = true;
    public double UiScale { get; set; } = 1.0;
    public bool HighContrast { get; set; }
    public bool ReducedMotion { get; set; }
    public bool SoundEnabled { get; set; } = true;
    public bool MusicEnabled { get; set; } = true;
    public bool AmbienceEnabled { get; set; } = true;
    public double MasterVolume { get; set; } = 0.8;
    public double EffectsVolume { get; set; } = 0.8;
    public double MusicVolume { get; set; } = 0.5;
    public double AmbienceVolume { get; set; } = 0.4;
    /// <summary>Sound pack folder names (see src/Wizardry.Desktop/soundpacks).</summary>
    public string EffectsPack { get; set; } = "avawiz-retro";
    public string MusicPack { get; set; } = "avawiz-music";
    /// <summary>OpenAL mixing period: auto, small, medium or large (larger rides out VM audio jitter).</summary>
    public string AudioBuffer { get; set; } = "auto";
    public bool ShowAutomap { get; set; } = true;
    public bool ShowTutorial { get; set; } = true;
    /// <summary>Optional remapping: command name → key names (Avalonia Key enum names).</summary>
    public Dictionary<string, string[]> KeyBindings { get; set; } = [];

    public GameSettings Clone()
    {
        var c = (GameSettings)MemberwiseClone();
        c.KeyBindings = KeyBindings.ToDictionary(kv => kv.Key, kv => (string[])kv.Value.Clone());
        return c;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(GameSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;

public sealed class SettingsStore(string path)
{
    public string Path { get; } = path;

    public static SettingsStore ForCurrentUser() => new(UserDataPaths.ForCurrentUser().SettingsFile);

    /// <summary>Missing or unreadable settings fall back to defaults (and report why).</summary>
    public GameSettings Load(out string? problem)
    {
        problem = null;
        try
        {
            if (!File.Exists(Path)) return new GameSettings();
            return JsonSerializer.Deserialize(File.ReadAllText(Path), SettingsJsonContext.Default.GameSettings) ?? new GameSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            problem = "Settings could not be read and were reset to defaults: " + e.Message;
            return new GameSettings();
        }
    }

    public Task SaveAsync(GameSettings settings, CancellationToken ct = default) =>
        AtomicFile.WriteAllTextAsync(Path, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.GameSettings), ct);
}
