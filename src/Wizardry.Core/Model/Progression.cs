using Wizardry.Core.Content;
using Wizardry.Core.Rules;

namespace Wizardry.Core.Model;

/// <summary>Making characters, and what happens to them as they grow (and grow old).</summary>
public static class Progression
{
    /// <summary>
    /// Bonus points to spread at creation: 5-9 usually, with a 1 in 11 chance of 10 more, and of 10
    /// more again. (Rerolling for a high number is a time-honoured habit.)
    /// </summary>
    public static int RollBonus(Rng rng)
    {
        int bonus = rng.Range(5, 9);
        if (rng.OneIn(11))
        {
            bonus += 10;
            if (rng.OneIn(11)) bonus += 10;
        }
        return bonus;
    }

    public static IEnumerable<CharClass> EligibleClasses(Alignment alignment, IReadOnlyList<int> stats) =>
        Enum.GetValues<CharClass>().Where(c => RuleTables.Qualifies(c, alignment, stats));

    public static Character Create(string name, Race race, Alignment alignment, CharClass cls, int[] stats, Rng rng)
    {
        if (!RuleTables.Qualifies(cls, alignment, stats))
            throw new ArgumentException($"A {alignment} {race} with those attributes can't be a {cls}.");
        var c = new Character
        {
            Name = name,
            Race = race,
            Alignment = alignment,
            Class = cls,
            Stats = (int[])stats.Clone(),
            AgeWeeks = RuleTables.StartingAgeWeeks(rng),
            Gold = rng.Roll(RuleTables.StartingGold),
        };
        c.MaxHp = Math.Max(2, RuleTables.StartingHp(cls) + RuleTables.VitalityBonus(c.Vit) + rng.Range(0, 2));
        c.Hp = c.MaxHp;
        LearnSpells(c, rng, firstTime: true);
        c.RestoreSpells();
        return c;
    }

    /// <summary>A character's level-up at the inn. Returns what changed, or an empty list.</summary>
    public static List<string> TryLevelUp(Character c, Rng rng)
    {
        var notes = new List<string>();
        while (c.Experience >= RuleTables.ExperienceFor(c.Class, c.Level + 1))
        {
            c.Level++;
            c.MaxLevel = Math.Max(c.MaxLevel, c.Level);
            notes.Add($"{c.Name} made level {c.Level}!");
            int gain = Math.Max(1, rng.Range(1, RuleTables.HitDie(c.Class)) + RuleTables.VitalityBonus(c.Vit));
            c.MaxHp += gain;
            c.Hp += gain;
            notes.AddRange(ChangeStats(c, rng));
            if (c.Status == Status.Lost) return notes; // died of old age
            int known = c.Spells.Count;
            LearnSpells(c, rng, firstTime: false);
            if (c.Spells.Count > known) notes.Add("You learned new spells!");
        }
        return notes;
    }

    /// <summary>
    /// At each new level every attribute may rise or fall. The older the character, the likelier it
    /// falls. Vitality falling below 3 means death of old age.
    /// </summary>
    private static IEnumerable<string> ChangeStats(Character c, Rng rng)
    {
        var notes = new List<string>();
        foreach (var stat in RuleTables.AllStats)
        {
            if (!rng.Percent(30)) continue;
            int i = (int)stat;
            bool fall = rng.Next(130) < c.AgeYears;
            if (fall && c.Stats[i] > 3)
            {
                c.Stats[i]--;
                notes.Add($"You lost {stat.Abbrev()}.");
            }
            else if (!fall && c.Stats[i] < RuleTables.MaxStat)
            {
                c.Stats[i]++;
                notes.Add($"You gained {stat.Abbrev()}.");
            }
        }
        if (c.Vit < 3 || c.AgeYears > 90)
        {
            c.Status = Status.Lost;
            notes.Add($"{c.Name} has died of old age.");
        }
        return notes;
    }

    /// <summary>
    /// Spells come with level: the first spell of each newly opened spell level is certain, and
    /// each other spell is learned with a chance set by I.Q. (mage) or piety (priest).
    /// </summary>
    public static void LearnSpells(Character c, Rng rng, bool firstTime)
    {
        foreach (var school in new[] { School.Mage, School.Priest })
        {
            if (!c.CanCast(school)) continue;
            int stat = school == School.Mage ? c.Iq : c.Pie;
            for (int level = 1; level <= 7; level++)
            {
                if (RuleTables.SpellLevelOpensAt(c.Class, school, level) is not { } opens || c.Level < opens) continue;
                var spells = Spells.Of(school, level).ToList();
                bool anyKnown = spells.Any(c.Knows);
                foreach (var s in spells)
                {
                    if (c.Knows(s)) continue;
                    if (!anyKnown || rng.Next(30) < stat)
                    {
                        c.Spells.Add(s.Name);
                        anyKnown = true;
                    }
                }
            }
        }
        if (firstTime) c.RestoreSpells();
    }

    /// <summary>Energy drain: a level lost, with its hit points.</summary>
    public static void Drain(Character c, int levels = 1)
    {
        for (int i = 0; i < levels && c.Level > 1; i++)
        {
            c.Level--;
            c.Experience = RuleTables.ExperienceFor(c.Class, c.Level);
            int loss = Math.Max(1, c.MaxHp / (c.Level + 1));
            c.MaxHp = Math.Max(1, c.MaxHp - loss);
            c.Hp = Math.Min(c.Hp, c.MaxHp);
        }
        if (c.Level == 1 && levels > 0 && c.Experience > 0) c.Experience = 0;
        for (int l = 1; l <= 7; l++)
        {
            c.MageSlots[l - 1] = Math.Min(c.MageSlots[l - 1], c.MaxSlots(School.Mage, l));
            c.PriestSlots[l - 1] = Math.Min(c.PriestSlots[l - 1], c.MaxSlots(School.Priest, l));
        }
    }

    /// <summary>
    /// Changing class at the training grounds: back to level 1 with the new class, attributes back
    /// to the race's base, older, but keeping known spells, gold, items and hit points.
    /// </summary>
    public static void ChangeClass(Character c, CharClass to, Rng rng)
    {
        if (!RuleTables.Qualifies(to, c.Alignment, c.Stats))
            throw new InvalidOperationException($"{c.Name} doesn't qualify to be a {to}.");
        c.Class = to;
        c.Level = 1;
        c.MaxLevel = 1;
        c.Experience = 0;
        c.Stats = RuleTables.BaseStats(c.Race);
        c.AgeWeeks += 52 * 3 + rng.Next(52);
        foreach (var item in c.Items.Where(i => i.Equipped && !i.Def.UsableBy(to) && !i.Def.Cursed))
            item.Equipped = false;
        LearnSpells(c, rng, firstTime: true);
        c.RestoreSpells();
    }

    /// <summary>Who can a character change into now (not their own class).</summary>
    public static IEnumerable<CharClass> ClassChanges(Character c) =>
        EligibleClasses(c.Alignment, c.Stats).Where(k => k != c.Class);

    /// <summary>Time passes for a resting character.</summary>
    public static void Age(Character c, int weeks) => c.AgeWeeks += weeks;
}
