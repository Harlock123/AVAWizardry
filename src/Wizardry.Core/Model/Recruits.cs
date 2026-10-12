using Wizardry.Core.Content;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Model;

/// <summary>A ready-made starting party, for players who'd rather begin in the maze than the training grounds.</summary>
public static class Recruits
{
    private sealed record Plan(string Name, Race Race, Alignment Align, CharClass Class, int[] Gear);

    private static readonly Plan[] Plans =
    [
        new("BRAND", Race.Dwarf, Alignment.Good, CharClass.Fighter, [1, 41, 60]),
        new("KESTREL", Race.Human, Alignment.Neutral, CharClass.Fighter, [1, 41, 60]),
        new("MORWEN", Race.Dwarf, Alignment.Good, CharClass.Priest, [3, 41, 60]),
        new("WREN", Race.Hobbit, Alignment.Neutral, CharClass.Thief, [2, 41]),
        new("TAMSIN", Race.Elf, Alignment.Neutral, CharClass.Mage, [6, 40]),
        new("OSRIC", Race.Gnome, Alignment.Good, CharClass.Bishop, [3, 40]),
    ];

    /// <summary>
    /// Six characters with their bonus points spent on what their class needs most, each armed and
    /// armoured from their own purse. They join the roster and the party.
    /// </summary>
    public static IReadOnlyList<Character> AddQuickParty(World world, Rng rng)
    {
        var made = new List<Character>();
        foreach (var plan in Plans)
        {
            string name = plan.Name;
            for (int n = 2; world.Roster.Any(c => c.Name == name); n++) name = $"{plan.Name} {n}";
            var stats = RuleTables.BaseStats(plan.Race);
            int bonus = Math.Max(Progression.RollBonus(rng), 9);
            var need = RuleTables.Requirements(plan.Class);
            // First meet the class's requirements, then favour its key attributes.
            for (int i = 0; i < 6 && bonus > 0; i++)
                while (stats[i] < need[i] && bonus > 0) { stats[i]++; bonus--; }
            Stat[] favour = plan.Class switch
            {
                CharClass.Fighter => [Stat.Str, Stat.Vit, Stat.Agi],
                CharClass.Priest => [Stat.Pie, Stat.Vit, Stat.Agi],
                CharClass.Thief => [Stat.Agi, Stat.Vit, Stat.Luc],
                CharClass.Mage => [Stat.Iq, Stat.Agi, Stat.Vit],
                _ => [Stat.Iq, Stat.Pie, Stat.Vit],
            };
            for (int k = 0; bonus > 0 && k < 60; k++)
            {
                int i = (int)favour[k % favour.Length];
                if (stats[i] < RuleTables.MaxStat) { stats[i]++; bonus--; }
            }
            var c = Progression.Create(name, plan.Race, plan.Align, plan.Class, stats, rng);
            foreach (var id in plan.Gear)
            {
                var def = Items.Get(id);
                c.Gold = Math.Max(0, c.Gold - def.Price);
                c.Items.Add(new OwnedItem { ItemId = id, Identified = true, Equipped = true });
            }
            world.Roster.Add(c);
            if (world.Party.Count < World.MaxParty) world.Party.Add(c.Id);
            made.Add(c);
        }
        return made;
    }
}
