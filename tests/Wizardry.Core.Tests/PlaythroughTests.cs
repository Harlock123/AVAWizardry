using Wizardry.Core.Model;
using Wizardry.Core.Screens;

namespace Wizardry.Core.Tests;

public sealed class PlaythroughTests(ITestOutputHelper output)
{
    /// <summary>A bot with the ready-made party, pressing keys through every screen, beats the game.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    public void A_bot_can_win_the_game(ulong seed)
    {
        var world = World.NewGame(seed);
        var game = new Game(world);
        Recruits.AddQuickParty(world, game.Rng);
        var bot = new Bot(game);
        try
        {
            bot.Run(maxPresses: 3_000_000);
        }
        finally
        {
            output.WriteLine($"presses {bot.Presses}, fights {bot.Fights}, trips {bot.Trips}, wipes {bot.Wipes}, deepest {bot.MaxDepth}, week {world.Week}");
            foreach (var c in world.Members)
                output.WriteLine($"  {c.Name,-12} {c.ClassTag} L{c.Level} HP {c.Hp}/{c.MaxHp} AC {c.ArmorClass()} age {c.AgeYears} gold {c.Gold} {c.Status} items {string.Join(",", c.Items.Select(i => i.DisplayName))}");
            output.WriteLine($"warden {world.WardenDefeated}, morthrain {world.MorthrainDefeated}, victories {world.Victories}");
            foreach (var t in bot.Trace.TakeLast(40)) output.WriteLine("  > " + t);
            foreach (var l in world.Log.TakeLast(30)) output.WriteLine("  | " + l);
        }
        Assert.True(bot.Won, "The bot did not win.");
    }
}
