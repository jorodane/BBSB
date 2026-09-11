using System;
using System.Collections.Generic;
using System.Linq;
using BBSB.Core;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class BattleGapFillerTests
    {
        [Test]
        public void ExactlyFourEmptyBeatsAreFilledButOneTickLessIsPreserved()
        {
            var stage = Stage(3, step: 1); var pattern = Pattern("tap"); var monster = Monster(pattern);
            var shorter = Resolve(stage, monster, (pattern, 16), (pattern, 39));
            Check.True(ReferenceEquals(shorter, BattleGapFiller.Fill(shorter, 3)));
            var original = Resolve(stage, monster, (pattern, 16), (pattern, 40));
            var filled = BattleGapFiller.Fill(original, 3);
            Check.Equal(1, filled.GapFills.Count); Check.Equal(24, filled.GapFills[0].ResponseStartTick);
            Check.Equal(2, original.Attacks.Count); Check.Equal(0, original.GapFills.Count);
            Check.True(original.Attacks.All(x => filled.Attacks.Contains(x)));
            Check.True(ReferenceEquals(stage, filled.Stage));
            Check.Equal(2, filled.Monsters[0].ProposedCount); Check.Equal(3, filled.Monsters[0].OccupiedBeatCount);
            Check.Equal(3, filled.Calls.Count);
            Check.True(filled.Calls.Select(x => x.Tick).SequenceEqual(new[] { 12, 20, 36 }));
            var round = new RhythmRound(filled);
            foreach (var attack in filled.Attacks)
            {
                double time = RhythmTime.Seconds(attack.ResponseStartTick, stage.Music.Bpm);
                round.Press(time, 0, 0); round.Release(time + .01, 0, 0);
            }
            Check.Equal(3, round.PerfectCount); Check.Equal(0, round.MissCount);
            Check.True(ReferenceEquals(filled, BattleGapFiller.Fill(filled, 999)));
        }

        [Test]
        public void ThresholdUsesFourBeatsIndependentOfTempoAndTimeSignature()
        {
            var pattern = Pattern("tap"); var monster = Monster(pattern); string expected = null;
            foreach (int bpm in new[] { 80, 200 })
            {
                var stage = Stage(4, bpm: bpm, meter: 3);
                var oneBarGap = Resolve(stage, monster, (pattern, 12), (pattern, 32));
                Check.Equal(0, BattleGapFiller.Fill(oneBarGap, 4).GapFills.Count);
                var fourBeatGap = BattleGapFiller.Fill(Resolve(stage, monster, (pattern, 12), (pattern, 36)), 4);
                Check.Equal(1, fourBeatGap.GapFills.Count); Check.Equal(20, fourBeatGap.GapFills[0].ResponseStartTick);
                if (expected == null) expected = Fingerprint(fourBeatGap);
                else Check.Equal(expected, Fingerprint(fourBeatGap));
            }
        }

        [Test]
        public void ShortestWholeCallAndResponseWinsBeforePopularity()
        {
            var stage = Stage(6); var shortCall = Pattern("short"); var longCall = Pattern("long", cue: 8, other: true);
            var original = Resolve(stage, Monster(shortCall, longCall),
                (shortCall, 16), (longCall, 32), (longCall, 48), (longCall, 80));
            var filled = BattleGapFiller.Fill(original, 7);
            Check.Equal(1, filled.GapFills.Count); Check.Equal(shortCall, filled.GapFills[0].Pattern);
            Check.Equal(56, filled.GapFills[0].ResponseStartTick);
        }

        [Test]
        public void UnplaceableShortestPatternDoesNotBlockALongerCompatibleOne()
        {
            var stage = Stage(6); var shortCall = Pattern("short", rest: 64); var longCall = Pattern("long", cue: 8, other: true);
            var original = Resolve(stage, Monster(shortCall, longCall),
                (longCall, 16), (longCall, 32), (longCall, 48), (shortCall, 80));
            var filled = BattleGapFiller.Fill(original, 1);
            Check.Equal(1, filled.GapFills.Count); Check.Equal(longCall, filled.GapFills[0].Pattern);
            Check.Equal(60, filled.GapFills[0].ResponseStartTick);
            AssertCompatible(filled);
        }

        [Test]
        public void EqualLengthCandidatesPreferTheMoreFrequentSurvivingPattern()
        {
            var stage = Stage(6, step: 1); var a = Pattern("a"); var b = Pattern("b", other: true);
            // Four proposed a's collapse to one; b survives three times. Proposal counts must not win.
            var original = Resolve(stage, Monster(a, b), (a, 16), (a, 17), (a, 18), (a, 19), (b, 40), (b, 52), (b, 88));
            Check.Equal(3, original.Withdrawals.Count);
            Check.Equal(1, original.Attacks.Count(x => x.Pattern == a));
            Check.Equal(3, original.Attacks.Count(x => x.Pattern == b));
            var filled = BattleGapFiller.Fill(original, 8);
            Check.True(filled.GapFills.Count > 0); Check.True(filled.GapFills.All(x => x.Pattern == b));
            Check.True(original.Withdrawals.SequenceEqual(filled.Withdrawals));
            AssertCompatible(filled);
        }

        [Test]
        public void TiesAreSeededAndEachInsertionUpdatesPopularity()
        {
            var stage = Stage(4); var a = Pattern("a"); var b = Pattern("b", other: true);
            var original = Resolve(stage, Monster(a, b), (a, 16), (b, 56));
            var reversed = Resolve(stage, Monster(b, a), (b, 56), (a, 16));
            var winners = new HashSet<string>();
            for (int seed = 0; seed < 32; seed++)
            {
                var filled = BattleGapFiller.Fill(original, seed); var winner = filled.GapFills[0].Pattern;
                winners.Add(winner.Id);
                Check.Equal(3, filled.GapFills.Count);
                Check.True(filled.GapFills.All(x => x.Pattern == winner), "The first random winner becomes the most frequent pattern.");
                Check.Equal(Fingerprint(filled), Fingerprint(BattleGapFiller.Fill(original, seed)));
                Check.Equal(Fingerprint(filled), Fingerprint(BattleGapFiller.Fill(reversed, seed)));
            }
            Check.Equal(2, winners.Count);
        }

        [Test]
        public void TiesChoosePatternTypesWithoutWeightingTheirNumberOfOpenSlots()
        {
            var stage = Stage(4); var a = Pattern("a", alignment: 1); var b = Pattern("b", other: true, alignment: 8);
            var original = Resolve(stage, Monster(a, b), (a, 16), (b, 60));
            int aWins = 0;
            for (int seed = 0; seed < 100; seed++)
                if (BattleGapFiller.Fill(original, seed).GapFills[0].Pattern == a) aWins++;
            Check.True(aWins >= 35 && aWins <= 65, "A pattern with more placements must not get extra lottery entries.");
        }

        [Test]
        public void CallsLongHoldsAndSpacesWithinAPhraseAreNotEmptyGaps()
        {
            var stage = Stage(4); var shortCall = Pattern("short", other: true);
            var held = Pattern("hold", cue: 20, response: 20, steps: new[] { new PatternStep(GestureKind.Hold, 0, 20) });
            var original = Resolve(stage, Monster(held, shortCall), (held, 32));
            Check.True(ReferenceEquals(original, BattleGapFiller.Fill(original, 2)));

            var sparse = Pattern("spaced", cue: 4, response: 32,
                steps: new[] { new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 20) });
            stage = Stage(3);
            var owner = Monster(sparse, shortCall);
            var another = new MonsterDefinition("another", "another", "", GestureKind.Tap, new[] { shortCall });
            var nested = BattlePlanner.Resolve(stage, new[]
            {
                Proposal(stage, owner, (sparse, 16)), Proposal(stage, another, (shortCall, 32))
            }, 2);
            Check.Equal(2, nested.Monsters.Count);
            Check.True(ReferenceEquals(nested, BattleGapFiller.Fill(nested, 2)), "Measure silence across all monsters, including nested phrases.");
        }

        [Test]
        public void UnfillableEarlyGapDoesNotPreventFillingALaterGapAndBothSidesKeepTheirRest()
        {
            var stage = Stage(9); var pattern = Pattern("rest", rest: 32);
            var original = Resolve(stage, Monster(pattern), (pattern, 16), (pattern, 80));
            var filled = BattleGapFiller.Fill(original, 5);
            Check.Equal(1, filled.GapFills.Count); Check.Equal(116, filled.GapFills[0].ResponseStartTick);
            AssertCompatible(filled);
            Check.True(ReferenceEquals(filled, BattleGapFiller.Fill(filled, 5)));
        }

        [Test]
        public void IntroBreakAndOutroStayEmptyWhilePlayableLeadingAndTrailingGapsCanFill()
        {
            var sections = new[] { new MusicSection("INTRO", 0, 2, 1, false), new MusicSection("A", 2, 2, 1),
                new MusicSection("BREAK", 4, 2, 1, false), new MusicSection("B", 6, 2, 1), new MusicSection("OUTRO", 8, 2, 1, false) };
            var stage = Stage(10, sections: sections); var pattern = Pattern("tap");
            var original = Resolve(stage, Monster(pattern), (pattern, 56), (pattern, 96));
            var filled = BattleGapFiller.Fill(original, 5);
            Check.True(filled.GapFills.Select(x => x.ResponseStartTick).SequenceEqual(new[] { 36, 104, 112 }));
            foreach (var fill in filled.GapFills)
                for (int tick = fill.CallStartTick; tick < fill.PhraseEndTick; tick++)
                    Check.True(stage.Music.SectionAtBar(tick / stage.Music.TicksPerBar).AllowsResponse);
            AssertCompatible(filled);
        }

        [Test]
        public void NoEligibleSlotOrMonsterLeavesThePlanUnchanged()
        {
            var pattern = Pattern("rest", rest: 128); var stage = Stage(3);
            var blocked = Resolve(stage, Monster(pattern), (pattern, 16));
            Check.True(ReferenceEquals(blocked, BattleGapFiller.Fill(blocked, 1)));
            var empty = BattlePlanner.Resolve(stage, Array.Empty<MonsterProposal>(), 1);
            Check.True(ReferenceEquals(empty, BattleGapFiller.Fill(empty, 1)));
            var sparseStage = MusicStage.Generate(new MusicDefinition("sparse", "Sparse", 120, 4,
                new[] { new MusicSection("INTRO", 0, 1, 1, false), new MusicSection("BODY", 1, 3, 1) },
                new[] { Array.Empty<SlotTemplate>(), new[] { new SlotTemplate(GestureKind.Tap, 0) },
                    Array.Empty<SlotTemplate>(), Array.Empty<SlotTemplate>() }));
            var shortPattern = Pattern("sparse");
            var sparse = Resolve(sparseStage, Monster(shortPattern), (shortPattern, 16));
            Check.True(ReferenceEquals(sparse, BattleGapFiller.Fill(sparse, 1)));
        }

        [Test]
        public void FormerlyConflictingTimingCanFillOnlyAfterItsBlockerHasGone()
        {
            var slots = Enumerable.Range(0, 4).SelectMany(i => new[] { new SlotTemplate(GestureKind.Tap, i * 4),
                new SlotTemplate(GestureKind.Flick, i * 4), new SlotTemplate(GestureKind.Hold, i * 4, 16) });
            var stage = MusicStage.Generate(new MusicDefinition("arbitration", "Arbitration", 120, 4,
                new[] { new MusicSection("INTRO", 0, 1, 1, false), new MusicSection("BODY", 1, 7, 1) }, new[] { slots }));
            var tap = Pattern("tap"); var held = Pattern("held", response: 16, steps: new[] { new PatternStep(GestureKind.Hold, 0, 16) });
            var flick = Pattern("flick", steps: new[] { new PatternStep(GestureKind.Flick, 0) });
            var blocker = new MonsterDefinition("blocker", "blocker", "", GestureKind.Hold, new[] { held });
            var finisher = new MonsterDefinition("finisher", "finisher", "", GestureKind.Flick, new[] { flick });
            var original = BattlePlanner.Resolve(stage, new[]
            {
                Proposal(stage, Monster(tap), (tap, 20), (tap, 80), (tap, 92), (tap, 104), (tap, 116)),
                Proposal(stage, blocker, (held, 20)), Proposal(stage, finisher, (flick, 36))
            }, 5);
            Check.Equal(2, original.Withdrawals.Count); Check.Equal(2, original.Monsters.Count);
            var removed = original.Withdrawals[0].Attack;
            Check.Equal(20, removed.ResponseStartTick); Check.Equal(tap, removed.Pattern);
            var filled = BattleGapFiller.Fill(original, 5);
            Check.Equal(removed.Id, filled.GapFills[0].Id);
            Check.False(ReferenceEquals(removed, filled.GapFills[0]));
            Check.False(filled.Monsters.Any(x => x.Monster == blocker));
            AssertCompatible(filled);
        }

        [Test]
        public void GeneratedEncountersIncludeFillsWithCompleteCallsAndNoRemainingCompatibleGap()
        {
            int count = 0;
            foreach (var music in MusicCatalog.All)
            foreach (int field in new[] { 1, 3 })
            {
                var plan = BattlePlanner.Generate(MusicStage.Generate(music), StageKind.Monster, 23, field);
                count += plan.GapFills.Count;
                Check.True(ReferenceEquals(plan, BattleGapFiller.Fill(plan, 91)));
                Check.True(plan.GapFills.All(x => plan.Attacks.Contains(x)));
                Check.Equal(plan.Attacks.Sum(x => x.Call.Count), plan.Calls.Count);
                Check.True(plan.Monsters.Count <= field);
                foreach (var owner in plan.Monsters)
                    Check.True(owner.Attacks.Select(x => x.ResponseStartTick).SequenceEqual(owner.Attacks.Select(x => x.ResponseStartTick).OrderBy(x => x)));
                AssertCompatible(plan);
            }
            Check.True(count > 0, "Encounter generation must invoke the gap filler.");
        }

        private static MonsterPatternDefinition Pattern(string id, int cue = 4, int response = 4, int rest = 4,
            bool other = false, int alignment = 1, PatternStep[] steps = null)
            => new MonsterPatternDefinition(id, "", new RhythmPattern(id, cue, steps ?? new[] { new PatternStep(GestureKind.Tap, 0) }),
                new[] { new CallSignal(0, "call", other ? CallSound.Bell : CallSound.Wood, other ? CallMotion.Hop : CallMotion.Step) },
                response, rest, 1, alignment);
        private static MonsterDefinition Monster(params MonsterPatternDefinition[] patterns)
            => new MonsterDefinition("owner", "owner", "", GestureKind.Tap, patterns);
        private static MonsterProposal Proposal(MusicStage stage, MonsterDefinition owner, params (MonsterPatternDefinition pattern, int tick)[] entries)
            => new MonsterProposal(owner.Id, owner, entries.Select(x => stage.FindPlacements(x.pattern.Pattern).Single(y => y.StartTick == x.tick)));
        private static BattlePlan Resolve(MusicStage stage, MonsterDefinition owner, params (MonsterPatternDefinition pattern, int tick)[] entries)
            => BattlePlanner.Resolve(stage, new[] { Proposal(stage, owner, entries) }, 2);
        private static MusicStage Stage(int bars, int step = 4, int bpm = 120, int meter = 4, MusicSection[] sections = null)
        {
            var slots = new List<SlotTemplate>();
            for (int tick = 0; tick < meter * 4; tick += step)
            {
                slots.Add(new SlotTemplate(GestureKind.Tap, tick));
                slots.Add(new SlotTemplate(GestureKind.Hold, tick, 20));
            }
            return MusicStage.Generate(new MusicDefinition("gaps", "Gaps", bpm, meter,
                sections ?? new[] { new MusicSection("INTRO", 0, 1, 1, false), new MusicSection("BODY", 1, bars - 1, 1) }, new[] { slots }));
        }
        private static string Fingerprint(BattlePlan plan) => string.Join("|", plan.Attacks.Select(x => x.Id));
        private static void AssertCompatible(BattlePlan plan)
        {
            Check.Equal(plan.Attacks.Count, plan.Attacks.Select(x => x.Id).Distinct().Count());
            for (int i = 0; i < plan.Attacks.Count; i++) for (int j = i + 1; j < plan.Attacks.Count; j++)
                Check.False(BattlePlanner.Conflicts(plan.Attacks[i], plan.Attacks[j], out _));
        }
    }
}
