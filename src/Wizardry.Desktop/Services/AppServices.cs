using Wizardry.Audio;
using Wizardry.Infrastructure.Saves;
using Wizardry.Infrastructure.Settings;
using Wizardry.Infrastructure.Storage;

namespace Wizardry.Desktop.Services;

/// <summary>Application-wide services, created once at startup and passed to view models.</summary>
public sealed class AppServices : IDisposable
{
    public required SaveStore Saves { get; init; }
    public required SettingsStore SettingsStore { get; init; }
    public required GameSettings Settings { get; set; }
    public required IAudioEngine Audio { get; init; }
    public required SoundDirector Sound { get; init; }
    public IReadOnlyList<SoundPack> SoundPacks { get; init; } = [];
    public string? StartupProblem { get; init; }

    /// <summary>Why the game is silent (no device, library missing...), or null when sound works.</summary>
    public string? AudioProblem => Audio is NullAudioEngine n && !QuietAboutAudio ? n.Reason ?? "Sound is turned off (--no-audio)." : null;

    /// <summary>Tests run silent on purpose; there's nothing to tell the player.</summary>
    public bool QuietAboutAudio { get; init; }

    public static string UserAmbienceDirectory =>
        Path.Combine(UserDataPaths.ForCurrentUser().ConfigRoot, "ambience");

    public static AppServices CreateDefault(string[] args)
    {
        var settingsStore = SettingsStore.ForCurrentUser();
        var settings = settingsStore.Load(out var problem);
        // Audio is optional: without a device (or with --no-audio) the game is simply silent.
        IAudioEngine engine = args.Contains("--no-audio")
            ? new NullAudioEngine()
            : OpenAlAudioEngine.CreateOrSilent(AudioBuffer.PeriodFrames(settings.AudioBuffer, VirtualMachine.Detect()));
        var packProblems = new List<string>();
        var packs = SoundPackCatalog.DiscoverDefault(packProblems);
        var services = new AppServices
        {
            Saves = SaveStore.ForCurrentUser(),
            SettingsStore = settingsStore,
            Settings = settings,
            Audio = engine,
            Sound = new SoundDirector(engine),
            SoundPacks = packs,
            StartupProblem = string.Join(" ", new[] { problem }.Concat(packProblems).Where(p => p is not null)) is { Length: > 0 } all ? all : null,
        };
        services.ApplyAudioSettings();
        return services;
    }

    /// <summary>For headless tests: everything under a temporary directory, and no sound device.</summary>
    public static AppServices CreateIsolated(string root, IAudioEngine? audio = null)
    {
        var engine = audio ?? new NullAudioEngine("tests");
        var services = new AppServices
        {
            Saves = new SaveStore(Path.Combine(root, "saves")),
            SettingsStore = new SettingsStore(Path.Combine(root, "settings.json")),
            Settings = new GameSettings(),
            Audio = engine,
            Sound = new SoundDirector(engine),
            SoundPacks = SoundPackCatalog.Discover([SoundPackCatalog.DefaultDirectory]),
            QuietAboutAudio = audio is null,
        };
        services.ApplyAudioSettings();
        return services;
    }

    /// <summary>Pushes the sound settings to the engine and director.</summary>
    public void ApplyAudioSettings()
    {
        var s = Settings;
        Audio.SetVolumes((float)s.MasterVolume, (float)s.EffectsVolume, (float)s.MusicVolume);
        Audio.SetAmbienceVolume((float)s.AmbienceVolume);
        Sound.Effects = SoundPacks.FirstOrDefault(p => p.Id == s.EffectsPack && p.HasSounds) ?? SoundPacks.FirstOrDefault(p => p.HasSounds);
        Sound.MusicPack = SoundPacks.FirstOrDefault(p => p.Id == s.MusicPack && p.HasMusic) ?? SoundPacks.FirstOrDefault(p => p.HasMusic);
        Sound.EffectsEnabled = s.SoundEnabled;
        Sound.MusicEnabled = s.MusicEnabled;
        Sound.AmbienceEnabled = s.AmbienceEnabled;
        Sound.AmbienceFolders = [UserAmbienceDirectory, Path.Combine(AppContext.BaseDirectory, "ambience")];
    }

    public async Task SaveSettingsAsync()
    {
        try { await SettingsStore.SaveAsync(Settings); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* non-fatal; settings stay in memory */ }
    }

    public void Dispose()
    {
        Sound.Dispose();
        Audio.Dispose();
    }
}
