using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wizardry.Desktop.Services;
using Wizardry.Infrastructure.Settings;

namespace Wizardry.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly MainViewModel _main;
    private readonly GameSettings _saved;

    public SettingsViewModel(AppServices services, MainViewModel main)
    {
        _services = services;
        _main = main;
        _saved = services.Settings.Clone();
        var s = services.Settings;
        _uiScale = s.UiScale;
        _showAutomap = s.ShowAutomap;
        _rescuePatrols = s.RescuePatrols;
        _soundEnabled = s.SoundEnabled;
        _musicEnabled = s.MusicEnabled;
        _ambienceEnabled = s.AmbienceEnabled;
        _masterVolume = s.MasterVolume;
        _effectsVolume = s.EffectsVolume;
        _musicVolume = s.MusicVolume;
        _ambienceVolume = s.AmbienceVolume;
        EffectsPacks = services.SoundPacks.Where(p => p.HasSounds).Select(p => new Choice(p.Id, $"{p.Name} ({p.License})")).ToList();
        MusicPacks = services.SoundPacks.Where(p => p.HasMusic).Select(p => new Choice(p.Id, $"{p.Name} ({p.License})")).ToList();
        _effectsPack = EffectsPacks.FirstOrDefault(p => p.Id == s.EffectsPack) ?? EffectsPacks.FirstOrDefault();
        _musicPack = MusicPacks.FirstOrDefault(p => p.Id == s.MusicPack) ?? MusicPacks.FirstOrDefault();
        BufferChoices = Wizardry.Audio.AudioBuffer.Choices.Select(c => new Choice(c.Id, c.Label)).ToList();
        _audioBuffer = BufferChoices.FirstOrDefault(c => c.Id == s.AudioBuffer) ?? BufferChoices[0];
        AudioStatus = services.AudioProblem ?? "Sound is working.";
    }

    public sealed record Choice(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    public IReadOnlyList<Choice> EffectsPacks { get; }
    public IReadOnlyList<Choice> MusicPacks { get; }
    public IReadOnlyList<Choice> BufferChoices { get; }
    public string AudioStatus { get; }

    [ObservableProperty] private double _uiScale;
    [ObservableProperty] private bool _showAutomap;
    [ObservableProperty] private bool _rescuePatrols;
    [ObservableProperty] private bool _soundEnabled;
    [ObservableProperty] private bool _musicEnabled;
    [ObservableProperty] private bool _ambienceEnabled;
    [ObservableProperty] private double _masterVolume;
    [ObservableProperty] private double _effectsVolume;
    [ObservableProperty] private double _musicVolume;
    [ObservableProperty] private double _ambienceVolume;
    [ObservableProperty] private Choice? _effectsPack;
    [ObservableProperty] private Choice? _musicPack;
    [ObservableProperty] private Choice? _audioBuffer;

    // Sound changes are heard at once; Cancel puts them back.
    partial void OnSoundEnabledChanged(bool value) => Preview();
    partial void OnMusicEnabledChanged(bool value) => Preview();
    partial void OnAmbienceEnabledChanged(bool value) => Preview();
    partial void OnMasterVolumeChanged(double value) => Preview();
    partial void OnEffectsVolumeChanged(double value) => Preview();
    partial void OnMusicVolumeChanged(double value) => Preview();
    partial void OnAmbienceVolumeChanged(double value) => Preview();
    partial void OnEffectsPackChanged(Choice? value) => Preview();
    partial void OnMusicPackChanged(Choice? value) => Preview();
    partial void OnUiScaleChanged(double value) => _main.UiScale = value;

    private void Apply(GameSettings s)
    {
        s.UiScale = Math.Round(UiScale, 2);
        s.ShowAutomap = ShowAutomap;
        s.RescuePatrols = RescuePatrols;
        s.SoundEnabled = SoundEnabled;
        s.MusicEnabled = MusicEnabled;
        s.AmbienceEnabled = AmbienceEnabled;
        s.MasterVolume = MasterVolume;
        s.EffectsVolume = EffectsVolume;
        s.MusicVolume = MusicVolume;
        s.AmbienceVolume = AmbienceVolume;
        if (EffectsPack is { } e) s.EffectsPack = e.Id;
        if (MusicPack is { } m) s.MusicPack = m.Id;
        if (AudioBuffer is { } b) s.AudioBuffer = b.Id;
    }

    private void Preview()
    {
        if (_services is null) return;
        Apply(_services.Settings);
        _services.ApplyAudioSettings();
    }

    [RelayCommand]
    private void Save()
    {
        Apply(_services.Settings);
        _services.ApplyAudioSettings();
        _main.Back();
        _ = _services.SaveSettingsAsync();
    }

    [RelayCommand]
    private void Cancel()
    {
        _services.Settings = _saved;
        _services.ApplyAudioSettings();
        _main.UiScale = _saved.UiScale;
        _main.Back();
    }
}
