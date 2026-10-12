using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wizardry.Core.Model;
using Wizardry.Core.Rules;
using Wizardry.Core.Screens;
using Wizardry.Desktop.Input;
using Wizardry.Desktop.Services;
using Wizardry.Infrastructure.Saves;

namespace Wizardry.Desktop.ViewModels;

public sealed record LineView(string Text, IBrush Brush);

public sealed partial class OptionView(string key, string label, bool enabled, Action<string> press) : ObservableObject
{
    public string Key => key;
    public string KeyLabel => KeyMap.Label(key);
    public string Label => label;
    public bool Enabled => enabled;
    [RelayCommand] private void Choose() => press(key);
}

/// <summary>One row of the party table along the bottom.</summary>
public sealed record RosterRow(int Index, string Name, string Class, string Ac, string Hits, string Status, IBrush Brush);

/// <summary>A game in progress: draws the core's current screen and passes keys back to it.</summary>
public sealed partial class GameViewModel : ViewModelBase, IDisposable
{
    private readonly AppServices _services;
    private readonly MainViewModel _main;

    public GameViewModel(AppServices services, MainViewModel main, Game game)
    {
        _services = services;
        _main = main;
        Game = game;
        game.Sound += services.Sound.Play;
        game.SaveRequested += Save;
        game.QuitRequested += Quit;
        Refresh();
    }

    public Game Game { get; }

    [ObservableProperty] private int _version;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private IReadOnlyList<LineView> _lines = [];
    [ObservableProperty] private IReadOnlyList<OptionView> _options = [];
    [ObservableProperty] private string? _prompt;
    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private Pane _pane;
    [ObservableProperty] private string _place = "castle";
    [ObservableProperty] private bool _isMaze;
    [ObservableProperty] private bool _showAutomap;
    [ObservableProperty] private IReadOnlyList<RosterRow> _roster = [];
    [ObservableProperty] private string _footer = "";
    [ObservableProperty] private string? _problem;

    public bool HasPrompt => Prompt is not null;

    partial void OnPromptChanged(string? value) => OnPropertyChanged(nameof(HasPrompt));

    public void Refresh()
    {
        var screen = Game.Current;
        Title = screen.Title;
        Lines = screen.Lines.Select(l => new LineView(l.Text, BrushFor(l.Tone))).ToList();
        Options = screen.Options.Select(o => new OptionView(o.Key, o.Label, o.Enabled, Press)).ToList();
        if (Prompt != screen.Prompt) InputText = "";
        Prompt = screen.Prompt;
        Pane = screen.Pane;
        Place = screen.Place;
        IsMaze = screen.Pane == Pane.Maze;
        ShowAutomap = IsMaze && _services.Settings.ShowAutomap;
        Roster = Game.Party.Select((c, i) => new RosterRow(
            i + 1,
            c.Name + (c.Honors > 0 ? " >" : ""),
            c.ClassTag,
            c.ArmorClass(Game.World.TripWard).ToString(),
            $"{c.Hp}/{c.MaxHp}",
            c.Poisoned && c.IsAlive ? "POISON" : c.Status.Label(),
            c.Status switch
            {
                Status.Ok when c.Poisoned => Brush("BadBrush"),
                Status.Ok => Brush("Ink"),
                Status.Asleep or Status.Afraid => Brush("InfoBrush"),
                Status.Paralyzed or Status.Stoned => Brush("BadBrush"),
                _ => Brush("DangerBrush"),
            })).ToList();
        var w = Game.World;
        Footer = w.InMaze ? $"LEVEL {w.Depth}" : w.Victories > 0 ? $"CASTLE   CHAMPIONS {w.Victories}" : "CASTLE";
        Version++;
        // Music follows the party.
        _services.Sound.SetLocation(w.InMaze ? w.Depth : 0);
        _services.Sound.SetBoss(Game.Battle?.IsLair == true);
    }

    public void Press(string key)
    {
        if (Game.Press(key)) Refresh();
    }

    [RelayCommand]
    private void Submit()
    {
        if (Prompt is null) return;
        string text = InputText;
        InputText = "";
        Game.Submit(text);
        Refresh();
    }

    /// <summary>Routes a key to the game. Typing into a prompt is left to the text box.</summary>
    public bool HandleKey(Key key)
    {
        if (KeyMap.Name(key) is not { } name) return false;
        var screen = Game.Current;
        if (screen.Prompt is not null)
        {
            if (name == "ENTER") { Submit(); return true; }
            if (name == "ESC" && key == Key.Escape) { Press("ESC"); return true; }
            return false;
        }
        if (screen.Offers(name)) { Press(name); return true; }
        if (screen.Offers("UP") && KeyMap.MazeAlias(name) is { } alias && screen.Offers(alias)) { Press(alias); return true; }
        // ENTER also picks the only choice on a screen, and ESC backs out of a message.
        if (name == "ENTER" && screen.Options.Count(o => o.Enabled) == 1) { Press(screen.Options.First(o => o.Enabled).Key); return true; }
        if (name == "ESC" && screen.Offers("ENTER") && screen.Options.Count == 1) { Press("ENTER"); return true; }
        return false;
    }

    private void Save()
    {
        try
        {
            _services.Saves.Save(Game.World);
            Problem = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SaveLoadException)
        {
            Problem = "The game could not be saved: " + e.Message;
        }
    }

    private void Quit() => _main.ShowTitle();

    private static IBrush BrushFor(Tone tone) => tone switch
    {
        Tone.Dim => Brush("InkDim"),
        Tone.Title => Brush("Accent"),
        Tone.Good => Brush("GoodBrush"),
        Tone.Bad => Brush("DangerBrush"),
        Tone.Gold => Brush("Gold"),
        Tone.Info => Brush("InfoBrush"),
        _ => Brush("Ink"),
    };

    private static IBrush Brush(string key) =>
        Avalonia.Application.Current?.TryGetResource(key, null, out var v) == true && v is IBrush b ? b : Brushes.White;

    public void Dispose()
    {
        Game.Sound -= _services.Sound.Play;
        Game.SaveRequested -= Save;
        Game.QuitRequested -= Quit;
    }
}
