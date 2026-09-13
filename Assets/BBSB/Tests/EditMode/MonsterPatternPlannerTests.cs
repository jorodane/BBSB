using System;
using System.Collections.Generic;
using System.Linq;
using BBSB.Core;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class MonsterPatternPlannerTests
    {
        [Test]
        public void BeatShiftChainCountsMainBeatsThenOffbeatsThenMainBeats()
        {
            var stage = Stage(); var monster = Seesaw(); var chain = At(stage, monster, 16);
            var plan = Resolve(stage, monster, chain);
            Check.True(plan.Attacks.Count > 11); Check.Equal(plan.Attacks.Count, plan.Calls.Count);
            Check.True(plan.Calls.Take(11).Select(x => x.Tick).SequenceEqual(new[] { 16, 20, 24, 28, 34, 38, 42, 46, 52, 56, 60 }));
            Check.True(new RhythmRound(plan).Notes.Take(13).Select(x => x.StartTick)
                .SequenceEqual(new[] { 20, 24, 28, 32, 34, 38, 42, 46, 50, 52, 56, 60, 64 }));
            Check.True(plan.Attacks.All(x => ReferenceEquals(chain, x.Chain)));
            for (int i = 1; i < plan.Attacks.Count; i++)
                Check.Equal(plan.Attacks[i - 1].ResponseEndTick, plan.Attacks[i].CallStartTick);
            Check.Equal(0, plan.Withdrawals.Count); Check.Equal(0, chain.RestTicks); AssertUnbroken(chain);
            AssertCompatible(plan);
        }

        [Test]
        public void AllLinkedTapsJudgeOnceAndAMissDoesNotResetTheAuthoredPhase()
        {
            var stage = Stage(); var monster = Seesaw(); var plan = Resolve(stage, monster, At(stage, monster, 16));
            var round = new RhythmRound(plan);
            foreach (var note in round.Notes)
            { round.Press(note.StartSeconds, 0, 0); round.Release(note.StartSeconds + .001, 0, 0); }
            Check.Equal(round.Notes.Count, round.PerfectCount); Check.Equal(0, round.MissCount);
            Check.Equal(plan.Calls.Count, round.Calls.Count);
            var missed = new RhythmRound(plan); missed.Advance(RhythmTime.Seconds(34, 120) + .13);
            Check.True(missed.MissCount > 0); Check.Equal(34, missed.Calls.Last().Tick);
            Check.Equal(38, missed.Notes.First(x => x.StartSeconds > missed.ElapsedSeconds).StartTick);
        }

        [Test]
        public void OnlySongsWithACompletePhaseRoundTripAcceptTheMonster()
        {
            var monster = Seesaw(); var supported = new List<string>();
            foreach (var music in MusicCatalog.All)
            {
                var stage = MusicStage.Generate(music); var candidates = monster.PatternPlanner.Candidates(stage, monster);
                if (candidates.Count > 0) supported.Add(music.Id);
                var proposed = BattlePlanner.Propose(stage, monster.Id, monster, 3);
                Check.Equal(candidates.Count > 0, proposed.Placements.Count > 0);
                foreach (var chain in proposed.Chains)
                {
                    Check.True(chain.Placements.Count >= 11); AssertUnbroken(chain);
                    Check.True(chain.Placements.All(x => x.StartTick + 4 <= music.TotalTicks));
                }
            }
            Check.True(supported.SequenceEqual(new[] { "rapid-drive" }));
            var withBreak = Stage(new[] { new MusicSection("A", 0, 2, 1), new MusicSection("BREAK", 2, 1, 1, false),
                new MusicSection("B", 3, 13, 1) });
            foreach (var chain in monster.PatternPlanner.Candidates(withBreak, monster))
                Check.True(chain.PhraseEndTick <= 32 || chain.Placements[0].StartTick >= 48);
        }

        [Test]
        public void AConflictWithdrawsTheWholeDependentChainAndKeepsAnUnrelatedChain()
        {
            var stage = SplitStage(); var monster = Seesaw(); var first = At(stage, monster, 16); var second = At(stage, monster, 128);
            var hold = new RhythmPattern("block", 4, new[] { new PatternStep(GestureKind.Hold, 0, 4) });
            var blocker = new MonsterDefinition("blocker", "Blocker", "", hold, new[] { new CallSignal(0, "Call") }, 4, 0, 1);
            var plan = BattlePlanner.Resolve(stage, new[]
            {
                new MonsterProposal(monster.Id, monster, new List<PatternChain> { first, second }),
                new MonsterProposal(blocker.Id, blocker, stage.FindPlacements(hold).Where(x => x.StartTick == 50))
            }, 1);
            Check.Equal(first.Placements.Count, plan.Withdrawals.Count);
            Check.True(plan.Withdrawals.All(x => ReferenceEquals(first, x.Attack.Chain)));
            Check.True(plan.Attacks.Where(x => x.MonsterId == monster.Id).All(x => ReferenceEquals(second, x.Chain)));
            Check.Equal(second.Placements.Count, plan.Attacks.Count(x => x.MonsterId == monster.Id));
            Check.False(plan.Calls.Any(x => plan.Withdrawals.Any(w => w.Attack.Id == x.AttackId)));
            AssertCompatible(plan);
        }

        [Test]
        public void GapFillingUsesCompletePhaseChainsAndNeverAnIsolatedTransition()
        {
            var stage = SplitStage(); var monster = Seesaw(); var original = Resolve(stage, monster, At(stage, monster, 128));
            var filled = BattleGapFiller.Fill(original, 2);
            Check.True(filled.GapFills.Count > 0);
            Check.True(original.Attacks.All(x => filled.Attacks.Contains(x)));
            foreach (var group in filled.Attacks.GroupBy(x => x.Chain))
            {
                Check.True(group.Key != null); Check.Equal(group.Key.Placements.Count, group.Count()); AssertUnbroken(group.Key);
                Check.Equal(0, group.Key.CallStartTick % 4);
                Check.True(group.Count(x => x.Pattern.Pattern.Steps.Count == 2) >= 2);
            }
            Check.True(ReferenceEquals(filled, BattleGapFiller.Fill(filled, 7))); AssertCompatible(filled);
        }

        [Test]
        public void CustomMonsterStrategyControlsBothProposalsAndGapFillOptions()
        {
            var stage = Stage(); var strategy = new SecondPatternOnly(); var source = MonsterCatalog.All.Single(x => x.Id == "tap-slime");
            var monster = new MonsterDefinition("custom", "Custom", "", GestureKind.Tap, source.Patterns, patternPlanner: strategy);
            Check.Equal(1, strategy.ValidationCalls);
            var proposal = BattlePlanner.Propose(stage, monster.Id, monster, 19);
            Check.Equal(1, strategy.ProposalCalls); Check.Equal(1, proposal.Placements.Count);
            Check.Equal(monster.Patterns[1].Pattern, proposal.Placements[0].Pattern); Check.Equal(64, proposal.Placements[0].StartTick);
            // Seed an ordinary attack, then ensure filling asks this strategy rather than independently using all patterns.
            var original = BattlePlanner.Resolve(stage, new[] { new MonsterProposal(monster.Id, monster,
                stage.FindPlacements(monster.Patterns[0].Pattern).Where(x => x.StartTick == 16)) }, 1);
            var filled = BattleGapFiller.Fill(original, 1);
            Check.Equal(1, filled.GapFills.Count); Check.Equal(monster.Patterns[1], filled.GapFills[0].Pattern);
            Check.True(strategy.CandidateCalls >= 2);
        }

        [Test]
        public void LinkedCallOverlapMustBeExplicitAndNormalIndependentAttacksStillConflict()
        {
            var stage = Stage(); var monster = Seesaw(); var chain = At(stage, monster, 16);
            Reject(() => new PatternChain("invalid", monster, chain.Placements, 4));
            Reject(() => new PatternChain("duplicate", monster, new[] { chain.Placements[0], chain.Placements[0] }, 4, true));
            var raw = new MonsterProposal(monster.Id, monster, chain.Placements.Take(2));
            Check.Equal(1, BattlePlanner.Resolve(stage, new[] { raw }, 1).Withdrawals.Count);
            var shortStage = Stage(new[] { new MusicSection("ONLY", 0, 3, 1) });
            Check.Equal(0, monster.PatternPlanner.Candidates(shortStage, monster).Count);
            var invalid = new MonsterDefinition("plain", "Plain", "", GestureKind.Tap, monster.Patterns.Take(1));
            Reject(() => new MonsterDefinition("invalid", "Invalid", "", GestureKind.Tap, invalid.Patterns, patternPlanner: new BeatShiftPlanner()));
        }

        [Test]
        public void WholeMonsterPlanningIsSeededAndIndependentOfPatternDefinitionOrder()
        {
            var stage = Stage(); var source = Seesaw();
            var reversed = new MonsterDefinition(source.Id, source.Name, "", source.MainGesture, source.Patterns.Reverse(),
                patternPlanner: new BeatShiftPlanner());
            for (int seed = 0; seed < 10; seed++)
            {
                var first = BattlePlanner.Propose(stage, "same", source, seed);
                var second = BattlePlanner.Propose(stage, "same", reversed, seed);
                Check.Equal(Fingerprint(first), Fingerprint(second));
                Check.Equal(Fingerprint(first), Fingerprint(BattlePlanner.Propose(stage, "same", source, seed)));
            }
        }

        [Test]
        public void VariableLengthSequencesStillPreferTheShortestWholePlacement()
        {
            var stage = Stage(); var source = MonsterCatalog.All.Single(x => x.Id == "tap-slime");
            var monster = new MonsterDefinition("variable", "Variable", "", GestureKind.Tap, source.Patterns,
                patternPlanner: new VariableLengthPlanner());
            var original = BattlePlanner.Resolve(stage, new[] { new MonsterProposal(monster.Id, monster,
                stage.FindPlacements(monster.Patterns[0].Pattern).Where(x => x.StartTick == 16)) }, 1);
            var filled = BattleGapFiller.Fill(original, 1);
            Check.Equal(2, filled.GapFills.Count);
            Check.Equal(36, filled.GapFills[0].Chain.CallStartTick);
            Check.Equal(48, filled.GapFills[0].ResponseStartTick);
            Check.Equal(80, filled.GapFills[1].ResponseStartTick);
            Check.True(ReferenceEquals(filled, BattleGapFiller.Fill(filled, 1)));
        }

        private sealed class VariableLengthPlanner : IMonsterPatternPlanner
        {
            public void Validate(IReadOnlyList<MonsterPatternDefinition> patterns) { }
            public IReadOnlyList<PatternChain> Candidates(MusicStage stage, MonsterDefinition monster)
            {
                PatternPlacement At(int i, int tick) => stage.FindPlacements(monster.Patterns[i].Pattern).Single(x => x.StartTick == tick);
                return new[] { new PatternChain("pair", monster, new[] { At(0, 32), At(1, 80) }, 4),
                    new PatternChain("pair", monster, new[] { At(0, 48), At(1, 80) }, 4) };
            }
            public MonsterProposal Propose(MusicStage stage, string instanceId, MonsterDefinition monster, int seed)
                => new MonsterProposal(instanceId, monster, new List<PatternChain> { Candidates(stage, monster)[0] });
        }

        private sealed class SecondPatternOnly : IMonsterPatternPlanner
        {
            public int ValidationCalls, CandidateCalls, ProposalCalls;
            public void Validate(IReadOnlyList<MonsterPatternDefinition> patterns) { ValidationCalls++; }
            public IReadOnlyList<PatternChain> Candidates(MusicStage stage, MonsterDefinition monster)
            {
                CandidateCalls++; var pattern = monster.Patterns[1];
                return stage.FindPlacements(pattern.Pattern).Where(x => x.StartTick == 64)
                    .Select(x => new PatternChain(pattern.Id, monster, new[] { x }, pattern.RestTicks)).ToArray();
            }
            public MonsterProposal Propose(MusicStage stage, string instanceId, MonsterDefinition monster, int seed)
            { ProposalCalls++; return new MonsterProposal(instanceId, monster, Candidates(stage, monster).ToList()); }
        }
        [Test]
        public void NekomataProposalsFillEachAvailableRunWithoutRandomRestartsOrMissingBeats()
        {
            var stage = MusicStage.Generate(MusicCatalog.All.Single(m => m.Id == "rapid-drive"));
            var monster = Seesaw();
            for (int seed = 0; seed < 12; seed++)
            {
                var proposal = BattlePlanner.Propose(stage, monster.Id, monster, seed);
                Check.Equal(1, proposal.Chains.Count); var chain = proposal.Chains[0];
                Check.Equal(12, chain.CallStartTick); AssertUnbroken(chain);
                Check.True(chain.PhraseEndTick >= stage.Music.TotalTicks - 2);
                var plan = BattlePlanner.Resolve(stage, new[] { proposal }, seed);
                var filled = BattleGapFiller.Fill(plan, seed);
                Check.Equal(proposal.Placements.Count, filled.Attacks.Count); Check.Equal(0, filled.Withdrawals.Count);
                AssertCompatible(filled);
            }
        }

        private static void AssertUnbroken(PatternChain chain)
        {
            var taps = chain.Placements.SelectMany(p => p.Pattern.Steps.Select(s => p.StartTick + s.OffsetTick)).OrderBy(t => t).ToArray();
            for (int i = 1; i < taps.Length; i++) Check.True(taps[i] - taps[i - 1] > 0 && taps[i] - taps[i - 1] <= 4);
        }
        private static MusicStage SplitStage() => Stage(new[] { new MusicSection("INTRO", 0, 1, 1, false),
            new MusicSection("A", 1, 6, 1), new MusicSection("BREAK", 7, 1, 1, false), new MusicSection("B", 8, 8, 1) });

        private static MonsterDefinition Seesaw() => MonsterCatalog.All.Single(x => x.Id == "seesaw-goblin");
        private static PatternChain At(MusicStage stage, MonsterDefinition monster, int call)
            => monster.PatternPlanner.Candidates(stage, monster).Single(x => x.CallStartTick == call);
        private static BattlePlan Resolve(MusicStage stage, MonsterDefinition monster, PatternChain chain)
            => BattlePlanner.Resolve(stage, new[] { new MonsterProposal(monster.Id, monster, new List<PatternChain> { chain }) }, 1);
        private static string Fingerprint(MonsterProposal proposal)
            => string.Join("|", proposal.Placements.Select(x => x.Pattern.Id + "@" + x.StartTick));
        private static void AssertCompatible(BattlePlan plan)
        {
            for (int i = 0; i < plan.Attacks.Count; i++) for (int j = i + 1; j < plan.Attacks.Count; j++)
                Check.False(BattlePlanner.Conflicts(plan.Attacks[i], plan.Attacks[j], out _));
        }
        private static void Reject(Action action)
        {
            try { action(); } catch (ArgumentException) { return; }
            throw new Exception("Expected invalid linked sequence to be rejected.");
        }
        private static MusicStage Stage(MusicSection[] sections = null)
        {
            var slots = Enumerable.Range(0, 8).SelectMany(i => new[] { new SlotTemplate(GestureKind.Tap, i * 2),
                new SlotTemplate(GestureKind.Hold, i * 2, 4) });
            return MusicStage.Generate(new MusicDefinition("chain", "Chain", 120, 4,
                sections ?? new[] { new MusicSection("INTRO", 0, 1, 1, false), new MusicSection("BODY", 1, 15, 1) }, new[] { slots }));
        }
    }
}
