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
                Proposal(stage, "first", 16, new PatternStep(GestureKind.Dive, 0, 8)),
                Proposal(stage, "second", 16, new PatternStep(GestureKind.Dive, 0, 8)),
                Proposal(stage, "third", 16, new PatternStep(GestureKind.Dive, 0, 8))
            }, 1);
            var round = new RhythmRound(plan);
            round.Press(2, 0, 0); Check.Equal(0, round.PerfectCount);
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
        public void OneQuickShakeCompletesWithoutSustainedMovementOrHolding()
        {
            var round = Round(new PatternStep(GestureKind.Shake, 0));
            round.Press(1.95, 0, 0); ShakeFor(round, 1.95, .1); round.Release(2.05, 0, 0);
            Check.True(round.Notes[0].ShakeCompleted); Check.Equal(1, round.Results.Count);
            Check.Equal(RhythmGrade.Perfect, round.Results.Single().Grade);
            Check.True(round.Results[0].JudgedAtSeconds < 2.05);
            Check.Equal(round.Notes[0].StartSeconds, round.Notes[0].EndSeconds);
            round.Advance(4); Check.Equal(1, round.Results.Count);
        }

        [Test]
        public void ShakeNeedsAFullRoundTripAndNeverAwardsPartialCredit()
        {
            foreach (double distance in new[] { 0, .01, .079, .08, .1 })
            {
                var round = Round(new PatternStep(GestureKind.Shake, 0));
                round.Press(2, 0, 0); MoveLine(round, 2, 0, 2.05, distance); round.Release(2.05, distance, 0);
                round.Advance(2.121);
                Check.False(round.Notes[0].ShakeCompleted); Check.Equal(RhythmGrade.Miss, round.Results.Single().Grade);
                Check.Equal(0, round.HalfMissCount); Check.Equal(4m, round.TotalDamageTaken);
            }
        }

        [Test]
        public void ShakeUsesTheWholeHalfMissWindowAsOneBinarySuccessWindow()
        {
            foreach (double start in new[] { 1.88, 1.96, 2.04 })
            {
                var round = Round(new PatternStep(GestureKind.Shake, 0));
                round.Press(start, 0, 0); ShakeFor(round, start, .07); round.Release(start + .07, 0, 0);
                Check.Equal(1, round.PerfectCount); Check.Equal(0, round.HalfMissCount);
            }
            var boundary = Round(new PatternStep(GestureKind.Shake, 0));
            boundary.Press(2, 0, 0); boundary.Move(2.04, .1, 0); boundary.Move(2 + boundary.HalfMissWindow, .035, 0);
            Check.Equal(1, boundary.PerfectCount);
            var late = Round(new PatternStep(GestureKind.Shake, 0));
            late.Press(2, 0, 0); late.Move(2.05, .1, 0); late.Move(2.13, .035, 0);
            Check.Equal(1, late.MissCount); Check.Equal(0, late.PerfectCount);
        }

        [Test]
        public void SeparateContactsCannotSpliceAnOutwardAndReturnButCompletionSurvivesPause()
        {
            var round = Round(new PatternStep(GestureKind.Shake, 0));
            round.Press(1.89, 0, 0); round.Move(1.93, .1, 0); round.Release(1.94, .1, 0);
            round.Press(1.95, .1, 0); round.Move(1.99, 0, 0); round.Release(2, 0, 0);
            Check.False(round.Notes[0].ShakeCompleted);
            round.Press(2.005, 0, 0); round.Move(2.04, .1, 0); round.Move(2.09, 0, 0); round.Release(2.09, 0, 0);
            Check.Equal(1, round.PerfectCount);
            Check.False(round.Suspend()); round.Advance(99); Near(2.09, round.ElapsedSeconds);
            round.Resume(false); round.Advance(3); Check.Equal(1, round.PerfectCount);
        }

        [Test]
        public void ShakeClipsMotionAtBothEndsAndCannotImproveAfterTheWindow()
        {
            var before = Round(new PatternStep(GestureKind.Shake, 0));
            before.Press(1.5, 0, 0); ShakeFor(before, 1.5, .1); before.Release(1.6, 0, 0); before.Advance(3);
            Check.Equal(1, before.MissCount); Near(0, before.Notes[0].ShakeProgress);
            var crossing = Round(new PatternStep(GestureKind.Shake, 0));
            crossing.Press(1.82, 0, 0); crossing.Move(1.89, .08, 0); crossing.Move(1.93, .16, 0); crossing.Move(2, .07, 0);
            Check.Equal(1, crossing.PerfectCount);
            double traveled = crossing.Notes[0].ShakeTravelDistance;
            ShakeFor(crossing, 2.2, .1); Near(traveled, crossing.Notes[0].ShakeTravelDistance);
            var late = Round(new PatternStep(GestureKind.Shake, 0));
            late.Press(2.13, 0, 0); ShakeFor(late, 2.13, .08);
            Check.Equal(1, late.MissCount); Check.False(late.Notes[0].ShakeCompleted);
        }

        [Test]
        public void OneShakeWorksAtDifferentFrameRatesAndAcrossTheOrigin()
        {
            foreach (int fps in new[] { 30, 60, 120 })
            {
                var round = Round(new PatternStep(GestureKind.Shake, 0));
                round.Press(1.91, 0, 0); ShakeFor(round, 1.91, .18, fps); round.Advance(3);
                Check.True(round.Notes[0].ShakeCompleted); Check.Equal(1, round.PerfectCount);
            }
            var crossing = Round(new PatternStep(GestureKind.Shake, 0));
            crossing.Press(2, 0, 0); crossing.Move(2.05, .1, 0); crossing.Move(2.1, -.1, 0); crossing.Advance(3);
            Check.Equal(1, crossing.PerfectCount);
        }

        [Test]
        public void ShakeDoesNotInventMotionAcrossMissingSamples()
        {
            var round = Round(new PatternStep(GestureKind.Shake, 0));
            round.Press(2, 0, 0); round.Move(2, .1, 0); round.Move(2.8, .2, 0); round.Release(3, 0, 0);
            Near(0, round.Notes[0].ShakeProgress); Check.Equal(1, round.MissCount);
        }

        [Test]
        public void EachShakeWindowRequiresItsOwnRoundTrip()
        {
            var round = Round(new PatternStep(GestureKind.Shake, 0), new PatternStep(GestureKind.Shake, 8));
            round.Press(2, 0, 0); ShakeFor(round, 2, .1); round.Release(2.1, 0, 0); round.Advance(3.5);
            Check.Equal(1, round.PerfectCount); Check.Equal(1, round.MissCount);
        }

        [Test]
        public void ShakeCompletionDoesNotForgiveAnEarlyHoldOrDiveRelease()
        {
            var round = Round(new PatternStep(GestureKind.Hold, 0, 8), new PatternStep(GestureKind.Dive, 0, 8),
                new PatternStep(GestureKind.Shake, 0));
            round.Press(2, 0, 0); ShakeFor(round, 2, .08); round.Release(2.75, 0, 0); round.Advance(3);
            Check.Equal(2, round.MissCount); Check.Equal(1, round.PerfectCount);
            Check.Equal(GestureKind.Shake, round.Results.Single(x => x.Grade == RhythmGrade.Perfect).Note.Step.Kind);
        }

        [Test]
        public void DiveShakeAndFlickShareAContinuousGesture()
        {
            var round = Round(new PatternStep(GestureKind.Dive, 0, 8),
                new PatternStep(GestureKind.Shake, 4), new PatternStep(GestureKind.Flick, 8));
            round.Press(2, 0, 0); round.Advance(2.5); ShakeFor(round, 2.5, .08);
            round.Move(2.94, 0, 0); round.Release(3, .1, 0);
            Check.Equal(3, round.PerfectCount); Check.False(round.IsDown);
        }

        [Test]
        public void MissingOneGestureDoesNotDiscardOtherSharedResults()
        {
            var round = Round(new PatternStep(GestureKind.Dive, 0, 8),
                new PatternStep(GestureKind.Shake, 4), new PatternStep(GestureKind.Flick, 8));
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
                var round = new RhythmRound(BattlePlanner.Generate(MusicStage.Generate(music), StageKind.Elite, 73, 3));
                round.Advance(music.DurationSeconds + 1); round.Advance(music.DurationSeconds + 2);
                Check.True(round.Finished); Check.Equal(round.Notes.Count, round.MissCount);
                Check.Equal(round.Notes.Count, round.Results.Count); Check.Equal(0.0, round.ScorePercent);
            }
        }

        [Test]
        public void PauseFreezesTimeAndRegrabDoesNotJudgeAnotherPress()
        {
            var round = Round(new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Dive, 0, 8),
                new PatternStep(GestureKind.Shake, 0));
            round.Press(2, 0, 0); MoveLine(round, 2, 0, 2.04, .1);
            Check.True(round.Suspend()); round.Advance(100); Check.Equal(2.04, round.ElapsedSeconds);
            bool rejected = false;
            try { round.Resume(false); } catch (InvalidOperationException) { rejected = true; }
            Check.True(rejected);
            round.Resume(true, .6, .5); MoveLine(round, 2.04, .6, 2.08, .5, y: .5); round.Release(3, .5, .5);
            Check.Equal(3, round.PerfectCount); Check.Equal(3, round.Results.Count);
            Check.True(round.Notes.Single(x => x.Step.Kind == GestureKind.Shake).ShakeCompleted);
        }

        [Test]
        public void StandalonePreviewDoesNotMutateRunHealthOrTicket()
        {
            var session = new RunSession(73);
            while (true)
            {
                session.Enter(session.Map.Nodes.First(x => session.CanEnter(x.Id)).Id);
                if (session.CurrentNode.IsBattle) break;
                session.LeaveService();
            }
            var plan = session.BattlePlan; string ticket = session.StageTicket; decimal health = session.Health;
            var round = new RhythmRound(plan); round.Advance(plan.Stage.Music.DurationSeconds + 1);
            Check.Equal(RunPhase.Stage, session.Phase); Check.Equal(ticket, session.StageTicket);
            Check.Equal(health, session.Health); Check.True(ReferenceEquals(plan, session.BattlePlan));
        }

        private static RhythmRound Round(params PatternStep[] steps)
        {
            var stage = Fixture();
            return new RhythmRound(BattlePlanner.Resolve(stage, new[] { Proposal(stage, "sample", 16, steps) }, 1));
        }

        private static void ShakeFor(RhythmRound round, double start, double duration, int fps = 60)
        {
            if (duration <= 0) return;
            MoveLine(round, start, 0, start + duration / 2, .1, fps);
            MoveLine(round, start + duration / 2, .1, start + duration, 0, fps);
        }

        private static void MoveLine(RhythmRound round, double from, double fromX, double to, double toX, int fps = 60, double y = 0)
        {
            int count = Math.Max(1, (int)Math.Ceiling((to - from) * fps));
            for (int i = 1; i <= count; i++)
            {
                double t = i / (double)count;
                round.Move(from + (to - from) * t, fromX + (toX - fromX) * t, y);
            }
        }

        private static void Near(double expected, double actual)
            => Check.True(Math.Abs(expected - actual) < 1e-8, "Expected " + expected + ", got " + actual);

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
                slots.Add(new SlotTemplate(GestureKind.Shake, tick));
                foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive })
                    foreach (int duration in new[] { 4, 8, 16 }) slots.Add(new SlotTemplate(kind, tick, duration));
            }
            return MusicStage.Generate(new MusicDefinition("fixture", "Fixture", bpm, 4, new[]
            { new MusicSection("INTRO", 0, 1, 1, false), new MusicSection("BODY", 1, 3, 1) }, new[] { slots }));
        }
    }
}
