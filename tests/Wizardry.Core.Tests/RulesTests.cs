using Wizardry.Core.Content;
using Wizardry.Core.Model;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Tests;

public sealed class RulesTests
{
    [Fact]
    public void Dice_parse_and_roll_within_their_range()
    {
        Dice d = "3d6+2";
        Assert.Equal(new Dice(3, 6, 2), d);
        Assert.Equal("3d6+2", d.ToString());
        Assert.Equal(new Dice(1, 4, -1), Dice.Parse("1d4-1"));
        var rng = new Rng(7);
        for (int i = 0; i < 500; i++)
        {
            int r = d.Roll(rng);
            Assert.InRange(r, 5, 20);
        }
    }

    [Fact]
    public void The_same_seed_gives_the_same_rolls()
    {
        var a = new Rng(42);
        var b = new Rng(42);
        for (int i = 0; i < 100; i++) Assert.Equal(a.Next(1000), b.Next(1000));
    }

    [Theory]
    [InlineData(CharClass.Fighter, Alignment.Evil, new[] { 11, 3, 3, 3, 3, 3 }, true)]
    [InlineData(CharClass.Fighter, Alignment.Good, new[] { 10, 18, 18, 18, 18, 18 }, false)]
    [InlineData(CharClass.Priest, Alignment.Neutral, new[] { 3, 3, 18, 3, 3, 3 }, false)]
    [InlineData(CharClass.Thief, Alignment.Good, new[] { 3, 3, 3, 3, 18, 3 }, false)]
    [InlineData(CharClass.Thief, Alignment.Neutral, new[] { 3, 3, 3, 3, 11, 3 }, true)]
    [InlineData(CharClass.Bishop, Alignment.Evil, new[] { 3, 12, 12, 3, 3, 3 }, true)]
    [InlineData(CharClass.Samurai, Alignment.Evil, new[] { 15, 11, 10, 14, 10, 3 }, false)]
    [InlineData(CharClass.Samurai, Alignment.Neutral, new[] { 15, 11, 10, 14, 10, 3 }, true)]
    [InlineData(CharClass.Lord, Alignment.Good, new[] { 15, 12, 12, 15, 14, 15 }, true)]
    [InlineData(CharClass.Lord, Alignment.Neutral, new[] { 18, 18, 18, 18, 18, 18 }, false)]
    [InlineData(CharClass.Ninja, Alignment.Evil, new[] { 17, 17, 17, 17, 17, 17 }, true)]
    [InlineData(CharClass.Ninja, Alignment.Evil, new[] { 17, 17, 17, 17, 17, 16 }, false)]
    public void Classes_have_their_requirements(CharClass cls, Alignment align, int[] stats, bool ok) =>
        Assert.Equal(ok, RuleTables.Qualifies(cls, align, stats));

    [Fact]
    public void Bonus_points_are_usually_five_to_nine()
    {
        var rng = new Rng(3);
        var rolls = Enumerable.Range(0, 5000).Select(_ => Progression.RollBonus(rng)).ToList();
        Assert.All(rolls, b => Assert.True(b is >= 5 and <= 9 or >= 15 and <= 19 or >= 25 and <= 29));
        Assert.True(rolls.Count(b => b <= 9) > 4000);
        Assert.Contains(rolls, b => b >= 15);
    }

    [Fact]
    public void Experience_needed_rises_with_every_level()
    {
        foreach (var c in Enum.GetValues<CharClass>())
            for (int level = 2; level < 30; level++)
                Assert.True(RuleTables.ExperienceFor(c, level + 1) > RuleTables.ExperienceFor(c, level), $"{c} {level}");
        Assert.Equal(1000, RuleTables.ExperienceFor(CharClass.Fighter, 2));
        Assert.Equal(0, RuleTables.ExperienceFor(CharClass.Mage, 1));
    }

    [Fact]
    public void Spell_levels_open_by_class()
    {
        Assert.Equal(1, RuleTables.SpellSlots(CharClass.Mage, School.Mage, 1, 1));
        Assert.Equal(0, RuleTables.SpellSlots(CharClass.Mage, School.Mage, 2, 2));
        Assert.Equal(1, RuleTables.SpellSlots(CharClass.Mage, School.Mage, 2, 3));
        Assert.Equal(0, RuleTables.SpellSlots(CharClass.Fighter, School.Mage, 1, 20));
        Assert.Equal(0, RuleTables.SpellSlots(CharClass.Samurai, School.Mage, 1, 3));
        Assert.Equal(1, RuleTables.SpellSlots(CharClass.Samurai, School.Mage, 1, 4));
        Assert.Equal(1, RuleTables.SpellSlots(CharClass.Bishop, School.Priest, 1, 4));
        Assert.Equal(9, RuleTables.SpellSlots(CharClass.Priest, School.Priest, 1, 40));
    }

    [Fact]
    public void A_new_mage_knows_a_first_level_spell_and_can_cast_it()
    {
        var rng = new Rng(11);
        var mage = Progression.Create("TEST", Race.Elf, Alignment.Neutral, CharClass.Mage, [7, 15, 10, 6, 9, 6], rng);
        Assert.Contains(mage.Spells, s => Spells.Get(s) is { School: School.Mage, Level: 1 });
        // At least one cast per first-level spell known (as in the original).
        Assert.Equal(mage.Spells.Count(s => Spells.Get(s).Level == 1), mage.MageSlots[0]);
        Assert.Equal(0, mage.PriestSlots[0]);
        Assert.True(mage.MaxHp >= 2);
        Assert.InRange(mage.AgeYears, 18, 21);
    }

    [Fact]
    public void Levelling_up_adds_hit_points_and_opens_spells()
    {
        var rng = new Rng(5);
        var priest = Progression.Create("TEST", Race.Dwarf, Alignment.Good, CharClass.Priest, [10, 7, 15, 12, 5, 6], rng);
        int hp = priest.MaxHp;
        priest.Experience = RuleTables.ExperienceFor(CharClass.Priest, 3);
        var notes = Progression.TryLevelUp(priest, rng);
        Assert.Equal(3, priest.Level);
        Assert.True(priest.MaxHp > hp);
        Assert.Contains(notes, n => n.Contains("level 3"));
        Assert.Contains(priest.Spells, s => Spells.Get(s) is { School: School.Priest, Level: 2 });
    }

    [Fact]
    public void Drained_characters_lose_a_level_and_hit_points()
    {
        var rng = new Rng(9);
        var f = Progression.Create("TEST", Race.Human, Alignment.Good, CharClass.Fighter, [15, 8, 5, 14, 8, 9], rng);
        f.Experience = RuleTables.ExperienceFor(CharClass.Fighter, 5);
        Progression.TryLevelUp(f, rng);
        int hp = f.MaxHp;
        Progression.Drain(f);
        Assert.Equal(4, f.Level);
        Assert.True(f.MaxHp < hp);
        Assert.Equal(RuleTables.ExperienceFor(CharClass.Fighter, 4), f.Experience);
    }

    [Fact]
    public void Changing_class_starts_over_but_keeps_spells()
    {
        var rng = new Rng(2);
        var mage = Progression.Create("TEST", Race.Human, Alignment.Neutral, CharClass.Mage, [15, 13, 10, 14, 10, 9], rng);
        var known = mage.Spells.ToList();
        mage.Stats = [15, 13, 10, 14, 10, 9];
        Assert.Contains(CharClass.Samurai, Progression.ClassChanges(mage));
        Progression.ChangeClass(mage, CharClass.Samurai, rng);
        Assert.Equal(CharClass.Samurai, mage.Class);
        Assert.Equal(1, mage.Level);
        Assert.All(known, s => Assert.Contains(s, mage.Spells));
        Assert.Equal(RuleTables.BaseStats(Race.Human), mage.Stats);
        // A known spell keeps a cast even before the samurai's own spell levels open.
        Assert.True(mage.MageSlots[0] >= 1);
    }

    [Fact]
    public void Armour_lowers_armour_class_and_ninjas_fight_best_bare()
    {
        var rng = new Rng(4);
        var f = Progression.Create("TEST", Race.Human, Alignment.Good, CharClass.Fighter, [15, 8, 5, 14, 8, 9], rng);
        Assert.Equal(10, f.ArmorClass());
        f.Items.Add(new OwnedItem { ItemId = 44, Identified = true, Equipped = true }); // plate, 5
        f.Items.Add(new OwnedItem { ItemId = 61, Identified = true, Equipped = true }); // large shield, 3
        Assert.Equal(2, f.ArmorClass());
        Assert.Equal(1, f.ArmorClass(partyWard: 1));
        var n = Progression.Create("NIN", Race.Human, Alignment.Evil, CharClass.Ninja, [17, 17, 17, 17, 17, 17], rng);
        Assert.Equal(8, n.ArmorClass());
        Assert.Equal(2, n.Swings);
        Assert.True(n.CriticalChance > 0);
    }

    [Fact]
    public void Every_item_and_spell_reference_is_valid()
    {
        foreach (var item in Items.All)
        {
            if (item.Spell is { } s) Assert.NotNull(Spells.Find(s));
            Assert.True(item.Classes.All(ch => "FMPTBSLN".Contains(ch)), item.Name);
        }
        Assert.Equal(Items.All.Count, Items.All.Select(i => i.Id).Distinct().Count());
        Assert.Equal(Spells.All.Count, Spells.All.Select(s => s.Name).Distinct().Count());
        foreach (var school in new[] { School.Mage, School.Priest })
            for (int level = 1; level <= 7; level++)
                Assert.NotEmpty(Spells.Of(school, level));
    }

    [Fact]
    public void Every_level_has_monsters_and_companions_exist()
    {
        for (int d = 1; d <= 10; d++)
            Assert.All(Monsters.ForLevel(d), id => Assert.NotNull(Monsters.Get(id)));
        foreach (var m in Monsters.All)
        {
            if (m.Companion != 0) Assert.NotNull(Monsters.Get(m.Companion));
            Assert.True(m.Experience > 0, m.Name);
        }
    }
}
