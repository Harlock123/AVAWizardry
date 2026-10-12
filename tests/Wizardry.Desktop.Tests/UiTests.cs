using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Wizardry.Core.Combat;
using Wizardry.Core.Content;
using Wizardry.Core.Maze;
using Wizardry.Core.Model;
using Wizardry.Core.Rules;
using Wizardry.Core.Screens;
using Wizardry.Desktop.Services;
using Wizardry.Desktop.ViewModels;
using Wizardry.Desktop.Views;

namespace Wizardry.Desktop.Tests;

/// <summary>
/// The real window, driven headlessly by key presses. Set WIZ_SCREENSHOT_DIR to save a PNG of
/// each step (the README's screenshots come from here).
/// </summary>
public sealed class UiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wiz-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private (MainWindow Window, MainViewModel Vm, AppServices Services) Open()
    {
        var services = AppServices.CreateIsolated(_root);
        var vm = new MainViewModel(services);
        var window = new MainWindow { DataContext = vm, Width = 1180, Height = 780 };
        window.Show();
        Pump();
        return (window, vm, services);
    }

    private static void Pump()
    {
        for (int i = 0; i < 20; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Press(MainWindow w, Key key)
    {
        w.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        w.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        Pump();
    }

    private static void Capture(MainWindow w, string name)
    {
        var dir = Environment.GetEnvironmentVariable("WIZ_SCREENSHOT_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        Pump();
        w.CaptureRenderedFrame()?.Save(Path.Combine(dir, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>A game with a ready-made party, started from the title screen's R key.</summary>
    private (MainWindow W, MainViewModel Vm, GameViewModel Game) QuickStart()
    {
        var (w, vm, _) = Open();
        Press(w, Key.R);
        var game = Assert.IsType<GameViewModel>(vm.Current);
        return (w, vm, game);
    }

    [AvaloniaFact]
    public void The_title_screen_offers_a_quick_start_and_help()
    {
        var (w, vm, _) = Open();
        var title = Assert.IsType<TitleViewModel>(vm.Current);
        Assert.False(title.CanContinue);
        Capture(w, "01-title");
        Press(w, Key.H);
        var help = Assert.IsType<HelpViewModel>(vm.Current);
        Assert.Contains(help.Pages, p => p.Title == "Spells");
        Assert.Contains(help.Pages, p => p.Title == "What's new");
        Assert.True(help.Pages.Count >= 8);
        help.Selected = help.Pages.First(p => p.Title == "The maze");
        Capture(w, "12-help");
        Press(w, Key.Escape);
        Assert.IsType<TitleViewModel>(vm.Current);
        w.Close();
    }

    [AvaloniaFact]
    public void A_quick_start_puts_six_adventurers_in_the_castle_and_saves()
    {
        var (w, vm, game) = QuickStart();
        Assert.Equal(6, game.Roster.Count);
        Assert.Equal("CASTLE", game.Title);
        Capture(w, "02-castle");
        Assert.True(vm.Services.Saves.Exists);
        // The tavern and a character sheet.
        Press(w, Key.T);
        Press(w, Key.I);
        Press(w, Key.D6);
        Assert.Equal("CHARACTER", game.Title);
        Capture(w, "04-character");
        Press(w, Key.R);
        Assert.Equal("SPELLBOOK", game.Title);
        Press(w, Key.Enter);
        Press(w, Key.Escape);
        Press(w, Key.Escape);
        // The trading post.
        Press(w, Key.S);
        Press(w, Key.C);
        Press(w, Key.D1);
        Press(w, Key.B);
        Assert.Equal("BUY", game.Title);
        Capture(w, "05-shop");
        w.Close();
    }

    [AvaloniaFact]
    public void Characters_are_made_at_the_training_grounds_by_keyboard()
    {
        var (w, vm, _) = Open();
        Press(w, Key.N);
        var game = Assert.IsType<GameViewModel>(vm.Current);
        Press(w, Key.E);
        Press(w, Key.T);
        Press(w, Key.C);
        Assert.True(game.HasPrompt);
        game.InputText = "Rowan";
        Press(w, Key.Enter);
        Press(w, Key.D3); // dwarf
        Press(w, Key.G);  // good
        Capture(w, "03-training");
        // Spend the points on strength until a fighter is possible and none are left.
        for (int i = 0; i < 40 && game.Lines.Any(l => l.Text.StartsWith("BONUS POINTS: ") && !l.Text.EndsWith(": 0")); i++)
            Press(w, game.Game.Current.Offers("1") ? Key.D1 : Key.D4);
        Press(w, Key.Enter);
        var fighter = game.Options.First(o => o.Label == "Fighter");
        Press(w, Enum.Parse<Key>("D" + fighter.Key));
        Press(w, Key.Y);
        var rowan = Assert.Single(game.Game.World.Roster);
        Assert.Equal("ROWAN", rowan.Name);
        Assert.Equal(CharClass.Fighter, rowan.Class);
        w.Close();
    }

    [AvaloniaFact]
    public void The_maze_is_walked_with_the_arrow_keys_and_wasd()
    {
        var (w, _, game) = QuickStart();
        Press(w, Key.E);
        Press(w, Key.M);
        Assert.True(game.IsMaze);
        Assert.Equal(1, game.Game.World.Depth);
        var start = (game.Game.World.X, game.Game.World.Y, game.Game.World.Facing);
        Press(w, Key.D);
        Assert.Equal(start.Facing.Right(), game.Game.World.Facing);
        Press(w, Key.Left);
        Assert.Equal(start.Facing, game.Game.World.Facing);
        WalkSomewhere(w, game, steps: 3);
        Assert.True(game.Game.World.IsExplored(1, game.Game.World.X, game.Game.World.Y));
        // For the screenshots: a well-explored second level, from a spot with a long view and doors in it.
        var (x, y, facing) = BestView(MazeBuilder.Get(2));
        var world = game.Game.World;
        world.Depth = 2;
        (world.X, world.Y, world.Facing) = (x, y, facing);
        var level = MazeBuilder.Get(2);
        foreach (var (ex, ey) in level.Reachable(x, y, false).Where(p => Math.Abs(p.X - x) + Math.Abs(p.Y - y) <= 9))
            world.MarkExplored(2, ex, ey);
        game.Refresh();
        Capture(w, "06-maze");
        world.LongLight = true;
        game.Game.ShowLocation = true;
        game.Refresh();
        Capture(w, "07-maze-lit");
        w.Close();
    }

    /// <summary>The square and direction with the longest corridor ahead that shows a door or two.</summary>
    private static (int X, int Y, Dir Facing) BestView(Level level)
    {
        var best = (0, 0, Dir.North);
        int bestScore = -1;
        for (int x = 0; x < Level.Size; x++)
            for (int y = 0; y < Level.Size; y++)
                foreach (var d in Enum.GetValues<Dir>())
                {
                    if (level.FeatureAt(x, y) != Feature.None) continue;
                    int score = 0, cx = x, cy = y;
                    for (int i = 0; i < 4 && Level.InBounds(cx, cy); i++)
                    {
                        if (level.FeatureAt(cx, cy) == Feature.Dark) break;
                        foreach (var side in new[] { d.Left(), d.Right() })
                        {
                            var e = level.EdgeAt(cx, cy, side);
                            score += e == Edge.Door ? 3 : e == Edge.Open ? 2 : 0;
                        }
                        var front = level.EdgeAt(cx, cy, d);
                        if (front == Edge.Door) { score += 4; break; }
                        if (front != Edge.Open) break;
                        score += 2;
                        cx += d.Dx();
                        cy += d.Dy();
                    }
                    if (score > bestScore) { bestScore = score; best = (x, y, d); }
                }
        return best;
    }

    private static void WalkSomewhere(MainWindow w, GameViewModel game, int steps)
    {
        var level = game.Game.CurrentLevel!;
        for (int i = 0; i < steps * 4; i++)
        {
            while (game.Game.Top is not MazeState)
                game.Press(game.Game.Current.Options.First(o => o.Enabled && o.Key is "ENTER" or "N" or "ESC" or "R").Key);
            var world = game.Game.World;
            if (level.CanPass(world.X, world.Y, world.Facing, false)) Press(w, Key.Up);
            else Press(w, Key.Right);
        }
        while (game.Game.Top is not MazeState)
            game.Press(game.Game.Current.Options.First(o => o.Enabled).Key);
    }

    [AvaloniaFact]
    public void A_fight_shows_the_foe_and_takes_orders()
    {
        var (w, _, game) = QuickStart();
        Press(w, Key.E);
        Press(w, Key.M);
        var g = game.Game;
        g.BeginBattle([FoeGroup.Spawn(Monsters.Get(31), 2, g.Rng, identified: true), FoeGroup.Spawn(Monsters.Get(3), 4, g.Rng, identified: false)],
            lair: false, chest: true, roomKey: null);
        game.Refresh();
        Assert.Equal(Pane.Combat, game.Pane);
        Capture(w, "08-combat");
        // Everyone parries, round after round, until the fight ends one way or another.
        for (int i = 0; i < 400 && g.Top is CombatState; i++)
            Press(w, g.Current.Offers("F") ? Key.F : g.Current.Offers("P") ? Key.P : Key.Enter);
        Assert.IsNotType<CombatState>(g.Top);
        w.Close();
    }

    [AvaloniaFact]
    public void Morthrain_and_the_chest_and_the_victory_are_drawn()
    {
        var (w, _, game) = QuickStart();
        Press(w, Key.E);
        Press(w, Key.M);
        var g = game.Game;
        g.BeginBattle([FoeGroup.Spawn(Monsters.Get(Monsters.Morthrain), 1, g.Rng, identified: true), FoeGroup.Spawn(Monsters.Get(47), 1, g.Rng, identified: true)],
            lair: true, chest: true, roomKey: null);
        game.Refresh();
        Capture(w, "09-morthrain");
        g.Reset(new MazeState(g));
        g.Chest = Chest.Roll(5, g.Rng, lair: false);
        g.Push(new ChestState(g, g.Chest));
        game.Refresh();
        Assert.Equal(Pane.Chest, game.Pane);
        Capture(w, "10-chest");
        g.Reset(new MazeState(g));
        g.World.Members.First().Items.Add(OwnedItem.Known(Items.AmuletId));
        g.ReturnToCastle("home");
        game.Refresh();
        Assert.Equal("VICTORY", game.Title);
        Capture(w, "11-victory");
        w.Close();
    }

    [AvaloniaFact]
    public void Settings_open_mid_game_and_return_to_it()
    {
        var (w, vm, game) = QuickStart();
        Press(w, Key.F2);
        var settings = Assert.IsType<SettingsViewModel>(vm.Current);
        Assert.Contains(settings.EffectsPacks, p => p.Id == "avawiz-retro");
        Assert.Contains(settings.MusicPacks, p => p.Id == "avawiz-music");
        settings.ShowAutomap = false;
        settings.SaveCommand.Execute(null);
        Pump();
        Assert.Same(game, vm.Current);
        Assert.False(vm.Services.Settings.ShowAutomap);
        w.Close();
    }

    [AvaloniaFact]
    public void Every_sound_cue_has_an_effect_in_the_default_pack()
    {
        var (w, _, services) = Open();
        var pack = services.SoundPacks.First(p => p.Id == "avawiz-retro");
        foreach (var cue in Enum.GetValues<SoundCue>())
            Assert.Contains(Wizardry.Audio.SoundDirector.EventNames(cue), name => pack.Sounds.ContainsKey(name));
        var music = services.SoundPacks.First(p => p.Id == "avawiz-music");
        foreach (var mood in new[] { "title", "town", "dungeon", "deep", "boss" })
            Assert.True(music.Music.ContainsKey(mood), mood);
        Assert.All(music.ReferencedFiles(), f => Assert.True(File.Exists(music.Resolve(f)), f));
        w.Close();
    }

    [AvaloniaFact]
    public void A_saved_game_continues_where_it_left_off()
    {
        var (w, vm, game) = QuickStart();
        Press(w, Key.E);
        Press(w, Key.M);
        Press(w, Key.C); // camp saves
        Press(w, Key.Q); // save and quit
        var title = Assert.IsType<TitleViewModel>(vm.Current);
        Assert.True(title.CanContinue);
        Press(w, Key.C);
        var again = Assert.IsType<GameViewModel>(vm.Current);
        Assert.Equal(1, again.Game.World.Depth);
        Assert.True(again.IsMaze);
        w.Close();
    }
}
