using Wizardry.Core.Combat;
using Wizardry.Core.Content;
using Wizardry.Core.Maze;
using Wizardry.Core.Model;
using Wizardry.Core.Rules;
using Wizardry.Core.Screens;

namespace Wizardry.Core.Tests;

public sealed class ScreenTests
{
    private static Game NewGame(ulong seed = 1) => new(World.NewGame(seed));

    private static void Keys(Game g, params string[] keys)
    {
        foreach (var k in keys)
            Assert.True(g.Press(k), $"'{k}' not offered on {g.Current.Title}: {string.Join(", ", g.Current.Options.Select(o => $"{o.Key}{(o.Enabled ? "" : "(off)")}"))}");
    }

    [Fact]
    public void A_new_game_starts_in_the_castle()
    {
        var g = NewGame();
        Assert.Equal("CASTLE", g.Current.Title);
        Assert.Equal(Pane.Castle, g.Current.Pane);
        Assert.False(g.Press("Z"));
    }

    [Fact]
    public void A_character_is_made_at_the_training_grounds_and_joins_at_the_tavern()
    {
        var g = NewGame(5);
        Keys(g, "E", "T", "C");
        Assert.NotNull(g.Current.Prompt);
        Assert.True(g.Submit("Gwen"));
        Assert.Equal("CREATE A CHARACTER", g.Current.Title);
        Keys(g, "2", "N"); // elf, neutral
        // Spend every bonus point on I.Q., then agility, until none are left.
        while (g.Current.Lines.Any(l => l.Text.StartsWith("BONUS POINTS: ") && !l.Text.EndsWith(": 0")))
        {
            if (g.Current.Offers("2")) Keys(g, "2");
            else Keys(g, "5");
        }
        Keys(g, "ENTER");
        var mage = g.Current.Options.First(o => o.Label == "Mage");
        Keys(g, mage.Key, "Y");
        var gwen = Assert.Single(g.World.Roster);
        Assert.Equal("GWEN", gwen.Name);
        Assert.Equal(CharClass.Mage, gwen.Class);
        Keys(g, "ESC", "ESC", "T", "A", "1");
        Assert.Equal([gwen.Id], g.World.Party);
    }

    [Fact]
    public void Names_must_be_unique()
    {
        var g = NewGame();
        Recruits.AddQuickParty(g.World, g.Rng);
        Keys(g, "E", "T", "C");
        g.Submit("brand");
        Assert.Contains(g.Current.Lines, l => l.Text.Contains("already has that name"));
    }

    [Fact]
    public void Good_and_evil_characters_will_not_share_a_party()
    {
        var g = NewGame();
        var rng = new Rng(1);
        var good = Progression.Create("SAINT", Race.Human, Alignment.Good, CharClass.Fighter, [15, 8, 5, 8, 8, 9], rng);
        var evil = Progression.Create("SINNER", Race.Human, Alignment.Evil, CharClass.Fighter, [15, 8, 5, 8, 8, 9], rng);
        g.World.Roster.AddRange([good, evil]);
        g.World.Party.Add(good.Id);
        Assert.False(g.CanJoin(evil, out var why));
        Assert.Contains("Good and evil", why);
    }

    [Fact]
    public void The_party_walks_the_maze_and_bumps_into_walls()
    {
        var g = NewGame(3);
        Recruits.AddQuickParty(g.World, g.Rng);
        Keys(g, "E", "M");
        Assert.Equal(1, g.World.Depth);
        Assert.Equal(Pane.Maze, g.Current.Pane);
        var level = MazeBuilder.Get(1);
        // Face a wall and walk into it.
        var wall = Enum.GetValues<Dir>().First(d => level.EdgeAt(0, 0, d) == Edge.Wall);
        while (g.World.Facing != wall) Keys(g, "RIGHT");
        Keys(g, "UP");
        Assert.Equal((0, 0), (g.World.X, g.World.Y));
        Assert.Equal("OUCH!", g.World.Log.Last());
    }

    [Fact]
    public void Climbing_out_of_the_first_level_returns_to_the_castle()
    {
        var g = NewGame(3);
        Recruits.AddQuickParty(g.World, g.Rng);
        Keys(g, "E", "M");
        var level = MazeBuilder.Get(1);
        var open = Enum.GetValues<Dir>().First(d => level.CanPass(0, 0, d, false));
        while (g.World.Facing != open) Keys(g, "RIGHT");
        Keys(g, "UP");
        // Deal with anything that happened on the way (a fight or a message), then step back.
        while (g.Top is not MazeState) g.Press(g.Current.Options.First(o => o.Enabled).Key);
        if ((g.World.X, g.World.Y) != (0, 0))
        {
            Keys(g, "DOWN", "UP");
            while (g.Top is not StairsState && g.Top is not MazeState) g.Press(g.Current.Options.First(o => o.Enabled).Key);
        }
        if (g.Top is StairsState) Keys(g, "Y");
        Assert.Equal(0, g.World.Depth);
        Assert.Equal("CASTLE", g.Current.Title);
    }

    [Fact]
    public void Saved_spell_slots_are_spent_and_restored_at_the_inn()
    {
        var g = NewGame(4);
        Recruits.AddQuickParty(g.World, g.Rng);
        var priest = g.World.Members.First(c => c.Class == CharClass.Priest);
        priest.Hp = 1;
        priest.PriestSlots[0] = 0;
        priest.Gold = 1000;
        Keys(g, "I", "1");
        Keys(g, (g.World.Party.IndexOf(priest.Id) + 1).ToString(), "E", "ENTER");
        Assert.Equal(priest.MaxHp, priest.Hp);
        Assert.True(priest.PriestSlots[0] >= 1);
        Assert.True(priest.Gold < 1000);
    }

    [Fact]
    public void The_trading_post_sells_and_buys()
    {
        var g = NewGame(4);
        Recruits.AddQuickParty(g.World, g.Rng);
        var f = g.World.Members.First();
        f.Gold = 1000;
        Keys(g, "S", "C", "1", "B");
        while (!g.Current.Options.Any(o => o.Label == "Plate Mail")) Keys(g, "N");
        var plate = g.Current.Options.First(o => o.Label == "Plate Mail");
        Keys(g, plate.Key, "ENTER");
        Assert.Contains(f.Items, i => i.ItemId == 44);
        Assert.Equal(250, f.Gold);
        Keys(g, "ESC", "S", (f.Items.FindIndex(i => i.ItemId == 44) + 1).ToString(), "ENTER");
        Assert.DoesNotContain(f.Items, i => i.ItemId == 44);
        Assert.Equal(625, f.Gold);
    }

    [Fact]
    public void The_temple_raises_the_dead_for_a_tithe()
    {
        var g = NewGame(8);
        Recruits.AddQuickParty(g.World, g.Rng);
        var dead = g.World.Members.First();
        dead.Status = Status.Dead;
        dead.Hp = 0;
        dead.Stats[3] = 18;
        var payer = g.World.Members.Last();
        payer.Gold = 5000;
        Keys(g, "P", "H", "1");
        Keys(g, g.Current.Options.First(o => o.Label == payer.Name).Key, "ENTER");
        Assert.Equal(5000 - 250, payer.Gold);
        Assert.True(dead.Status is Status.Ok or Status.Ashes);
    }

    [Fact]
    public void Equipping_a_cursed_item_binds_it()
    {
        var g = NewGame(8);
        Recruits.AddQuickParty(g.World, g.Rng);
        var f = g.World.Members.First();
        f.Items.Add(new OwnedItem { ItemId = 15 }); // Long Sword -1, cursed
        Keys(g, "T", "I", "1", "E", f.Items.Count.ToString(), "ENTER");
        var cursed = f.Items.Last();
        Assert.True(cursed.Stuck);
        Assert.True(cursed.Identified);
        Assert.False(f.Items[0].Equipped); // the old sword came off
        // It won't come off until uncursed.
        Assert.False(g.Current.Offers(f.Items.Count.ToString()));
    }

    [Fact]
    public void Victory_honours_the_party_that_brings_the_amulet_home()
    {
        var g = NewGame(9);
        Recruits.AddQuickParty(g.World, g.Rng);
        Keys(g, "E", "M");
        g.World.Members.First().Items.Add(OwnedItem.Known(Items.AmuletId));
        g.ReturnToCastle("test");
        Assert.Equal("VICTORY", g.Current.Title);
        Assert.Equal(1, g.World.Victories);
        Assert.All(g.World.Members, c => Assert.Equal(1, c.Honors));
        Assert.False(g.World.PartyHas(Items.AmuletId));
    }

    [Fact]
    public void A_wiped_party_is_carried_home_by_a_patrol_or_left_where_it_fell()
    {
        foreach (bool patrols in new[] { true, false })
        {
            var g = new Game(World.NewGame(10, new GameOptions { RescuePatrols = patrols }));
            Recruits.AddQuickParty(g.World, g.Rng);
            Keys(g, "E", "M");
            foreach (var c in g.World.Members) { c.Status = Status.Dead; c.Hp = 0; }
            g.PartyWiped();
            Assert.Equal(0, g.World.Depth);
            Assert.Equal("ALL IS LOST", g.Current.Title);
            if (patrols) Assert.Equal(6, g.World.Party.Count);
            else
            {
                Assert.Empty(g.World.Party);
                Assert.All(g.World.Roster, c => Assert.Equal(new MazeSpot(1, 0, 0), c.LeftInMaze));
            }
        }
    }
}
