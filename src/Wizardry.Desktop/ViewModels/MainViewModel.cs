using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Wizardry.Core.Model;
using Wizardry.Core.Screens;
using Wizardry.Desktop.Services;

namespace Wizardry.Desktop.ViewModels;

/// <summary>Screen host: title → game, plus settings and help.</summary>
public sealed partial class MainViewModel : ViewModelBase
{
    public MainViewModel(AppServices services)
    {
        Services = services;
        _current = new TitleViewModel(services, this);
        services.Sound.TitleScreen = true;
    }

    public AppServices Services { get; }

    [ObservableProperty] private ViewModelBase _current;
    [ObservableProperty] private double _uiScale = 1;

    public event Action? ExitRequested;

    partial void OnCurrentChanged(ViewModelBase? oldValue, ViewModelBase newValue)
    {
        // A game set aside for settings or help lives on; anything else is finished with.
        if (oldValue is IDisposable d && !ReferenceEquals(oldValue, _suspended)) d.Dispose();
        Services.Sound.TitleScreen = newValue is not GameViewModel && _suspended is null;
    }

    private GameViewModel? _suspended;

    public void ShowTitle()
    {
        var old = _suspended;
        _suspended = null;
        Current = new TitleViewModel(Services, this);
        old?.Dispose();
    }

    /// <summary>Settings and help can be opened mid-game and return to it.</summary>
    public void ShowSettings()
    {
        if (Current is GameViewModel g) _suspended = g;
        Current = new SettingsViewModel(Services, this);
    }

    public void ShowHelp()
    {
        if (Current is GameViewModel g) _suspended = g;
        Current = new HelpViewModel(this);
    }

    /// <summary>Back from settings or help: to the game if one was running, else the title.</summary>
    public void Back()
    {
        if (_suspended is { } game)
        {
            Current = game;
            _suspended = null;
            game.Refresh();
        }
        else ShowTitle();
    }

    public void RequestExit() => ExitRequested?.Invoke();

    public void StartGame(World world)
    {
        var game = new Game(world);
        Current = new GameViewModel(Services, this, game);
    }

    /// <summary>Routes a key to the current screen.</summary>
    public bool HandleKey(Key key)
    {
        switch (Current)
        {
            case GameViewModel g:
                if (key == Key.F1) { ShowHelp(); return true; }
                if (key == Key.F2) { ShowSettings(); return true; }
                return g.HandleKey(key);
            case HelpViewModel when key == Key.Escape:
                Back();
                return true;
            case SettingsViewModel s when key == Key.Escape:
                s.CancelCommand.Execute(null);
                return true;
            case TitleViewModel t:
                return t.HandleKey(key);
            default:
                return false;
        }
    }
}
