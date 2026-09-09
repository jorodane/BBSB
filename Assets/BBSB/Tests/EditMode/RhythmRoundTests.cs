using System;
using System.Collections.Generic;
using System.Linq;
using BBSB.Core;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class RhythmRoundTests
    {
        [Test]
        public void TapGradesTimingAndCannotBeScoredTwice()
        {
            var perfect = Round(new PatternStep(GestureKind.Tap, 0));
            perfect.Press(2, 0, 0); perfect.Release(2.01, 0, 0); perfect.Press(2.02, 0, 0);
            Check.Equal(1, perfect.PerfectCount); Check.Equal(1, perfect.Results.Count);
            var half = Round(new PatternStep(GestureKind.Tap, 0));
            half.Press(2.1, 0, 0); Check.Equal(1, half.HalfMissCount); Check.Equal(.5, half.Results[0].Efficiency);
            var miss = Round(new PatternStep(GestureKind.Tap, 0));
            miss.Advance(2.2); miss.Press(2.21, 0, 0);
            Check.Equal(1, miss.MissCount); Check.Equal(MissReason.NoInput, miss.Results[0].Reason);
        }

        [Test]
        public void OnePressAndReleaseCanSatisfySeveralMonsters()
        {
            var stage = Fixture();
            var plan = BattlePlanner.Resolve(stage, new[]
            {
                Proposal(stage, "tap", 16, new PatternStep(GestureKind.Tap, 0)),
                Proposal(stage, "hold", 16, new PatternStep(GestureKind.Hold, 0, 8)),
                Proposal(stage, "dive", 16, new PatternStep(GestureKind.Dive, 0, 8))
            }, 1);
            var round = new RhythmRound(plan);
            round.Press(2, 0, 0); Check.Equal(1, round.PerfectCount);
            round.Release(3, 0, 0); Check.Equal(3, round.PerfectCount);
            Check.Equal(3, round.Results.Select(x => x.Note.Attack.MonsterId).Distinct().Count());
        }

        [Test]
        public void HoldFinishesWhileDownButAnEarlyReleaseFails()
        {
            var round = Round(new PatternStep(GestureKind.Hold, 0, 8));
            round.Press(2, 0, 0); round.Advance(3);
            Check.True(round.IsDown); Check.Equal(1, round.PerfectCount);
            round.Release(3.5, 0, 0); Check.Equal(1, round.Results.Count);
            var early = Round(new PatternStep(GestureKind.Hold, 0, 8));
            early.Press(2, 0, 0); early.Release(2.5, 0, 0);
            Check.Equal(MissReason.ReleasedEarly, early.Results[0].Reason);
            var nearEnd = Round(new PatternStep(GestureKind.Hold, 0, 8));
            nearEnd.Press(2, 0, 0); nearEnd.Release(2.9, 0, 0);
            Check.Equal(1, nearEnd.HalfMissCount);
        }

        [Test]
        public void DiveUsesBothPressAndReleaseAndRequiresTheRelease()
        {
            var round = Round(new PatternStep(GestureKind.Dive, 0, 8));
            round.Press(1.9, 0, 0); round.Release(3, 0, 0);
            Check.Equal(1, round.HalfMissCount);
            var held = Round(new PatternStep(GestureKind.Dive, 0, 8));
            held.Press(2, 0, 0); held.Advance(3); Check.Equal(0, held.Results.Count);
            held.Advance(3.2); Check.Equal(MissReason.ReleaseTiming, held.Results[0].Reason);
        }

        [Test]
        public void FlickNeedsFastRecentMovementWhenReleasing()
        {
            var round = Round(new PatternStep(GestureKind.Flick, 0));
            round.Press(1.4, 0, 0); round.Move(1.94, 0, 0); round.Release(2, .1, 0);
            Check.Equal(1, round.PerfectCount);
            var still = Round(new PatternStep(GestureKind.Flick, 0));
            still.Press(1.8, 0, 0); still.Release(2, 0, 0);
            Check.Equal(MissReason.MissingFlick, still.Results[0].Reason);
            var oldMovement = Round(new PatternStep(GestureKind.Flick, 0));
            oldMovement.Press(1, 0, 0); oldMovement.Move(1.4, .2, 0); oldMovement.Move(1.9, .2, 0);
            oldMovement.Release(2, .2, 0); Check.Equal(1, oldMovement.MissCount);
        }

        [Test]
        public void ShakeMustMoveOutAndBackWhileMaintainingContact()
        {
            var round = Round(new PatternStep(GestureKind.Shake, 0, 4));
            round.Press(1.7, 0, 0); round.Move(2, 0, 0);
            round.Move(2.1, .1, 0); round.Move(2.2, .01, 0); round.Advance(2.5);
            Check.Equal(1, round.PerfectCount);
            var still = Round(new PatternStep(GestureKind.Shake, 0, 4));
            still.Press(2, 0, 0); still.Advance(2.7);
            Check.Equal(MissReason.MissingShake, still.Results[0].Reason);
            var noReturn = Round(new PatternStep(GestureKind.Shake, 0, 4));
            noReturn.Press(2, 0, 0); noReturn.Move(2.1, .1, 0); noReturn.Release(2.5, .1, 0);
            Check.Equal(1, noReturn.MissCount);
        }

        [Test]
        public void ShakeLateReturnGetsReducedEfficiencyAndEarlyReleaseFails()
        {
            var late = Round(new PatternStep(GestureKind.Shake, 0, 4));
            late.Press(2, 0, 0); late.Move(2.1, .1, 0); late.Move(2.6, 0, 0);
            Check.Equal(1, late.HalfMissCount);
            var early = Round(new PatternStep(GestureKind.Shake, 0, 4));
            early.Press(2, 0, 0); early.Move(2.1, .1, 0); early.Release(2.2, 0, 0);
            Check.Equal(1, early.MissCount);
        }

        [Test]
        public void DiveShakeAndFlickShareAContinuousGesture()
        {
            var round = Round(new PatternStep(GestureKind.Dive, 0, 8),
                new PatternStep(GestureKind.Shake, 4, 4), new PatternStep(GestureKind.Flick, 8));
            round.Press(2, 0, 0); round.Move(2.5, 0, 0);
            round.Move(2.65, .1, 0); round.Move(2.8, 0, 0);
            round.Move(2.94, 0, 0); round.Release(3, .1, 0);
            Check.Equal(3, round.PerfectCount); Check.False(round.IsDown);
        }

        [Test]
        public void MissingOneGestureDoesNotDiscardOtherSharedResults()
        {
            var round = Round(new PatternStep(GestureKind.Dive, 0, 8),
                new PatternStep(GestureKind.Shake, 4, 4), new PatternStep(GestureKind.Flick, 8));
            round.Press(2, 0, 0); round.Move(2.5, 0, 0); round.Release(3, 0, 0);
            Check.Equal(1, round.PerfectCount); Check.Equal(2, round.MissCount);
            Check.Equal(GestureKind.Dive, round.Results.Single(x => x.Grade == RhythmGrade.Perfect).Note.Step.Kind);
        }

        [Test]
        public void BlankPreviousHalfBeatLatchesAnEarlyPressAsMissWithoutRetry()
        {
            var round = Round(new PatternStep(GestureKind.Tap, 0));
            round.Press(1.8, 0, 0); round.Release(1.81, 0, 0); round.Press(2, 0, 0);
            Check.Equal(1, round.MissCount); Check.Equal(MissReason.TooEarly, round.Results[0].Reason);
            Check.Equal(1, round.Results.Count);
        }

        [Test]
        public void OccupiedPreviousHalfBeatDoesNotPenalizeTheNextBeatTwice()
        {
            var round = Round(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 2));
            round.Press(2, 0, 0); round.Release(2.01, 0, 0); round.Press(2.05, 0, 0); round.Release(2.06, 0, 0);
            round.Press(2.25, 0, 0);
            Check.Equal(2, round.PerfectCount); Check.Equal(0, round.MissCount);
        }

        [Test]
        public void StrayInputOutsideIncomingHalfBeatIsIgnored()
        {
            var round = Round(new PatternStep(GestureKind.Tap, 0));
            round.Press(1, 0, 0); round.Release(1.1, 0, 0); round.Press(2, 0, 0);
            Check.Equal(1, round.PerfectCount); Check.Equal(0, round.MissCount);
        }

        [Test]
        public void WindowsShrinkAtHighTempoAndKeepBoundaryInputs()
        {
            var stage = Fixture(168); var round = new RhythmRound(BattlePlanner.Resolve(stage,
                new[] { Proposal(stage, "fast", 16, new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 2)) }, 1));
            Check.True(round.HalfMissWindow * 2 < round.BeatSeconds * .5);
            double start = round.Notes[0].StartSeconds;
            round.Press(start + round.HalfMissWindow, 0, 0);
            Check.Equal(1, round.HalfMissCount);
        }

        [Test]
        public void CallsAreDeliveredInOrderOnceAcrossLongFrames()
        {
            var round = Round(new PatternStep(GestureKind.Tap, 0));
            round.Advance(1.49); Check.Equal(0, round.Calls.Count);
            round.Advance(1.5); Check.Equal(1, round.Calls.Count);
            round.Advance(5); round.Advance(5);
            Check.Equal(round.Plan.Calls.Count, round.Calls.Count);
            Check.Equal(1, round.MissCount);
        }

        [Test]
        public void RoundEndWaitsForTheLastReleaseWindowAndDoesNotMutateThePlan()
        {
            var stage = Fixture(); var plan = BattlePlanner.Resolve(stage,
                new[] { Proposal(stage, "last", 56, new PatternStep(GestureKind.Dive, 0, 8)) }, 1);
            var round = new RhythmRound(plan);
            round.Press(7, 0, 0); round.Advance(8); Check.False(round.Finished);
            round.Release(8 + round.HalfMissWindow, 0, 0); Check.Equal(1, round.HalfMissCount);
            round.Advance(8.2); Check.True(round.Finished); Check.Equal(50.0, round.ScorePercent);
            var replay = new RhythmRound(plan);
            Check.True(ReferenceEquals(round.Plan, replay.Plan)); Check.Equal(0, replay.Results.Count);
            Check.Equal(ResponseState.Pending, replay.Notes[0].State);
        }

        [Test]
        public void NoInputFinishesWithOneMissPerNoteEvenAfterAClockJump()
        {
            foreach (var music in MusicCatalog.All)
            {
                var round = new RhythmRound(BattlePlanner.Generate(MusicStage.Generate(music), StageKind.Elite, 73));
                round.Advance(music.DurationSeconds + 1); round.Advance(music.DurationSeconds + 2);
                Check.True(round.Finished); Check.Equal(round.Notes.Count, round.MissCount);
                Check.Equal(round.Notes.Count, round.Results.Count); Check.Equal(0.0, round.ScorePercent);
            }
        }

        [Test]
        public void PauseFreezesTimeAndRegrabDoesNotJudgeAnotherPress()
        {
            var round = Round(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Dive, 0, 8),
                new PatternStep(GestureKind.Shake, 0, 8));
            round.Press(2, 0, 0); round.Move(2.2, .1, 0);
            Check.True(round.Suspend()); round.Advance(100); Check.Equal(2.2, round.ElapsedSeconds);
            bool rejected = false;
            try { round.Resume(false); } catch (InvalidOperationException) { rejected = true; }
            Check.True(rejected);
            round.Resume(true, .6, .5); round.Move(2.5, .5, .5); round.Release(3, .5, .5);
            Check.Equal(3, round.PerfectCount); Check.Equal(3, round.Results.Count);
        }

        [Test]
        public void SongCompletionLeavesRunInSameEncounterWithSameHealthAndTicket()
        {
            var session = new RunSession(73);
            while (true)
            {
                session.Enter(session.Map.Nodes.First(x => session.CanEnter(x.Id)).Id);
                if (session.CurrentNode.IsBattle) break;
                session.LeaveService();
            }
            var plan = session.BattlePlan; string ticket = session.StageTicket; int health = session.Health;
            var round = new RhythmRound(plan); round.Advance(plan.Stage.Music.DurationSeconds + 1);
            Check.Equal(RunPhase.Stage, session.Phase); Check.Equal(ticket, session.StageTicket);
            Check.Equal(health, session.Health); Check.True(ReferenceEquals(plan, session.BattlePlan));
        }

        private static RhythmRound Round(params PatternStep[] steps)
        {
            var stage = Fixture();
            return new RhythmRound(BattlePlanner.Resolve(stage, new[] { Proposal(stage, "sample", 16, steps) }, 1));
        }

        private static MonsterProposal Proposal(MusicStage stage, string id, int tick, params PatternStep[] steps)
        {
            var pattern = new RhythmPattern(id, 4, steps);
            var monster = new MonsterDefinition(id, id, "", pattern, new[] { new CallSignal(0, "call") },
                Math.Max(4, pattern.EndOffsetTick), 4, 1);
            return new MonsterProposal(id, monster, stage.FindPlacements(pattern).Where(x => x.StartTick == tick));
        }

        private static MusicStage Fixture(double bpm = 120)
        {
            var slots = new List<SlotTemplate>();
            for (int tick = 0; tick < 16; tick += 2)
            {
                slots.Add(new SlotTemplate(GestureKind.Tap, tick)); slots.Add(new SlotTemplate(GestureKind.Flick, tick));
                foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive, GestureKind.Shake })
                    foreach (int duration in new[] { 4, 8, 16 }) slots.Add(new SlotTemplate(kind, tick, duration));
            }
            return MusicStage.Generate(new MusicDefinition("fixture", "Fixture", bpm, 4, new[]
            { new MusicSection("INTRO", 0, 1, 1, false), new MusicSection("BODY", 1, 3, 1) }, new[] { slots }));
        }
    }
}
