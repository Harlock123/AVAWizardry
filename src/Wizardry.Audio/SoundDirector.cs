using Wizardry.Core.Screens;

namespace Wizardry.Audio;

/// <summary>
/// Turns AVAWizardry's sound cues into effects and picks the music and ambience (adapted from
/// AVABand's director). The engine knows nothing about audio: the host forwards
/// <see cref="SoundCue"/> events and tells the director where the party is. Sound choices use
/// their own random generator, so they never disturb the game's deterministic randomness.
/// </summary>
public sealed class SoundDirector : IDisposable
{
    private readonly IAudioEngine _engine;
    private readonly Random _random = new();
    private readonly object _musicLock = new();
    private string? _lastTrack;

    public SoundDirector(IAudioEngine engine)
    {
        _engine = engine;
        _engine.MusicEnded += OnMusicEnded;
    }

    public IAudioEngine Engine => _engine;

    /// <summary>
    /// Pack event names for each cue, best first. AVAWizardry's own pack uses the first name; the
    /// later ones are Angband-style names, so packs made for AVABand (such as Dubtrain's) also work.
    /// </summary>
    public static IReadOnlyList<string> EventNames(SoundCue cue) => cue switch
    {
        SoundCue.Step => ["STEP"],
        SoundCue.Bump => ["BUMP", "HITWALL"],
        SoundCue.Turn => ["TURN"],
        SoundCue.Door => ["DOOR", "OPENDOOR"],
        SoundCue.Encounter => ["ENCOUNTER", "NOTICE"],
        SoundCue.Hit => ["HIT"],
        SoundCue.Miss => ["MISS"],
        SoundCue.Hurt => ["HURT", "MON_HIT"],
        SoundCue.Kill => ["KILL"],
        SoundCue.Spell => ["SPELL"],
        SoundCue.Treasure => ["TREASURE", "MONEY1"],
        SoundCue.Coins => ["COINS", "STORE5", "MONEY1"],
        SoundCue.Trap => ["TRAP", "CREATE_TRAP"],
        SoundCue.Teleport => ["TELEPORT"],
        SoundCue.Fall => ["FALL", "TPLEVEL"],
        SoundCue.Stairs => ["STAIRS", "STAIRS_DOWN"],
        SoundCue.Inn => ["INN", "STORE_ENTER"],
        SoundCue.LevelUp => ["LEVEL"],
        SoundCue.Death => ["DEATH"],
        SoundCue.Temple => ["GONG", "BELL"],
        SoundCue.Victory => ["THRONE", "IDENT_ART"],
        SoundCue.Breath => ["BREATH", "BR_FIRE"],
        SoundCue.Paralyzed => ["PARALYZED"],
        SoundCue.Drain => ["DRAIN", "DRAIN_STAT"],
        SoundCue.Spinner => ["CONFUSED"],
        _ => [],
    };

    public SoundPack? Effects { get; set; }

    public SoundPack? MusicPack
    {
        get => _musicPack;
        set { _musicPack = value; UpdateMusic(); }
    }
    private SoundPack? _musicPack;

    public bool EffectsEnabled { get; set; } = true;

    public bool MusicEnabled
    {
        get => _musicEnabled;
        set { _musicEnabled = value; UpdateMusic(); }
    }
    private bool _musicEnabled = true;

    public bool AmbienceEnabled
    {
        get => _ambienceEnabled;
        set { _ambienceEnabled = value; UpdateAmbience(); }
    }
    private bool _ambienceEnabled = true;

    /// <summary>Folders searched for ambience loops (the player's own first).</summary>
    public IReadOnlyList<string> AmbienceFolders
    {
        get => _ambienceFolders;
        set { _ambienceFolders = value; UpdateAmbience(); }
    }
    private IReadOnlyList<string> _ambienceFolders = [];

    /// <summary>Event names played (for tests and diagnostics).</summary>
    public event Action<string>? Played;

    private bool _titleScreen = true;
    private bool _inGame;
    private int _depth;
    private bool _boss;

    /// <summary>The title screen (or any menu outside a game) is showing.</summary>
    public bool TitleScreen
    {
        get => _titleScreen;
        set
        {
            if (_titleScreen == value) return;
            _titleScreen = value;
            if (value) _inGame = false;
            UpdateMusic();
            UpdateAmbience();
        }
    }

    /// <summary>Where the party is: 0 in the castle, otherwise the maze level (1-10).</summary>
    public void SetLocation(int depth)
    {
        _inGame = true;
        _titleScreen = false;
        if (_depth == depth) { UpdateMusic(); UpdateAmbience(); return; }
        _depth = depth;
        UpdateMusic();
        UpdateAmbience();
    }

    /// <summary>A guardian fight (the Warden, Morthrain) brings the boss music.</summary>
    public void SetBoss(bool boss)
    {
        if (boss == _boss) return;
        _boss = boss;
        UpdateMusic();
    }

    /// <summary>title, boss, town (the castle), dungeon (levels 1-5) or deep (6-10).</summary>
    public string MusicMood => _titleScreen || !_inGame ? "title"
        : _boss && _depth > 0 ? "boss"
        : _depth == 0 ? "town"
        : _depth >= 6 ? "deep" : "dungeon";

    public void Play(SoundCue cue)
    {
        if (!EffectsEnabled || Effects is not { } pack) return;
        foreach (var name in EventNames(cue))
        {
            if (!pack.Sounds.TryGetValue(name, out var files) || files.Count == 0) continue;
            _engine.PlayEffect(pack.Resolve(files[_random.Next(files.Count)]), pack.Volumes.GetValueOrDefault(name, 1f));
            Played?.Invoke(name);
            return;
        }
    }

    // ---- music --------------------------------------------------------------------------------

    private void UpdateMusic()
    {
        lock (_musicLock)
        {
            if (Playlist() is not { } tracks)
            {
                _engine.StopMusic();
                return;
            }
            if (_engine.CurrentMusic is { } current && tracks.Contains(current)) return;
            PlayFrom(tracks);
        }
    }

    private void OnMusicEnded()
    {
        lock (_musicLock)
        {
            if (Playlist() is { } tracks) PlayFrom(tracks);
        }
    }

    private List<string>? Playlist()
    {
        if (!MusicEnabled || MusicPack is not { } pack) return null;
        var mood = MusicMood;
        string[] order = mood switch
        {
            "boss" => ["boss", _depth >= 6 ? "deep" : "dungeon", "dungeon"],
            "deep" => ["deep", "dungeon"],
            "title" => ["title", "town"],
            _ => [mood],
        };
        foreach (var m in order)
            if (pack.Music.TryGetValue(m, out var tracks) && tracks.Count > 0)
                return [.. tracks.Select(pack.Resolve)];
        return null;
    }

    private void PlayFrom(List<string> tracks)
    {
        var choices = tracks.Count > 1 ? tracks.Where(t => t != _lastTrack).ToList() : tracks;
        var next = choices[_random.Next(choices.Count)];
        _lastTrack = next;
        // Single-track moods (title, boss) loop; playlists move on to another track when one ends.
        _engine.PlayMusic(next, loop: tracks.Count == 1);
    }

    // ---- ambience -----------------------------------------------------------------------------

    private static readonly string[] AmbienceExtensions = [".ogg", ".wav", ".mp3"];

    /// <summary>Loop names for a depth, best first (the castle and the title have none).</summary>
    public static IReadOnlyList<string> AmbienceNames(int depth) => depth switch
    {
        <= 0 => [],
        >= 9 => ["ambient-dungeon-abyss", "ambient-dungeon-deep", "ambient-dungeon-shallow"],
        >= 5 => ["ambient-dungeon-deep", "ambient-dungeon-shallow"],
        _ => ["ambient-dungeon-shallow"],
    };

    public string? AmbienceFile()
    {
        if (_titleScreen || !_inGame) return null;
        foreach (var name in AmbienceNames(_depth))
            foreach (var folder in _ambienceFolders)
                foreach (var ext in AmbienceExtensions)
                    if (Path.Combine(folder, name + ext) is var path && File.Exists(path)) return path;
        return null;
    }

    private void UpdateAmbience()
    {
        if (!_ambienceEnabled || AmbienceFile() is not { } file) _engine.StopAmbience();
        else _engine.PlayAmbience(file);
    }

    public void Dispose()
    {
        _engine.MusicEnded -= OnMusicEnded;
        _engine.StopAmbience();
        _engine.StopMusic();
    }
}
