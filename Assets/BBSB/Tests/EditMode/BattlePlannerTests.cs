using System;
using System.Collections.Generic;
using System.Linq;
using BBSB.Core;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class BattlePlannerTests
    {
        [Test]
        public void SampleEncountersHaveCompleteCalledPatternsAndNoInputConflicts()
        {
            var seen = new HashSet<string>();
            foreach (var music in MusicCatalog.All)
            {
                var stage = MusicStage.Generate(music);
                for (int field = 1; field <= 3; field++)
                foreach (StageKind kind in new[] { StageKind.Monster, StageKind.Elite, StageKind.Boss })
                for (int seed = 0; seed < 40; seed++)
                {
                    var plan = BattlePlanner.Generate(stage, kind, seed, field);
                    Check.True(ReferenceEquals(stage, plan.Stage));
                    Check.True(plan.Monsters.Count > 0 && plan.Monsters.Count <= field);
                    Check.Equal(plan.Attacks.Count, plan.Attacks.Select(x => x.Id).Distinct().Count());
                    Check.Equal(plan.Attacks.Sum(x => x.Call.Count), plan.Calls.Count);
                    Check.True(plan.Calls.Select(x => x.Tick).SequenceEqual(plan.Calls.Select(x => x.Tick).OrderBy(x => x)));
                    foreach (var monster in plan.Monsters)
                    {
                        seen.Add(monster.Monster.Id); Check.True(monster.Attacks.Count > 0);
                        var starts = monster.Monster.Patterns.ToDictionary(x => x.Id, x => stage.FindPlacements(x.Pattern).Select(y => y.StartTick).ToHashSet());
                        long responseAfter = 0, callAfter = 0;
                        foreach (var attack in monster.Attacks)
                        {
                            Check.True(starts[attack.Pattern.Id].Contains(attack.ResponseStartTick));
                            Check.Equal(attack.Placement.Pattern.Steps.Count, attack.Placement.Slots.Count);
                            Check.True(attack.CallStartTick >= callAfter && attack.ResponseStartTick >= responseAfter);
                            Check.True(attack.ResponseEndTick <= music.TotalTicks);
                            Check.True((long)attack.ResponseStartTick + attack.Pattern.ResponseTicks <= music.TotalTicks);
                            foreach (var call in attack.Call)
                            {
                                Check.True(call.Tick >= attack.CallStartTick && call.Tick < attack.ResponseStartTick);
                                Check.Equal(attack.Id, call.AttackId); Check.Equal(monster.InstanceId, call.MonsterId);
                            }
                            callAfter = (long)attack.ResponseStartTick + attack.Pattern.ResponseTicks;
                            responseAfter = callAfter + attack.Pattern.RestTicks;
                        }
                    }
                    for (int i = 0; i < plan.Attacks.Count; i++) for (int j = i + 1; j < plan.Attacks.Count; j++)
                        Check.False(BattlePlanner.Conflicts(plan.Attacks[i], plan.Attacks[j], out _));
                    foreach (var withdrawal in plan.Withdrawals)
                    {
                        Check.True(withdrawal.YieldingOccupiedBeats >= withdrawal.KeptOccupiedBeats);
                        Check.False(plan.Attacks.Contains(withdrawal.Attack));
                        // Later arbitration can remove the old blocker. Only a newly validated gap fill may reuse that time.
                        Check.False(plan.Attacks.Any(x => x.Id == withdrawal.Attack.Id && !plan.GapFills.Contains(x)));
                        if (!plan.GapFills.Any(x => x.Id == withdrawal.Attack.Id))
                            Check.False(plan.Calls.Any(x => x.AttackId == withdrawal.Attack.Id));
                    }
                }
            }
            Check.Equal(9, MonsterCatalog.All.Count); Check.Equal(9, seen.Count);
        }

        [Test]
        public void WholeTripleTapPhraseIsFollowedByOneRestBeat()
        {
            var monster = TripleTap();
            var proposal = BattlePlanner.Propose(MusicStage.Generate(MusicCatalog.All[0]), "slime", monster, 7);
            Check.True(proposal.Placements.Count > 3);
            for (int i = 0; i < proposal.Placements.Count; i++)
            {
                var placement = proposal.Placements[i];
                Check.Equal(16 + i * 16, placement.StartTick);
                Check.Equal(3, placement.Slots.Count);
                Check.True(placement.Slots.Select(x => x.StartTick - placement.StartTick).SequenceEqual(new[] { 0, 4, 8 }));
            }
        }

        [Test]
        public void WeightedHighlightsAreMoreLikelyWithoutBeingMandatory()
        {
            var stage = MusicStage.Generate(MusicCatalog.All[0]);
            var monster = Monster("sparse", new[] { new PatternStep(GestureKind.Tap, 0) }, 4, chance: .06);
            int groove = 0, highlight = 0; bool skippedHighlight = false;
            for (int seed = 1; seed <= 250; seed++)
            {
                var proposal = BattlePlanner.Propose(stage, "sparse", monster, seed);
                Check.True(proposal.Placements.Count > 0);
                int inHighlight = 0;
                foreach (var attack in proposal.Placements)
                {
                    string section = stage.Music.SectionAtBar(attack.StartTick / 16).Name;
                    if (section == "GROOVE") groove++;
                    if (section == "HIGHLIGHT") { highlight++; inHighlight++; }
                }
                skippedHighlight |= inHighlight == 0;
            }
            Check.True(highlight / 4.0 > groove / 6.0 * 1.4, "Normalize by each section's bar count.");
            Check.True(skippedHighlight, "Highlight weighting must not force a highlight attack.");
        }

        [Test]
        public void DifferentInputKindsConflictEvenWhenOneFingerCouldPerformBoth()
        {
            var stage = Fixture();
            foreach (GestureKind left in Enum.GetValues(typeof(GestureKind)))
            foreach (GestureKind right in Enum.GetValues(typeof(GestureKind)))
            {
                int leftDuration = left == GestureKind.Tap || left == GestureKind.Flick ? 0 : 8;
                int rightDuration = right == GestureKind.Tap || right == GestureKind.Flick ? 0 : 8;
                var a = At(stage, Monster("a", new[] { new PatternStep(left, 0, leftDuration) }, 8), 16);
                var b = At(stage, Monster("b", new[] { new PatternStep(right, 0, rightDuration) }, 8), 16);
                Check.Equal(left != right, InputCompatibility.Conflict(a, b, out int tick));
                Check.Equal(left != right ? 16 : -1, tick);
            }
            var hold = At(stage, Monster("hold", new[] { new PatternStep(GestureKind.Hold, 0, 8) }, 8), 16);
            var shake = At(stage, Monster("shake", new[] { new PatternStep(GestureKind.Shake, 0, 4) }, 4), 20);
            Check.True(InputCompatibility.Conflict(hold, shake, out int overlap)); Check.Equal(20, overlap);
            var flick = Monster("flick", new[] { new PatternStep(GestureKind.Flick, 0) }, 4);
            Check.True(InputCompatibility.Conflict(hold, At(stage, flick, 24), out int ending)); Check.Equal(24, ending);
            Check.False(InputCompatibility.Conflict(hold, At(stage, flick, 28), out _));
        }

        [Test]
        public void SameInputKindStillRequiresCompatibleHoldAndReleaseTiming()
        {
            var stage = Fixture();
            foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive })
            {
                var monster = Monster("held", new[] { new PatternStep(kind, 0, 8) }, 8);
                var held = At(stage, monster, 16);
                Check.False(InputCompatibility.Conflict(held, At(stage, monster, 16), out _));
                Check.True(InputCompatibility.Conflict(held, At(stage, monster, 20), out int at)); Check.Equal(20, at);
                Check.True(InputCompatibility.Conflict(held, At(stage, monster, 24), out _));
                var shorter = Monster("short", new[] { new PatternStep(kind, 0, 4) }, 4);
                Check.Equal(kind == GestureKind.Dive, InputCompatibility.Conflict(held, At(stage, shorter, 16), out _));
            }
            var shake = Monster("shake", new[] { new PatternStep(GestureKind.Shake, 0, 8) }, 8);
            Check.False(InputCompatibility.Conflict(At(stage, shake, 16), At(stage, shake, 20), out _));
        }

        [Test]
        public void ExtraFlickCannotInterruptTapTapFlickButCanShareItsFlickEnding()
        {
            var stage = Fixture(2);
            var bat = Monster("bat", new[] { new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 2), new PatternStep(GestureKind.Flick, 6) }, 8);
            var flick = Monster("flick", new[] { new PatternStep(GestureKind.Flick, 0) }, 4);
            var phrase = At(stage, bat, 32); // Tap 32, Tap 34, Flick 38.
            foreach (int tick in new[] { 32, 34, 36 })
            {
                var extra = At(stage, flick, tick);
                Check.True(InputCompatibility.Conflict(phrase, extra, out int at)); Check.Equal(tick, at);
                Check.True(InputCompatibility.Conflict(extra, phrase, out int reverse)); Check.Equal(at, reverse);
            }
            foreach (int tick in new[] { 28, 38, 40 })
                Check.False(InputCompatibility.Conflict(phrase, At(stage, flick, tick), out _));
            var shared = BattlePlanner.Resolve(stage, new[] { Proposal(stage, bat, 32), Proposal(stage, flick, 38) }, 1);
            Check.Equal(2, shared.Monsters.Count); Check.Equal(0, shared.Withdrawals.Count);
            var interrupted = BattlePlanner.Resolve(stage, new[] { Proposal(stage, bat, 32), Proposal(stage, flick, 36) }, 1);
            Check.Equal(1, interrupted.Withdrawals.Count);
            Check.Equal(bat.Id, interrupted.Withdrawals[0].Attack.MonsterId);
            Check.Equal(3, interrupted.Withdrawals[0].Attack.Placement.Slots.Count);
            Check.True(interrupted.Calls.All(x => x.MonsterId == flick.Id));
        }

        [Test]
        public void AuthoredCompoundPatternsRemainPlayableAndSharedPositionsCountOnce()
        {
            var stage = Fixture();
            var compound = Monster("compound", new[]
            {
                new PatternStep(GestureKind.Dive, 0, 8), new PatternStep(GestureKind.Tap, 0),
                new PatternStep(GestureKind.Shake, 4, 4), new PatternStep(GestureKind.Flick, 8)
            }, 8);
            Check.True(InputCompatibility.IsPlayable(compound.Patterns[0].Pattern));
            foreach (var monster in MonsterCatalog.All) foreach (var pattern in monster.Patterns) Check.True(InputCompatibility.IsPlayable(pattern.Pattern));
            var plan = BattlePlanner.Resolve(stage, new[] { Proposal(stage, compound, 16) }, 1);
            Check.Equal(0, plan.Withdrawals.Count); Check.Equal(1, plan.Attacks.Count);
            Check.Equal(3, plan.Monsters.Single(x => x.InstanceId == "compound").OccupiedBeatCount); // 16, 20, 24; not five steps + cues.
        }

        [Test]
        public void MoreOccupiedBeatsYieldsTheWholeBundleEvenWithFewerOccurrences()
        {
            var stage = Fixture();
            var triple = TripleTap();
            var flick = Monster("flick", new[] { new PatternStep(GestureKind.Flick, 0) }, 4);
            var plan = BattlePlanner.Resolve(stage, new[] { Proposal(stage, triple, 16), Proposal(stage, flick, 20, 36) }, 1);
            Check.Equal(1, plan.Withdrawals.Count);
            var removed = plan.Withdrawals[0];
            Check.Equal(triple.Id, removed.Attack.MonsterId);
            Check.Equal(3, removed.Attack.Placement.Slots.Count);
            Check.Equal(3, removed.YieldingOccupiedBeats); Check.Equal(2, removed.KeptOccupiedBeats);
            Check.Equal(2, plan.Attacks.Count); Check.Equal(1, plan.Monsters.Count);
            Check.True(plan.Calls.All(x => x.MonsterId == flick.Id));
        }

        [Test]
        public void OccupancyIsRecountedAfterEachWithdrawal()
        {
            var stage = Fixture();
            var triple = TripleTap();
            var flick = Monster("flick", new[] { new PatternStep(GestureKind.Flick, 0) }, 4);
            var plan = BattlePlanner.Resolve(stage, new[] { Proposal(stage, triple, 16, 48), Proposal(stage, flick, 20, 36, 52, 68) }, 1);
            Check.Equal(2, plan.Withdrawals.Count);
            Check.Equal(triple.Id, plan.Withdrawals[0].Attack.MonsterId);
            Check.Equal(6, plan.Withdrawals[0].YieldingOccupiedBeats); Check.Equal(4, plan.Withdrawals[0].KeptOccupiedBeats);
            Check.Equal(flick.Id, plan.Withdrawals[1].Attack.MonsterId);
            Check.Equal(4, plan.Withdrawals[1].YieldingOccupiedBeats); Check.Equal(3, plan.Withdrawals[1].KeptOccupiedBeats);
            Check.Equal(1, plan.Monsters.Single(x => x.InstanceId == triple.Id).Attacks.Count);
            Check.Equal(3, plan.Monsters.Single(x => x.InstanceId == flick.Id).Attacks.Count);
        }

        [Test]
        public void TiedConflictsAreSeededAndDoNotDependOnProposalEnumerationOrder()
        {
            var stage = Fixture();
            var tap = Proposal(stage, Monster("tap", new[] { new PatternStep(GestureKind.Tap, 0) }, 4), 16);
            var flick = Proposal(stage, Monster("flick", new[] { new PatternStep(GestureKind.Flick, 0) }, 4), 16);
            var winners = new HashSet<string>();
            for (int seed = 0; seed < 20; seed++)
            {
                var a = BattlePlanner.Resolve(stage, new[] { tap, flick }, seed);
                var b = BattlePlanner.Resolve(stage, new[] { flick, tap }, seed);
                Check.Equal(Fingerprint(a), Fingerprint(b)); winners.Add(a.Monsters.Single().InstanceId);
            }
            Check.Equal(2, winners.Count);
        }

        [Test]
        public void PlansAreReproducibleAndIndependentOfOtherEncounterGeneration()
        {
            var stage = MusicStage.Generate(MusicCatalog.All[0]);
            var expected = BattlePlanner.Generate(stage, StageKind.Elite, 73, 3);
            BattlePlanner.Generate(MusicStage.Generate(MusicCatalog.All[3]), StageKind.Monster, 9);
            var actual = BattlePlanner.Generate(stage, StageKind.Elite, 73, 3);
            Check.Equal(Fingerprint(expected), Fingerprint(actual));
            var fingerprints = new HashSet<string>();
            for (int seed = 0; seed < 10; seed++) fingerprints.Add(Fingerprint(BattlePlanner.Generate(stage, StageKind.Elite, seed, 3)));
            Check.True(fingerprints.Count > 1);
        }

        [Test]
        public void InvalidCallsUnplayablePatternsAndForeignSlotsAreRejected()
        {
            var tap = new RhythmPattern("tap", 4, new[] { new PatternStep(GestureKind.Tap, 0) });
            Throws(() => new MonsterDefinition("bad", "Bad", "", tap, new[] { new CallSignal(4, "late") }, 4, 4, .3));
            Throws(() => Monster("bad", new[] { new PatternStep(GestureKind.Dive, 0, 8), new PatternStep(GestureKind.Tap, 4) }, 8));
            Throws(() => Monster("bad", new[] { new PatternStep(GestureKind.Dive, 0, 8), new PatternStep(GestureKind.Shake, 4, 8) }, 12));
            var stage = Fixture(); var monster = Monster("tap", tap.Steps, 4);
            Throws(() => BattlePlanner.Generate(stage, StageKind.Monster, 1, 0));
            Throws(() => BattlePlanner.Generate(stage, StageKind.Monster, 1, -1));
            Throws(() => BattlePlanner.Resolve(Fixture(), new[] { Proposal(stage, monster, 16) }, 1));
            Check.Equal(1, BattlePlanner.Resolve(stage, new[] { Proposal(stage, monster, 16, 20) }, 1).Withdrawals.Count);
            Throws(() => BattlePlanner.Resolve(stage, new[] { Proposal(stage, monster, 16), Proposal(stage, monster, 32) }, 1));
        }

        [Test]
        public void RunEntryOwnsThePlanUntilTheBattleActuallyFinishes()
        {
            var run = new RunSession(73); Check.True(run.BattlePlan == null);
            EnterBattle(run); var plan = run.BattlePlan;
            Check.True(plan != null && ReferenceEquals(plan.Stage, run.BattleMusic));
            Check.False(run.ResolveBattle("stale", true, run.Health)); Check.True(ReferenceEquals(plan, run.BattlePlan));
            foreach (var monster in plan.Monsters) foreach (var pattern in monster.Monster.Patterns) run.BattleMusic.FindPlacements(pattern.Pattern).ToArray();
            Check.True(ReferenceEquals(plan, run.BattlePlan));
            Check.True(run.ResolveBattle(run.StageTicket, true, run.Health)); Check.True(run.BattlePlan == null);
            run.Restart(73); EnterBattle(run); Check.Equal(Fingerprint(plan), Fingerprint(run.BattlePlan));
            Check.True(run.ResolveBattle(run.StageTicket, false, 0)); Check.True(run.BattlePlan == null);
            run.Restart(73); EnterBattle(run); run.Abandon(); Check.True(run.BattlePlan == null);
            run.Restart(73); EnterBattle(run); run.Restart(73); Check.True(run.BattlePlan == null);
        }

        [Test]
        public void RunProgressRaisesMonsterLimitWithoutAnEarlyEliteOrBossSpike()
        {
            var largest = new int[4];
            var openingKinds = new HashSet<StageKind>();
            for (int seed = 0; seed < 30; seed++)
            {
                var run = new RunSession(seed);
                for (int field = 1; field <= 4; field++)
                {
                    while (run.Phase != RunPhase.FieldCleared)
                    {
                        Check.True(run.Enter(run.Map.Nodes.First(x => run.CanEnter(x.Id)).Id));
                        if (run.CurrentNode.IsBattle)
                        {
                            int count = run.BattlePlan.Monsters.Count;
                            Check.True(count > 0 && count <= Math.Min(field, 3));
                            largest[field - 1] = Math.Max(largest[field - 1], count);
                            if (field == 1) { Check.Equal(1, count); openingKinds.Add(run.CurrentNode.Kind); }
                            Check.True(run.ResolveBattle(run.StageTicket, true, run.Health)); Check.True(run.SkipReward());
                        }
                        else Check.True(run.LeaveService());
                    }
                    Check.True(run.AdvanceField());
                }
                run.Restart(seed); EnterBattle(run); Check.Equal(1, run.BattlePlan.Monsters.Count);
            }
            Check.True(largest.SequenceEqual(new[] { 1, 2, 3, 3 }));
            foreach (var kind in new[] { StageKind.Monster, StageKind.Elite, StageKind.Boss }) Check.True(openingKinds.Contains(kind));
        }

        private static void EnterBattle(RunSession run)
        {
            while (true)
            {
                Check.True(run.Enter(run.Map.Nodes.First(x => run.CanEnter(x.Id)).Id));
                if (run.CurrentNode.IsBattle) return;
                Check.True(run.BattlePlan == null); Check.True(run.LeaveService());
            }
        }

        private static MonsterDefinition TripleTap() => Monster("triple", new[] { new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4), new PatternStep(GestureKind.Tap, 8) }, 12);

        private static MonsterDefinition Monster(string id, IEnumerable<PatternStep> steps, int responseTicks, double chance = 1)
        {
            return new MonsterDefinition(id, id, "", new RhythmPattern(id, 4, steps), new[] { new CallSignal(0, "call") }, responseTicks, 4, chance);
        }

        private static PatternPlacement At(MusicStage stage, MonsterDefinition monster, int tick)
            => stage.FindPlacements(monster.Patterns[0].Pattern).Single(x => x.StartTick == tick);
        private static MonsterProposal Proposal(MusicStage stage, MonsterDefinition monster, params int[] ticks)
            => new MonsterProposal(monster.Id, monster, ticks.Select(x => At(stage, monster, x)));

        private static MusicStage Fixture(int stepTicks = 4)
        {
            var slots = new List<SlotTemplate>();
            for (int tick = 0; tick < 16; tick += stepTicks)
            {
                slots.Add(new SlotTemplate(GestureKind.Tap, tick)); slots.Add(new SlotTemplate(GestureKind.Flick, tick));
                foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive, GestureKind.Shake })
                    foreach (int duration in new[] { 4, 8, 16 }) slots.Add(new SlotTemplate(kind, tick, duration));
            }
            return MusicStage.Generate(new MusicDefinition("fixture", "Fixture", 120, 4, new[]
            { new MusicSection("INTRO", 0, 1, 1, false), new MusicSection("BODY", 1, 6, 1) }, new[] { slots }));
        }

        private static string Fingerprint(BattlePlan plan) => string.Join("|", plan.Attacks.Select(x => x.Id)) + ":" +
            string.Join("|", plan.Withdrawals.Select(x => x.Attack.Id + ">" + x.KeptMonsterId));

        private static void Throws(Action action)
        {
            try { action(); } catch (ArgumentException) { return; }
            throw new Exception("Expected an argument error.");
        }
    }
}
