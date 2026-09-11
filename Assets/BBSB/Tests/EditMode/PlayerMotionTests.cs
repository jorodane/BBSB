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
                var round = Single(kind); var timeline = new PlayerMotionTimeline(1);
                Judge(round, kind, grade);
                var result = round.Results.Single(); var frame = timeline.Evaluate(round);
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
                round.Advance(round.ElapsedSeconds + .25); var recover = timeline.Evaluate(round);
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
            round.Advance(2.25); Check.Equal(PlayerMotionPhase.Idle, timeline.Evaluate(round).Phase);
            var tap = Single(GestureKind.Tap); tap.Advance(1.99);
            Check.Equal(PlayerMotionPhase.Idle, new PlayerMotionTimeline().Evaluate(tap).Phase);
        }

        [Test]
        public void SameTimeMixedResultsShowTheFailureAndKeepAllUnderlyingResults()
        {
            var round = Round(1, new PatternStep(GestureKind.Hold, 0, 4), new PatternStep(GestureKind.Shake, 0, 4));
            var timeline = new PlayerMotionTimeline(); round.Press(2, 0, 0); timeline.Evaluate(round);
            round.Advance(2.5); var frame = timeline.Evaluate(round);
            Check.Equal(2, round.Results.Count); Check.Equal(RhythmGrade.Miss, frame.Grade.Value);
            Check.Equal(GestureKind.Shake, frame.Kind.Value); Check.Equal(4, frame.Index);
        }

        [Test]
        public void FrozenSongTimeAndClockJumpsDoNotReselectOrReplayPunches()
        {
            var round = Single(GestureKind.Tap); var timeline = new PlayerMotionTimeline(3);
            round.Press(2, 0, 0); var first = timeline.Evaluate(round);
            round.Suspend(); round.Advance(20); var frozen = timeline.Evaluate(round);
            Check.Equal(first.Index, frozen.Index); Check.Equal(first.Age, frozen.Age); Check.Equal(1, timeline.PunchSelections);
            round.Resume(false); round.Advance(5);
            Check.Equal(PlayerMotionPhase.Idle, timeline.Evaluate(round).Phase); Check.Equal(1, timeline.PunchSelections);
        }

        [Test]
        public void NativeAlphaAtlasPreservesGreenColorsAndPartialTransparency()
        {
            const int width = 64, height = 64;
            var source = new byte[width * height * 4];
            var colors = new[] { (0, 255, 0, 255), (14, 244, 49, 128), (244, 55, 124, 255), (255, 255, 255, 255) };
            for (int cell = 0; cell < 4; cell++)
            {
                var color = colors[cell];
                for (int y = 8; y < 20; y++)
                for (int x = 8; x < 20; x++)
                {
                    int at = ((cell / 2 * 32 + y) * width + cell % 2 * 32 + x) * 4;
                    source[at] = (byte)color.Item1; source[at + 1] = (byte)color.Item2;
                    source[at + 2] = (byte)color.Item3; source[at + 3] = (byte)color.Item4;
                }
            }
            var original = (byte[])source.Clone();
            var output = PlayerAtlasProcessor.Prepare(source, width, height, 2, 2);
            Check.Equal(true, source.SequenceEqual(original));
            for (int cell = 0; cell < 4; cell++)
            {
                int center = ((cell / 2 * 32 + 8) * width + cell % 2 * 32 + 16) * 4;
                var color = colors[cell];
                Check.Equal((byte)color.Item1, output[center]); Check.Equal((byte)color.Item2, output[center + 1]);
                Check.Equal((byte)color.Item3, output[center + 2]); Check.Equal((byte)color.Item4, output[center + 3]);
                for (int edge = 0; edge < 32; edge++)
                {
                    Check.Equal((byte)0, output[((cell / 2 * 32) * width + cell % 2 * 32 + edge) * 4 + 3]);
                    Check.Equal((byte)0, output[((cell / 2 * 32 + edge) * width + cell % 2 * 32) * 4 + 3]);
                }
            }
        }

        [Test]
        public void OpaqueSheetIsNotKeyedOrRejectedBeforeTransparentArtIsSupplied()
        {
            var source = new byte[32 * 32 * 4];
            for (int i = 0; i < source.Length; i += 4)
            { source[i] = 2; source[i + 1] = 249; source[i + 2] = 56; source[i + 3] = 255; }
            var original = (byte[])source.Clone();
            var output = PlayerAtlasProcessor.Prepare(source, 32, 32, 2, 2);
            Check.Equal(true, original.SequenceEqual(output));
            Check.Equal(true, original.SequenceEqual(source));
        }

        private static RhythmRound Single(GestureKind kind) => Round(1, new PatternStep(kind, 0,
            kind == GestureKind.Hold || kind == GestureKind.Dive || kind == GestureKind.Shake ? 4 : 0));

        private static void Judge(RhythmRound round, GestureKind kind, RhythmGrade grade)
        {
            if (grade == RhythmGrade.Miss) { round.Advance(kind == GestureKind.Shake ? 2.5 : 2.121); return; }
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
                    round.Advance(2.5); break;
            }
        }

        private static RhythmRound Round(int count, params PatternStep[] steps)
        {
            var slots = new List<SlotTemplate>();
            for (int tick = 0; tick < 16; tick += 2)
            {
                slots.Add(new SlotTemplate(GestureKind.Tap, tick)); slots.Add(new SlotTemplate(GestureKind.Flick, tick));
                foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive, GestureKind.Shake })
                    slots.Add(new SlotTemplate(kind, tick, 4));
            }
            var stage = MusicStage.Generate(new MusicDefinition("motion", "Motion", 120, 4, new[]
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
