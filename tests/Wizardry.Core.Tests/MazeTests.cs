using Wizardry.Core.Maze;

namespace Wizardry.Core.Tests;

public sealed class MazeTests(ITestOutputHelper output)
{
    [Fact]
    public void There_are_ten_levels_and_each_starts_where_the_stairs_above_come_down()
    {
        var levels = MazeBuilder.Levels;
        Assert.Equal(10, levels.Count);
        Assert.Equal((0, 0), levels[0].StairsUp);
        for (int d = 0; d < 9; d++)
            Assert.Equal(levels[d].StairsDown, levels[d + 1].StairsUp);
        Assert.Null(levels[9].StairsDown);
        Assert.NotNull(levels[9].LairCenter);
        Assert.NotNull(levels[3].LairCenter);
    }

    [Fact]
    public void Every_square_is_reachable_with_the_ribbon()
    {
        foreach (var level in MazeBuilder.Levels)
        {
            var reach = level.Reachable(level.StairsUp.X, level.StairsUp.Y, hasRibbon: true);
            Assert.Equal(Level.Size * Level.Size, reach.Count);
        }
    }

    [Fact]
    public void Without_the_ribbon_the_fourth_level_holds_the_warden_but_not_the_way_down()
    {
        var level = MazeBuilder.Get(4);
        var reach = level.Reachable(level.StairsUp.X, level.StairsUp.Y, hasRibbon: false);
        output.WriteLine($"up {level.StairsUp} down {level.StairsDown} lair {level.LairCenter} reach {reach.Count}");
        Assert.Contains(level.LairCenter!.Value, reach);
        Assert.DoesNotContain(level.StairsDown!.Value, reach);
        Assert.Equal(Feature.Lair, level.FeatureAt(level.LairCenter.Value.X, level.LairCenter.Value.Y));
    }

    [Fact]
    public void Lifts_serve_levels_one_to_four_and_four_to_nine()
    {
        for (int d = 1; d <= 9; d++)
        {
            var level = MazeBuilder.Get(d);
            Assert.Contains(level.Lifts, l => d >= l.Low && d <= l.High);
            foreach (var lift in level.Lifts)
                Assert.Equal(Feature.Lift, level.FeatureAt(lift.X, lift.Y));
        }
        Assert.Empty(MazeBuilder.Get(10).Lifts);
        Assert.Equal(2, MazeBuilder.Get(4).Lifts.Count);
        Assert.True(MazeBuilder.Get(9).Lifts.Single().NeedsRibbon);
    }

    [Fact]
    public void Every_level_has_rooms_and_the_maze_never_changes()
    {
        foreach (var level in MazeBuilder.Levels)
            Assert.True(level.RoomCount >= 2, $"level {level.Depth} has {level.RoomCount} rooms");
        var again = MazeBuilder.Build(3, MazeBuilder.Get(3).StairsUp);
        var first = MazeBuilder.Get(3);
        for (int x = 0; x < Level.Size; x++)
            for (int y = 0; y < Level.Size; y++)
            {
                // Lifts are added once every level exists.
                if (first.FeatureAt(x, y) != Feature.Lift) Assert.Equal(first.FeatureAt(x, y), again.FeatureAt(x, y));
                foreach (var d in Enum.GetValues<Dir>())
                    Assert.Equal(first.EdgeAt(x, y, d), again.EdgeAt(x, y, d));
            }
    }
}
