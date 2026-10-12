using Wizardry.Core.Model;
using Wizardry.Core.Screens;
using Wizardry.Infrastructure.Saves;

namespace Wizardry.Core.Tests;

public sealed class SaveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wiz-save-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void A_world_saves_and_loads_unchanged()
    {
        var world = World.NewGame(77);
        var g = new Game(world);
        Recruits.AddQuickParty(world, g.Rng);
        g.Press("E");
        g.Press("M");
        world.Members.First().Items.Add(new OwnedItem { ItemId = 26 });
        world.Members.First().Spells.Add("NOSA");
        world.ClearedRooms.Add("1:2");
        var store = new SaveStore(_dir);
        store.Save(world);
        var back = store.Load();
        Assert.Equal(world.Roster.Count, back.Roster.Count);
        Assert.Equal(world.Party, back.Party);
        Assert.Equal((world.Depth, world.X, world.Y, world.Facing), (back.Depth, back.X, back.Y, back.Facing));
        Assert.Equal(world.RngState, back.RngState);
        Assert.Equal(world.Explored, back.Explored);
        Assert.Contains("1:2", back.ClearedRooms);
        var a = world.Members.First();
        var b = back.Members.First();
        Assert.Equal(a.Stats, b.Stats);
        Assert.Equal(a.Items.Select(i => (i.ItemId, i.Equipped, i.Identified)), b.Items.Select(i => (i.ItemId, i.Equipped, i.Identified)));
        Assert.Equal(a.Spells.OrderBy(s => s), b.Spells.OrderBy(s => s));
        Assert.Equal(a.MageSlots, b.MageSlots);
        // The loaded world resumes in the maze.
        var resumed = new Game(back);
        Assert.Equal(Pane.Maze, resumed.Current.Pane);
    }

    [Fact]
    public void A_tampered_save_is_refused_and_the_backup_kept()
    {
        var store = new SaveStore(_dir);
        var world = World.NewGame(1);
        store.Save(world);
        store.Save(world);
        Assert.True(File.Exists(store.BackupPath));
        File.WriteAllText(store.MainPath, File.ReadAllText(store.MainPath).Replace("\"Week\": 0", "\"Week\": 9"));
        var e = Assert.Throws<SaveLoadException>(() => store.Load());
        Assert.Contains("checksum", e.Message);
        Assert.Null(store.Summary());
    }

    [Fact]
    public void A_missing_save_says_so()
    {
        var store = new SaveStore(_dir);
        Assert.False(store.Exists);
        Assert.Throws<SaveLoadException>(() => store.Load());
    }
}
