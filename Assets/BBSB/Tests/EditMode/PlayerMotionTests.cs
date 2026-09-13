using System;
using System.Collections.Generic;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class PlayerMotionTests
    {
        [Test]
        public void RealJudgmentsSelectEveryGestureAndAllThreeOutcomes()
        {
            foreach (GestureKind kind in Enum.GetValues(typeof(GestureKind)))
            foreach (RhythmGrade grade in Enum.GetValues(typeof(RhythmGrade)))
            {
                if (kind == GestureKind.Shake && grade == RhythmGrade.HalfMiss) continue;
                var round = Single(kind); var timeline = new PlayerMotionTimeline(1);
                Judge(round, kind, grade);
                var result = round.Results.Single(); var frame = timeline.Evaluate(round);
                if (kind == GestureKind.Tap && result.Reason != MissReason.NoInput)
                {
                    Check.Equal(PlayerMotionPhase.Prepare, frame.Phase); Check.Equal(0, frame.Index % 4);
                    round.Advance(result.JudgedAtSeconds + .04); frame = timeline.Evaluate(round);
                }
                Check.Equal(grade, result.Grade);
                Check.Equal(kind, frame.Kind.Value); Check.Equal(grade, frame.Grade.Value);
                Check.Equal(PlayerMotionPhase.Impact, frame.Phase);
                Check.Equal(PlayerMotionTimeline.SheetFor(kind), frame.Sheet);
                int expected = kind == GestureKind.Tap ? frame.Punch * 4 + (grade == RhythmGrade.Perfect ? 1 : grade == RhythmGrade.HalfMiss ? 2 : 3) :
                    kind == GestureKind.Dive || kind == GestureKind.Flick ? (grade == RhythmGrade.Perfect ? 1 : grade == RhythmGrade.HalfMiss ? 2 : 3) :
                    grade == RhythmGrade.Perfect ? 2 : grade == RhythmGrade.HalfMiss ? 3 : 4;
                Check.Equal(expected, frame.Index);
                // Presentation never modifies grades, counts or incoming damage.
                decimal damage = round.TotalDamageTaken; int count = round.Results.Count;
                for (int i = 0; i < 10; i++) Check.Equal(frame.Index, timeline.Evaluate(round).Index);
                Check.Equal(count, round.Results.Count); Check.Equal(damage, round.TotalDamageTaken);
                double judgedAt = result.JudgedAtSeconds;
                round.Advance(judgedAt + .119);
                Check.Equal(PlayerMotionPhase.Impact, timeline.Evaluate(round).Phase);
                round.Advance(judgedAt + .121);
                var transition = timeline.Evaluate(round);
                bool faster = kind == GestureKind.Tap || kind == GestureKind.Dive || kind == GestureKind.Flick;
                Check.Equal(faster ? PlayerMotionPhase.Recover : PlayerMotionPhase.Impact, transition.Phase);
                Check.Equal(grade, transition.Grade.Value);
                Check.Equal(count, round.Results.Count); Check.Equal(damage, round.TotalDamageTaken);
            }
        }

        [Test]
        public void SharedPunchesSelectOnceAndNeverRepeatAcrossConsecutiveActions()
        {
            var round = Round(3, new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4),
                new PatternStep(GestureKind.Tap, 8), new PatternStep(GestureKind.Tap, 12));
            var timeline = new PlayerMotionTimeline(7); int previous = -1;
            for (int i = 0; i < 4; i++)
            {
                double time = 2 + i * .5;
                round.Press(time + (i % 2 == 0 ? 0 : .1), 0, 0);
                var frame = timeline.Evaluate(round);
                Check.Equal(true, frame.Punch >= 0 && frame.Punch <= 2 && frame.Punch != previous);
                previous = frame.Punch;
                Check.Equal(i + 1, timeline.PunchSelections); Check.Equal((i + 1) * 3, round.Results.Count);
                for (int repeat = 0; repeat < 5; repeat++) Check.Equal(frame.Punch, timeline.Evaluate(round).Punch);
                round.Release(time + .11, 0, 0);
            }
        }

        [Test]
        public void EveryPunchVariantRetainsItsOwnGlanceAndTorsoHit()
        {
            var seen = new HashSet<int>();
            for (int seed = 0; seed < 20; seed++)
            foreach (var grade in new[] { RhythmGrade.Perfect, RhythmGrade.HalfMiss, RhythmGrade.Miss })
            {
                var round = Single(GestureKind.Tap); var timeline = new PlayerMotionTimeline(seed);
                Judge(round, GestureKind.Tap, grade); var strike = timeline.Evaluate(round); seen.Add(strike.Punch);
                Check.Equal(strike.Punch, strike.Index / 4);
                round.Advance(round.ElapsedSeconds + .125); var recover = timeline.Evaluate(round);
                Check.Equal(strike.Punch, recover.Punch); Check.Equal(strike.Punch * 4, recover.Index);
            }
            Check.Equal(3, seen.Count);
        }

        [Test]
        public void FallsSitOnlyWhenTheNextResponseLeavesEnoughTime()
        {
            foreach (var kind in new[] { GestureKind.Dive, GestureKind.Flick })
            {
                var longGap = Single(kind); var a = new PlayerMotionTimeline();
                longGap.Advance(2.121); Check.Equal(3, a.Evaluate(longGap).Index);
                longGap.Advance(2.42); var seated = a.Evaluate(longGap);
                Check.Equal(PlayerMotionPhase.Recover, seated.Phase); Check.Equal(4, seated.Index);
                longGap.Advance(2.85); Check.Equal(5, a.Evaluate(longGap).Index);
                longGap.Advance(3.1); Check.Equal(PlayerMotionPhase.Idle, a.Evaluate(longGap).Phase);

                int nextTick = kind == GestureKind.Dive ? 6 : 4;
                var shortGap = Round(1, new PatternStep(kind, 0, kind == GestureKind.Dive ? 4 : 0), new PatternStep(GestureKind.Tap, nextTick));
                var b = new PlayerMotionTimeline(); shortGap.Advance(2.121); b.Evaluate(shortGap);
                shortGap.Advance(2.38); Check.Equal(5, b.Evaluate(shortGap).Index);
                shortGap.Press(2 + nextTick * .125, 0, 0); var punch = b.Evaluate(shortGap);
                Check.Equal(GestureKind.Tap, punch.Kind.Value); Check.Equal(RhythmGrade.Perfect, punch.Grade.Value);
            }
        }

        [Test]
        public void NewValidContactImmediatelyInterruptsThePreviousFall()
        {
            var round = Round(1, new PatternStep(GestureKind.Flick, 0), new PatternStep(GestureKind.Hold, 4, 4));
            var timeline = new PlayerMotionTimeline(); round.Advance(2.121); timeline.Evaluate(round);
            round.Press(2.4, 0, 0); var frame = timeline.Evaluate(round);
            Check.Equal(PlayerMotionPhase.Sustain, frame.Phase); Check.Equal(GestureKind.Hold, frame.Kind.Value);
        }

        [Test]
        public void StationaryShakeAndPendingResponsesStayIdleUntilActualContactMovement()
        {
            var round = Single(GestureKind.Shake); var timeline = new PlayerMotionTimeline();
            round.Advance(2); Check.Equal(PlayerMotionPhase.Idle, timeline.Evaluate(round).Phase);
            round.Press(2, 0, 0); Check.Equal(PlayerMotionPhase.Idle, timeline.Evaluate(round).Phase);
            round.Move(2.04, .1, 0); var shove = timeline.Evaluate(round);
            Check.Equal(PlayerMotionPhase.Sustain, shove.Phase); Check.Equal(GestureKind.Shake, shove.Kind.Value);
            round.Advance(2.25); Check.Equal(RhythmGrade.Miss, timeline.Evaluate(round).Grade.Value);
            var tap = Single(GestureKind.Tap); tap.Advance(1.99);
            Check.Equal(PlayerMotionPhase.Idle, new PlayerMotionTimeline().Evaluate(tap).Phase);
        }

        [Test]
        public void ShakeFailureAppearsAtItsDeadlineAndLaterHoldKeepsItsOwnResult()
        {
            var round = Round(1, new PatternStep(GestureKind.Hold, 0, 4), new PatternStep(GestureKind.Shake, 0));
            var timeline = new PlayerMotionTimeline(); round.Press(2, 0, 0); timeline.Evaluate(round);
            round.Advance(2.121); var frame = timeline.Evaluate(round);
            Check.Equal(1, round.Results.Count); Check.Equal(RhythmGrade.Miss, frame.Grade.Value);
            Check.Equal(GestureKind.Shake, frame.Kind.Value); Check.Equal(4, frame.Index);
            round.Advance(2.5); Check.Equal(2, round.Results.Count); Check.Equal(1, round.PerfectCount);
        }

        [Test]
        public void FrozenSongTimeAndClockJumpsDoNotReselectOrReplayPunches()
        {
            var round = Single(GestureKind.Tap); var timeline = new PlayerMotionTimeline(3);
            round.Press(2, 0, 0); timeline.Evaluate(round); round.Advance(2.017);
            var first = timeline.Evaluate(round); var shape = PlayerSquashStretch.Calculate(first, round.BeatSeconds);
            Check.True(shape.Y < 1);
            round.Suspend(); round.Advance(20); var frozen = timeline.Evaluate(round);
            Check.Equal(first.Index, frozen.Index); Check.Equal(first.Age, frozen.Age); Check.Equal(1, timeline.PunchSelections);
            Check.Equal(shape.Y, PlayerSquashStretch.Calculate(frozen, round.BeatSeconds).Y);
            round.Resume(false); round.Advance(5);
            Check.Equal(PlayerMotionPhase.Idle, timeline.Evaluate(round).Phase); Check.Equal(1, timeline.PunchSelections);
        }

        [Test]
        public void EmptyPressesSwingOnceWithoutCreatingGradesOrDamage()
        {
            var round = Single(GestureKind.Tap); var timeline = new PlayerMotionTimeline(3);
            round.Press(.4, 0, 0); var first = timeline.Evaluate(round);
            Check.True(first.IsFreeInput); Check.Equal(GestureKind.Tap, first.Kind.Value);
            Check.False(first.Grade.HasValue); Check.Equal(PlayerMotionPhase.Prepare, first.Phase); Check.Equal(0, first.Index % 4);
            round.Release(.43, 0, 0); Check.Equal(first.Punch, timeline.Evaluate(round).Punch);
            Check.Equal(1, timeline.PunchSelections);
            round.Advance(.44); Check.Equal(1, timeline.Evaluate(round).Index % 4);
            round.Advance(.519); Check.Equal(PlayerMotionPhase.Impact, timeline.Evaluate(round).Phase);
            round.Advance(.521); var recall = timeline.Evaluate(round);
            Check.Equal(PlayerMotionPhase.Recover, recall.Phase); Check.Equal(first.Punch * 4, recall.Index);
            round.Press(.65, 0, 0); var second = timeline.Evaluate(round);
            Check.True(second.IsFreeInput && second.Punch != first.Punch);
            round.Release(.68, 0, 0); round.Advance(1.2);
            Check.Equal(PlayerMotionPhase.Idle, timeline.Evaluate(round).Phase);
            Check.Equal(0, round.Results.Count); Check.Equal(0, round.MissCount); Check.Equal(0m, round.TotalDamageTaken);
            Check.Equal(ResponseState.Pending, round.Notes.Single().State);
        }

        [Test]
        public void EmptyHoldReleaseRecoversTheGuardWithoutAnUnmatchedDive()
        {
            foreach (double bpm in new[] { 120.0, 168.0, 240.0 })
            foreach (bool longHold in new[] { false, true })
            {
                var round = RoundAtBpm(bpm, 1, new PatternStep(GestureKind.Dive, 0, 4));
                var timeline = new PlayerMotionTimeline(); double beat = round.BeatSeconds;
                round.Press(beat * .2, 0, 0); timeline.Evaluate(round); round.Advance(beat * .8);
                var held = timeline.Evaluate(round);
                Check.True(held.IsFreeInput); Check.Equal(GestureKind.Hold, held.Kind.Value);
                Check.Equal("hold", held.Sheet); Check.Equal(PlayerMotionPhase.Sustain, held.Phase);
                double release = beat * (longHold ? 2.5 : 1);
                round.Release(release, 0, 0); var released = timeline.Evaluate(round);
                Check.Equal(GestureKind.Hold, round.FreeInput.Kind.Value); Check.False(round.FreeInput.IsHeld);
                Check.True(released.IsFreeInput); Check.Equal("hold", released.Sheet);
                Check.Equal(PlayerMotionPhase.Recover, released.Phase); Check.Equal(5, released.Index);
                Check.False(released.Grade.HasValue); Check.False(released.IsFall);
                round.Suspend(); round.Advance(100);
                Check.Equal(released.PhaseAge, timeline.Evaluate(round).PhaseAge);
                round.Resume(false); round.Advance(release + beat * .6);
                Check.Equal(PlayerMotionPhase.Idle, timeline.Evaluate(round).Phase);
                Check.Equal(0, round.Results.Count); Check.Equal(0m, round.TotalDamageTaken);
                Check.Equal(ResponseState.Pending, round.Notes.Single().State);
            }
        }

        [Test]
        public void HeldInputUsesTheAuthoredHoldOrDivePoseAndKeepsItsTimingGrade()
        {
            foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive })
            foreach (double offset in new[] { 0.0, .1 })
            {
                var round = Single(kind); var timeline = new PlayerMotionTimeline();
                round.Press(2 + offset, 0, 0); var preparing = timeline.Evaluate(round);
                Check.False(preparing.IsFreeInput); Check.Equal(kind, preparing.Kind.Value);
                Check.Equal(PlayerMotionTimeline.SheetFor(kind), preparing.Sheet);
                Check.Equal(PlayerMotionPhase.Sustain, preparing.Phase);
                round.Advance(2.3); var held = timeline.Evaluate(round);
                Check.Equal(kind, held.Kind.Value); Check.Equal(1, held.Index);
                Check.False(held.Grade.HasValue); Check.Equal(0, round.Results.Count);
                round.Suspend(); round.Advance(100); Check.Equal(2.3, round.ElapsedSeconds);
                round.Resume(true, 10, 10); var resumed = timeline.Evaluate(round);
                Check.Equal(held.Kind, resumed.Kind); Check.Equal(held.Index, resumed.Index);
                Check.False(round.FreeInput.Kind.HasValue);
                round.Advance(2.5);
                Check.Equal(kind == GestureKind.Dive ? 0 : 1, round.Results.Count);
                if (kind == GestureKind.Dive) Check.Equal(PlayerMotionPhase.Sustain, timeline.Evaluate(round).Phase);
                round.Release(2.5, 10, 10); var result = timeline.Evaluate(round);
                Check.Equal(kind, result.Kind.Value); Check.False(result.IsFreeInput);
                Check.Equal(offset == 0 ? RhythmGrade.Perfect : RhythmGrade.HalfMiss, result.Grade.Value);
                Check.Equal(1, round.Results.Count); Check.Equal(offset == 0 ? 0m : 2m, round.TotalDamageTaken);
            }
        }

        [Test]
        public void CompletedHoldCannotReplaceADiveThatIsStillHeld()
        {
            var round = Round(1, new PatternStep(GestureKind.Hold, 0, 4), new PatternStep(GestureKind.Dive, 0, 4));
            var timeline = new PlayerMotionTimeline();
            round.Press(2, 0, 0); var started = timeline.Evaluate(round);
            Check.Equal(GestureKind.Dive, started.Kind.Value); Check.False(started.IsFreeInput);
            round.Advance(2.5); var waitingForRelease = timeline.Evaluate(round);
            Check.Equal(1, round.PerfectCount); Check.True(round.IsDown);
            Check.Equal(GestureKind.Hold, round.Results.Single().Note.Step.Kind);
            Check.Equal(GestureKind.Dive, waitingForRelease.Kind.Value);
            Check.Equal(PlayerMotionPhase.Sustain, waitingForRelease.Phase); Check.Equal("dive", waitingForRelease.Sheet);
            round.Release(2.5, 0, 0); var finished = timeline.Evaluate(round);
            Check.Equal(2, round.PerfectCount); Check.Equal(0m, round.TotalDamageTaken);
            Check.Equal(GestureKind.Dive, finished.Kind.Value); Check.Equal(RhythmGrade.Perfect, finished.Grade.Value);
        }

        [Test]
        public void EmptyFlickAndShakeUseTheirOwnInputMotion()
        {
            var flick = Single(GestureKind.Tap); var a = new PlayerMotionTimeline();
            flick.Press(.2, 0, 0); a.Evaluate(flick); flick.Release(.25, .1, 0);
            var jump = a.Evaluate(flick);
            Check.True(jump.IsFreeInput); Check.Equal(GestureKind.Flick, jump.Kind.Value); Check.Equal(1, jump.Index);
            flick.Advance(.371); var landed = a.Evaluate(flick);
            Check.True(landed.IsFreeInput); Check.Equal(PlayerMotionPhase.Recover, landed.Phase); Check.Equal(0, landed.Index);
            var shake = Single(GestureKind.Tap); var b = new PlayerMotionTimeline();
            shake.Press(.2, 0, 0); b.Evaluate(shake); shake.Move(.24, .1, 0);
            var outward = b.Evaluate(shake);
            Check.True(outward.IsFreeInput); Check.Equal(GestureKind.Shake, outward.Kind.Value);
            Check.Equal(PlayerMotionPhase.Sustain, outward.Phase); Check.Equal(1, outward.Index);
            shake.Move(.28, 0, 0); Check.Equal(0, b.Evaluate(shake).Index);
            shake.Release(.42, 0, 0); var push = b.Evaluate(shake);
            Check.True(push.IsFreeInput); Check.Equal(GestureKind.Shake, push.Kind.Value); Check.Equal(2, push.Index);
            Check.Equal(0, flick.Results.Count); Check.Equal(0, shake.Results.Count);
        }

        [Test]
        public void RealGesturePreparationAndNewEmptyInputsKeepTheirOwnPriority()
        {
            var flick = Single(GestureKind.Flick); var a = new PlayerMotionTimeline();
            flick.Press(1.95, 0, 0); Check.False(a.Evaluate(flick).IsFreeInput);
            flick.Release(2, .1, 0); var valid = a.Evaluate(flick);
            Check.False(valid.IsFreeInput); Check.Equal(RhythmGrade.Perfect, valid.Grade.Value);
            var tap = Single(GestureKind.Tap); var b = new PlayerMotionTimeline(5);
            tap.Press(2, 0, 0); var punch = b.Evaluate(tap); tap.Release(2.01, 0, 0);
            tap.Press(2.04, 0, 0); var empty = b.Evaluate(tap);
            Check.True(empty.IsFreeInput && empty.Punch != punch.Punch);
            Check.Equal(1, tap.PerfectCount); Check.Equal(1, tap.Results.Count); Check.Equal(0m, tap.TotalDamageTaken);
            var miss = Single(GestureKind.Dive); var c = new PlayerMotionTimeline();
            miss.Advance(2.121); Check.True(c.Evaluate(miss).IsFall);
            miss.Press(2.14, 0, 0); Check.True(c.Evaluate(miss).IsFreeInput);
            Check.Equal(1, miss.MissCount);
        }

        [Test]
        public void FreeInputFreezesAndRecontactDoesNotInventAnotherGesture()
        {
            var round = Single(GestureKind.Tap); var timeline = new PlayerMotionTimeline();
            round.Press(.1, 0, 0); timeline.Evaluate(round); round.Advance(.35);
            var held = timeline.Evaluate(round); int sequence = round.FreeInput.Sequence;
            round.Suspend(); round.Advance(100);
            Check.Equal(held.Kind, timeline.Evaluate(round).Kind); Check.Equal(.35, round.ElapsedSeconds);
            round.Resume(true, 10, 10); round.Move(.36, 10, 10);
            Check.Equal(sequence, round.FreeInput.Sequence); Check.Equal(GestureKind.Hold, timeline.Evaluate(round).Kind.Value);
            round.Release(.4, 10, 10); timeline.Evaluate(round); round.Advance(1.2);
            Check.Equal(PlayerMotionPhase.Idle, timeline.Evaluate(round).Phase);
            round.Stop(); round.Press(1.3, 0, 0);
            Check.False(round.FreeInput.Kind.HasValue); Check.Equal(0, round.Results.Count);
        }

        [Test]
        public void FastConsecutiveTapsAlwaysPrepareAndRecallWithoutDelayingJudgments()
        {
            foreach (double bpm in new[] { 120.0, 168.0, 240.0 })
            {
                var round = RoundAtBpm(bpm, 3, new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 2),
                    new PatternStep(GestureKind.Tap, 4), new PatternStep(GestureKind.Tap, 6));
                var timeline = new PlayerMotionTimeline(7); int previous = -1;
                double beat = round.BeatSeconds;
                for (int i = 0; i < 4; i++)
                {
                    double at = beat * (4 + i * .5), prepare = PlayerMotionTimeline.TapPreparationDuration(beat);
                    round.Press(at, 0, 0); var ready = timeline.Evaluate(round);
                    Check.Equal(PlayerMotionPhase.Prepare, ready.Phase); Check.Equal(0, ready.Index % 4);
                    Check.True(ready.Punch != previous); previous = ready.Punch;
                    Check.Equal((i + 1) * 3, round.PerfectCount); Check.Equal(at, round.Results.Last().JudgedAtSeconds);
                    round.Advance(at + prepare * .5);
                    Check.True(PlayerSquashStretch.Calculate(timeline.Evaluate(round), beat).Y < 1);
                    round.Advance(at + prepare + .01); var strike = timeline.Evaluate(round);
                    Check.Equal(PlayerMotionPhase.Impact, strike.Phase); Check.Equal(ready.Punch * 4 + 1, strike.Index);
                    round.Advance(at + beat * .30); var recall = timeline.Evaluate(round);
                    Check.Equal(PlayerMotionPhase.Recover, recall.Phase); Check.Equal(ready.Punch * 4, recall.Index);
                    round.Release(round.ElapsedSeconds, 0, 0);
                }
                Check.Equal(4, timeline.PunchSelections); Check.Equal(0m, round.TotalDamageTaken);
            }
            // Unmatched rapid taps can interrupt a punch before its usual recall, but still show a ready pose.
            var empty = Single(GestureKind.Tap); var free = new PlayerMotionTimeline(9);
            for (int i = 0; i < 4; i++)
            {
                double at = .1 + i * .08;
                empty.Press(at, 0, 0); Check.Equal(PlayerMotionPhase.Prepare, free.Evaluate(empty).Phase);
                empty.Advance(at + .04); Check.Equal(PlayerMotionPhase.Impact, free.Evaluate(empty).Phase);
                empty.Release(at + .041, 0, 0);
            }
            Check.Equal(0, empty.Results.Count); Check.Equal(0m, empty.TotalDamageTaken);
        }

        [Test]
        public void EveryGestureHasBriefDeformationThatSettlesWithoutChangingItsGrade()
        {
            foreach (GestureKind kind in Enum.GetValues(typeof(GestureKind)))
            foreach (RhythmGrade grade in Enum.GetValues(typeof(RhythmGrade)))
            {
                if (kind == GestureKind.Shake && grade == RhythmGrade.HalfMiss) continue;
                var round = Single(kind); var timeline = new PlayerMotionTimeline(3);
                Judge(round, kind, grade); timeline.Evaluate(round);
                var result = round.Results.Single(); double at = result.JudgedAtSeconds;
                decimal damage = round.TotalDamageTaken;
                round.Advance(at + .06); var frame = timeline.Evaluate(round);
                var shape = PlayerSquashStretch.Calculate(frame, round.BeatSeconds);
                Check.True(Math.Abs(shape.X - 1) > .005);
                Check.True(shape.X > .9 && shape.X < 1.1 && shape.Y > .9 && shape.Y < 1.1);
                Check.True(Math.Abs(shape.X * shape.Y - 1) < 1e-9);
                var disabled = PlayerSquashStretch.Calculate(frame, round.BeatSeconds, 0);
                Check.Equal(1.0, disabled.X); Check.Equal(1.0, disabled.Y);
                round.Advance(at + .38);
                var settled = PlayerSquashStretch.Calculate(timeline.Evaluate(round), round.BeatSeconds);
                Check.Equal(1.0, settled.X); Check.Equal(1.0, settled.Y);
                Check.Equal(grade, result.Grade); Check.Equal(1, round.Results.Count); Check.Equal(damage, round.TotalDamageTaken);
            }
        }

        [Test]
        public void ShakeDeformationPulsesPerStrokeInsteadOfRestartingOnEveryMovementSample()
        {
            var round = Single(GestureKind.Tap); var timeline = new PlayerMotionTimeline();
            round.Press(.1, 0, 0); timeline.Evaluate(round);
            round.Move(.12, .1, 0); timeline.Evaluate(round);
            round.Move(.15, .11, 0);
            Check.True(PlayerSquashStretch.Calculate(timeline.Evaluate(round), round.BeatSeconds).X > 1);
            round.Move(.24, .12, 0);
            Check.Equal(1.0, PlayerSquashStretch.Calculate(timeline.Evaluate(round), round.BeatSeconds).X);
            round.Move(.27, 0, 0); timeline.Evaluate(round);
            round.Move(.30, 0, 0);
            Check.True(PlayerSquashStretch.Calculate(timeline.Evaluate(round), round.BeatSeconds).X > 1);
            round.Advance(.6);
            Check.Equal(1.0, PlayerSquashStretch.Calculate(timeline.Evaluate(round), round.BeatSeconds).X);
            Check.Equal(0, round.Results.Count);
        }

        [Test]
        public void GreenImportMattePreservesCostumeColorsAndSoftBlackEdges()
        {
            PlayerChromaKey.Composite(0, 255, 0, 255, out _, out _, out _, out byte clear);
            Check.Equal((byte)0, clear);
            foreach (var color in new[] { (255, 255, 255), (20, 18, 25), (244, 55, 124), (209, 171, 112), (255, 220, 205) })
            {
                PlayerChromaKey.Composite((byte)color.Item1, (byte)color.Item2, (byte)color.Item3, 255,
                    out byte r, out byte g, out byte b, out byte a);
                Check.Equal((byte)color.Item1, r); Check.Equal((byte)color.Item2, g); Check.Equal((byte)color.Item3, b); Check.Equal((byte)255, a);
            }
            PlayerChromaKey.Composite(0, 120, 0, 255, out byte er, out byte eg, out byte eb, out byte ea);
            Check.Equal(true, ea > 100 && ea < 155); Check.Equal((byte)0, er); Check.Equal((byte)0, eg); Check.Equal((byte)0, eb);
        }

        private static RhythmRound Single(GestureKind kind) => Round(1, new PatternStep(kind, 0,
            kind == GestureKind.Hold || kind == GestureKind.Dive ? 4 : 0));

        private static void Judge(RhythmRound round, GestureKind kind, RhythmGrade grade)
        {
            if (grade == RhythmGrade.Miss) { round.Advance(2.121); return; }
            double offset = grade == RhythmGrade.HalfMiss ? .1 : 0;
            switch (kind)
            {
                case GestureKind.Tap: round.Press(2 + offset, 0, 0); break;
                case GestureKind.Hold: round.Press(2 + offset, 0, 0); round.Advance(2.5); break;
                case GestureKind.Dive: round.Press(2 + offset, 0, 0); round.Release(2.5, 0, 0); break;
                case GestureKind.Flick: round.Press(1.95 + offset, 0, 0); round.Release(2 + offset, .1, 0); break;
                case GestureKind.Shake:
                    round.Press(2, 0, 0); round.Move(2.04, .1, 0);
                    if (grade == RhythmGrade.Perfect) round.Move(2.08, 0, 0);
                    break;
            }
        }

        private static RhythmRound Round(int count, params PatternStep[] steps) => RoundAtBpm(120, count, steps);

        private static RhythmRound RoundAtBpm(double bpm, int count, params PatternStep[] steps)
        {
            var slots = new List<SlotTemplate>();
            for (int tick = 0; tick < 16; tick += 2)
            {
                slots.Add(new SlotTemplate(GestureKind.Tap, tick)); slots.Add(new SlotTemplate(GestureKind.Flick, tick));
                slots.Add(new SlotTemplate(GestureKind.Shake, tick));
                foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive })
                    slots.Add(new SlotTemplate(kind, tick, 4));
            }
            var stage = MusicStage.Generate(new MusicDefinition("motion", "Motion", bpm, 4, new[]
            { new MusicSection("INTRO", 0, 1, 1, false), new MusicSection("BODY", 1, 3, 1) }, new[] { slots }));
            var pattern = new RhythmPattern("motion", 4, steps);
            var monster = new MonsterDefinition("tap-slime", "Motion", "", pattern, new[] { new CallSignal(0, "통!") },
                Math.Max(4, pattern.EndOffsetTick), 4, 1);
            var proposals = Enumerable.Range(0, count).Select(i => new MonsterProposal("m-" + i, monster,
                stage.FindPlacements(pattern).Where(x => x.StartTick == 16)));
            return new RhythmRound(BattlePlanner.Resolve(stage, proposals, 1));
        }
    }
}
