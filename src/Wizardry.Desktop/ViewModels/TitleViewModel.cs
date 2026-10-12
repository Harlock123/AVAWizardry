using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wizardry.Core.Model;
using Wizardry.Core.Rules;
using Wizardry.Desktop.Services;
using Wizardry.Infrastructure.Saves;

namespace Wizardry.Desktop.ViewModels;

/// <summary>Continue, start anew (with or without a ready-made party), settings, help, quit.</summary>
public sealed partial class TitleViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly MainViewModel _main;

    public TitleViewModel(AppServices services, MainViewModel main)
    {
        _services = services;
        _main = main;
        var summary = services.Saves.Summary();
        CanContinue = summary is not null;
        SaveSummary = summary is null
            ? (services.Saves.Exists ? "The saved game can't be read (see the message below)." : "No saved game yet.")
            : $"Saved {summary.SavedAt.ToLocalTime():d MMM yyyy, HH:mm}: {summary.RosterCount} on the roster, "
              + (summary.Depth > 0 ? $"the party on maze level {summary.Depth}" : "the party in the castle")
              + (summary.Victories > 0 ? $", {summary.Victories} victor{(summary.Victories == 1 ? "y" : "ies")}." : ".");
        if (services.Saves.Exists && summary is null)
        {
            try { services.Saves.Load(); }
            catch (SaveLoadException e) { Problem = e.Message; }
        }
        Problem ??= services.StartupProblem;
        AudioNote = services.AudioProblem;
    }

    public bool CanContinue { get; }
    public string SaveSummary { get; }
    public string? AudioNote { get; }
    [ObservableProperty] private string? _problem;

    /// <summary>Starting anew over a saved game asks first.</summary>
    [ObservableProperty] private bool _confirmNew;
    private bool _pendingQuick;

    [RelayCommand]
    private void Continue()
    {
        if (!CanContinue) return;
        try { _main.StartGame(_services.Saves.Load()); }
        catch (SaveLoadException e) { Problem = e.Message; }
    }

    [RelayCommand] private void NewGame() => Begin(quick: false);
    [RelayCommand] private void QuickStart() => Begin(quick: true);

    private void Begin(bool quick, bool confirmed = false)
    {
        if (CanContinue && !confirmed)
        {
            _pendingQuick = quick;
            ConfirmNew = true;
            return;
        }
        ConfirmNew = false;
        var seed = (ulong)Random.Shared.NextInt64(1, long.MaxValue);
        var world = World.NewGame(seed, new GameOptions { RescuePatrols = _services.Settings.RescuePatrols });
        if (quick) Recruits.AddQuickParty(world, new Rng(seed ^ 0x5A5A5A5A));
        _services.Saves.Save(world);
        _main.StartGame(world);
    }

    [RelayCommand] private void ConfirmYes() => Begin(_pendingQuick, confirmed: true);
    [RelayCommand] private void ConfirmNo() => ConfirmNew = false;
    [RelayCommand] private void Settings() => _main.ShowSettings();
    [RelayCommand] private void Help() => _main.ShowHelp();
    [RelayCommand] private void Quit() => _main.RequestExit();

    public bool HandleKey(Key key)
    {
        switch (key)
        {
            case Key.C when CanContinue: Continue(); return true;
            case Key.N: NewGame(); return true;
            case Key.R: QuickStart(); return true;
            case Key.S: Settings(); return true;
            case Key.H or Key.F1: Help(); return true;
            case Key.Q: Quit(); return true;
            case Key.Y when ConfirmNew: ConfirmYes(); return true;
            case Key.Escape when ConfirmNew: ConfirmNo(); return true;
            case Key.Enter when CanContinue && !ConfirmNew: Continue(); return true;
            default: return false;
        }
    }
}
