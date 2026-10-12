using Wizardry.Core.Combat;
using Wizardry.Core.Content;
using Wizardry.Core.Model;
using Wizardry.Core.Rules;
using Wizardry.Core.Screens;

namespace Wizardry.Core.Tests;

public sealed class CombatTests
{
    private static (World World, Rng Rng) Party(ulong seed = 1)
    {
        var world = World.NewGame(seed);
        var rng = new Rng(seed);
        Recruits.AddQuickParty(world, rng);
        world.Depth = 1;
        return (world, rng);
    }

    [Fact]
    public void Only_the_front_rank_can_melee()
    {
        var (world, rng) = Party();
        var battle = new Battle(world, rng, [FoeGroup.Spawn(Monsters.Get(1), 3, rng, true)], lair: false, chest: false, antiMagic: false);
        var members = world.Members.ToList();
        Assert.NotEmpty(battle.FightTargets(members[0]));
        Assert.Empty(battle.FightTargets(members[4]));
    }

    [Fact]
    public void Fighting_kills_monsters_and_fills_the_experience_pool()
    {
        var (world, rng) = Party(2);
        var group = FoeGroup.Spawn(Monsters.Get(2), 2, rng, true);
        var battle = new Battle(world, rng, [group], lair: false, chest: false, antiMagic: false);
        for (int round = 0; round < 30 && battle.Outcome == BattleOutcome.Ongoing; round++)
            battle.PlayRound(world.Members.Where(c => c.Status.CanAct()).Select(c => new PartyAction(c, battle.FightTargets(c).Any() ? ActionKind.Fight : ActionKind.Parry)).ToList());
        Assert.Equal(BattleOutcome.Won, battle.Outcome);
        Assert.Equal(2 * Monsters.Get(2).Experience, battle.ExperiencePool);
    }

    [Fact]
    public void Sleep_puts_weak_monsters_to_sleep()
    {
        var (world, rng) = Party(3);
        var mage = world.Members.First(c => c.Class == CharClass.Mage);
        mage.Spells.Add("SOMNA");
        mage.MageSlots[0] = 5;
        var group = FoeGroup.Spawn(Monsters.Get(2), 9, rng, true);
        var battle = new Battle(world, rng, [group], lair: false, chest: false, antiMagic: false);
        var log = battle.PlayRound([new PartyAction(mage, ActionKind.Spell, 0, Spells.Get("SOMNA"))]);
        Assert.Contains(log, l => l.Contains("fall asleep") || l.Contains("falls asleep"));
        Assert.Equal(4, mage.MageSlots[0]);
    }

    [Fact]
    public void Spells_fizzle_on_anti_magic_squares()
    {
        var (world, rng) = Party(4);
        var mage = world.Members.First(c => c.Class == CharClass.Mage);
        mage.Spells.Add("ARDO");
        mage.MageSlots[0] = 3;
        var group = FoeGroup.Spawn(Monsters.Get(2), 1, rng, true);
        int hp = group.Members[0].Hp;
        var battle = new Battle(world, rng, [group], lair: false, chest: false, antiMagic: true);
        var log = battle.PlayRound([new PartyAction(mage, ActionKind.Spell, 0, Spells.Get("ARDO"))]);
        Assert.Contains(log, l => l.Contains("fizzles"));
        Assert.Equal(2, mage.MageSlots[0]);
    }

    [Fact]
    public void Healing_spells_heal_and_raising_can_turn_to_ashes()
    {
        var (world, rng) = Party(5);
        var priest = world.Members.First(c => c.Class == CharClass.Priest);
        var f = world.Members.First();
        f.Hp = 1;
        var log = new List<string>();
        Magic.Apply(Spells.Get("SANCTA"), world, rng, null, null, f, log, priest);
        Assert.Equal(f.MaxHp, f.Hp);
        int alive = 0, ashes = 0;
        for (int i = 0; i < 200; i++)
        {
            f.Status = Status.Dead;
            Magic.Apply(Spells.Get("VITA"), world, rng, null, null, f, log, priest);
            if (f.Status == Status.Ok) alive++;
            if (f.Status == Status.Ashes) ashes++;
        }
        Assert.True(alive > 0 && ashes > 0);
    }

    [Fact]
    public void Running_away_from_a_lair_is_impossible()
    {
        var (world, rng) = Party(6);
        var battle = new Battle(world, rng, [FoeGroup.Spawn(Monsters.Get(Monsters.Warden), 1, rng, true)], lair: true, chest: true, antiMagic: false);
        var first = world.Members.First();
        for (int i = 0; i < 10; i++)
        {
            var log = battle.PlayRound([new PartyAction(first, ActionKind.Run)]);
            if (battle.Outcome != BattleOutcome.Ongoing) break;
            Assert.Contains(log, l => l.Contains("no escape"));
        }
        Assert.NotEqual(BattleOutcome.Fled, battle.Outcome);
    }

    [Fact]
    public void Chests_name_their_traps_and_hold_treasure()
    {
        var rng = new Rng(7);
        var kinds = new HashSet<TrapKind>();
        for (int i = 0; i < 400; i++)
        {
            var chest = Chest.Roll(6, rng, lair: false);
            kinds.Add(chest.Trap);
            Assert.True(chest.Gold > 0);
        }
        Assert.Equal(Enum.GetValues<TrapKind>().Length, kinds.Count);
        Assert.All(Enum.GetValues<TrapKind>(), t => Assert.False(string.IsNullOrEmpty(Traps.Name(t))));
    }

    [Fact]
    public void The_warden_guards_the_ribbon_until_someone_holds_it()
    {
        var g = new Game(World.NewGame(12));
        Recruits.AddQuickParty(g.World, g.Rng);
        foreach (var c in g.World.Members) { c.MaxHp = c.Hp = 999; }
        g.Press("E");
        g.Press("M");
        var lair = Maze.MazeBuilder.Get(4).LairCenter!.Value;
        g.Teleport(4, random: false, lair.X, lair.Y);
        g.Arrive();
        Assert.True(g.Battle!.IsLair);
        // Once the ribbon is carried, the lair is empty.
        g.World.Members.First().Items.Add(OwnedItem.Known(Items.RibbonId));
        var g2 = new Game(g.World);
        g2.Teleport(4, random: false, lair.X, lair.Y);
        g2.Arrive();
        Assert.True(g2.Battle is null || !g2.Battle.IsLair);
    }
}
