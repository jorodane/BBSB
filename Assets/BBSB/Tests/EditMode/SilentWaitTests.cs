using System;
using System.Linq;
using BBSB.Core;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class SilentWaitTests
    {
        [Test]
        public void WaitingThemesPairOneLongWaitWithAMoreFrequentShortTap()
        {
            var monsters = MonsterCatalog.All.Where(x => x.Patterns.Any(p => p.SilentWaitTicks > 0)).ToArray();
            Check.Equal(2, monsters.Length);
            foreach (var monster in monsters)
            {
                var wait = monster.Patterns.Single(x => x.SilentWaitTicks > 0);
                var quick = monster.Patterns.Single(x => x.SilentWaitTicks == 0);
                Check.Equal(monster.Id == "drowsy-slime" ? 16 : 28, wait.SilentWaitTicks);
                Check.Equal(wait.SilentWaitTicks, wait.Pattern.CueLeadTicks);
                Check.Equal(1, wait.Call.Count); Check.Equal(0, wait.Call[0].OffsetTick);
                Check.Equal(1, wait.Pattern.Steps.Count); Check.Equal(GestureKind.Tap, wait.Pattern.Steps[0].Kind);
                Check.Equal(4, quick.Pattern.CueLeadTicks); Check.Equal(4, quick.ResponseTicks);
                Check.True(quick.ParticipationChance > wait.ParticipationChance);
                Check.True(quick.Call[0].Sound != wait.Call[0].Sound);
                Check.True(quick.Call[0].Motion != wait.Call[0].Motion);
            }
        }

        [Test]
        public void LongWaitRequiresRememberingFourOrSevenBeatsBeforeTheOnlyResponse()
        {
            foreach (var music in MusicCatalog.All)
            foreach (var monster in MonsterCatalog.All.Where(x => x.Patterns.Any(p => p.SilentWaitTicks > 0)))
            {
                var stage = MusicStage.Generate(music); var wait = monster.Patterns.Single(x => x.SilentWaitTicks > 0);
                var plan = BattlePlanner.Resolve(stage, new[] { Proposal(stage, monster, wait, 64) }, 1);
                var attack = plan.Attacks.Single(); var round = new RhythmRound(plan);
                double call = RhythmTime.Seconds(attack.CallStartTick, music.Bpm);
                double response = RhythmTime.Seconds(64, music.Bpm);
                Check.True(Math.Abs(response - call - round.BeatSeconds * wait.SilentWaitTicks / 4) < 1e-9);
                round.Advance(call); Check.Equal(1, round.Calls.Count); Check.Equal(0, round.Results.Count);
                round.Advance(response - .001);
                Check.Equal(1, round.Calls.Count); Check.Equal(0, round.Results.Count);
                Check.Equal(1, round.Notes.Count); Check.Equal(64, round.Notes[0].StartTick);
                round.Press(response, 0, 0);
                Check.Equal(1, round.PerfectCount); Check.Equal(0, round.MissCount);
            }
        }

        [Test]
        public void AMonsterCannotCombineTwoLongWaitingPatterns()
        {
            var waits = MonsterCatalog.All.SelectMany(x => x.Patterns).Where(x => x.SilentWaitTicks > 0).ToArray();
            Reject(() => new MonsterDefinition("two-waits", "Two waits", "", GestureKind.Tap, waits));
        }

        [Test]
        public void AuthoredWaitMustReachTheResponseFromTheFinalCallWithoutAnExtraCue()
        {
            Reject(() => Wait(16, -1, 0));
            Reject(() => Wait(12, 12, 0));
            Reject(() => Wait(20, 16, 0));
            Reject(() => Wait(20, 16, 0, 8));
            var valid = Wait(20, 16, 0, 4);
            Check.Equal(16, valid.SilentWaitTicks);
        }

        [Test]
        public void GapFillingPreservesAuthoredWaitsAgainstEveryMonstersFillCandidates()
        {
            var stage = MusicStage.Generate(MusicCatalog.All.Single(x => x.Id == "steady-pulse"));
            var other = MonsterCatalog.All.Single(x => x.Id == "march-slime");
            foreach (var monster in MonsterCatalog.All.Where(x => x.Patterns.Any(p => p.SilentWaitTicks > 0)))
            foreach (bool includeOther in new[] { false, true })
            {
                var wait = monster.Patterns.Single(x => x.SilentWaitTicks > 0);
                var proposals = new[] { Proposal(stage, monster, wait, 64) }.ToList();
                if (includeOther) proposals.Add(Proposal(stage, other, other.Patterns[0], 112));
                var original = BattlePlanner.Resolve(stage, proposals, 3);
                var protectedAttack = original.Attacks.Single(x => x.Pattern == wait);
                var filled = BattleGapFiller.Fill(original, 3);
                Check.True(filled.GapFills.Count > 0);
                Check.True(filled.Attacks.Contains(protectedAttack));
                foreach (var fill in filled.GapFills)
                {
                    Check.True(fill.PhraseEndTick <= protectedAttack.CallStartTick ||
                        fill.CallStartTick >= protectedAttack.PhraseEndTick, "An authored wait is part of its Call/Response phrase.");
                    Check.Equal(0, fill.Pattern.SilentWaitTicks);
                }
                Check.True(ReferenceEquals(filled, BattleGapFiller.Fill(filled, 9)));
            }
        }

        [Test]
        public void ShortPatternsRemainMoreFrequentAfterArbitrationAndGapFilling()
        {
            foreach (var music in MusicCatalog.All)
            foreach (var monster in MonsterCatalog.All.Where(x => x.Patterns.Any(p => p.SilentWaitTicks > 0)))
            {
                var stage = MusicStage.Generate(music); int shortCount = 0, waitCount = 0;
                for (int seed = 0; seed < 80; seed++)
                {
                    var proposal = BattlePlanner.Propose(stage, monster.Id, monster, seed);
                    var plan = BattleGapFiller.Fill(BattlePlanner.Resolve(stage, new[] { proposal }, seed), seed);
                    shortCount += plan.Attacks.Count(x => x.Pattern.SilentWaitTicks == 0);
                    waitCount += plan.Attacks.Count(x => x.Pattern.SilentWaitTicks > 0);
                }
                Check.True(waitCount > 0, monster.Id + " must sometimes use its wait in " + music.Id);
                Check.True(shortCount > waitCount * 2,
                    monster.Id + " in " + music.Id + ": " + shortCount + " short, " + waitCount + " waiting occurrences.");
            }
        }

        private static MonsterProposal Proposal(MusicStage stage, MonsterDefinition monster, MonsterPatternDefinition pattern, int tick)
            => new MonsterProposal(monster.Id, monster, new[] { stage.FindPlacements(pattern.Pattern).Single(x => x.StartTick == tick) });
        private static MonsterPatternDefinition Wait(int cue, int wait, params int[] calls)
            => new MonsterPatternDefinition("Wait", "", new RhythmPattern("wait", cue, new[] { new PatternStep(GestureKind.Tap, 0) }),
                calls.Select(x => new CallSignal(x, "Call")), 4, 4, .2, silentWaitTicks: wait);
        private static void Reject(Action action)
        {
            try { action(); }
            catch (ArgumentException) { return; }
            throw new Exception("Expected invalid waiting pattern to be rejected.");
        }
    }
}
