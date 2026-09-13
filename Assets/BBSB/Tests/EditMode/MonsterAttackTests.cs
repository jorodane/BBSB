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
    public sealed class MonsterAttackTests
    {
        [Test]
        public void EveryCatalogStepHasAUniqueImageFolderAndARealCallBeforeItsResponse()
        {
            Check.Equal(24, MonsterAttackCatalog.All.Select(x => x.PatternId).Distinct().Count());
            int expected = 0;
            foreach (var monster in MonsterCatalog.All) foreach (var pattern in monster.Patterns) expected += pattern.Pattern.Steps.Count;
            Check.Equal(expected, MonsterAttackCatalog.All.Count);
            Check.Equal(MonsterAttackCatalog.All.Count, MonsterAttackCatalog.All.Select(x => x.ResourceFolder).Distinct().Count());
            foreach (double bpm in new[] { 90.0, 120.0, 168.0, 240.0 })
            foreach (var pattern in MonsterCatalog.All.SelectMany(x => x.Patterns))
            {
                var round = Round(pattern.Id, bpm);
                foreach (var note in round.Notes)
                {
                    var art = MonsterAttackCatalog.For(note);
                    Check.Equal(note.StepIndex, art.StepIndex);
                    Check.True(art.CallIndex < note.Attack.Call.Count);
                    double spawn = art.SpawnSeconds(note, round.BeatSeconds);
                    Check.True(spawn < note.StartSeconds, art.ResourceFolder);
                    Check.True(art.Height > 0);
                    Check.False(Sample(round, note, spawn - .001).Visible);
                    var arrival = Sample(round, note, note.StartSeconds);
                    Check.Equal(MonsterAttackPhase.Contact, arrival.Phase);
                    Near(1, arrival.Progress); Near(0, arrival.Lift);
                    Check.Equal(0, round.Results.Count); Check.Equal(0m, round.TotalDamageTaken);
                }
            }
        }

        [Test]
        public void JellyRelaysFollowTheActualCallIntervalsAndStillRequireOnlyOneTap()
        {
            foreach (var id in new[] { "count-four-tap", "tresillo-call-tap" })
            {
                var round = Round(id); var note = round.Notes.Single();
                for (int i = 0; i < note.Attack.Call.Count; i++)
                {
                    double time = RhythmTime.Seconds(note.Attack.Call[i].Tick, round.Plan.Stage.Music.Bpm);
                    round.Advance(time); var frame = Sample(round, note, time);
                    Near(i / (double)(note.Attack.Call.Count + 1), frame.Progress);
                    Check.Equal(MonsterAttackPhase.Spawn, frame.Phase);
                }
                double last = RhythmTime.Seconds(note.Attack.Call.Last().Tick, round.Plan.Stage.Music.Bpm);
                double lastPosition = Sample(round, note, last).Progress;
                Check.True(lastPosition <= .5, "The final bounce needs at least half the whole flight distance.");
                double middle = (last + note.StartSeconds) * .5;
                Near((lastPosition + 1) * .5, Sample(round, note, middle).Progress);
                double before = note.StartSeconds - round.HalfMissWindow;
                Near((1 - lastPosition) * round.HalfMissWindow / (note.StartSeconds - last),
                    1 - Sample(round, note, before).Progress);
                Check.Equal(0, round.Results.Count);
                round.Press(note.StartSeconds, 0, 0);
                Check.Equal(1, round.PerfectCount); Check.Equal(1, round.Notes.Count);
                Check.Equal(note.Attack.Call.Count, round.Calls.Count);
                Check.Equal(MonsterAttackPhase.Contact, Sample(round, note, round.ElapsedSeconds).Phase);
                Check.Equal(MonsterAttackPhase.Perfect, Sample(round, note,
                    round.ElapsedSeconds + PlayerMotionTimeline.TapPreparationDuration(round.BeatSeconds)).Phase);
            }
        }

        [Test]
        public void DollLandsEachOfSevenStepsOnTheBeatAndAttacksAtTheExistingResponse()
        {
            foreach (double bpm in new[] { 90.0, 168.0, 240.0 })
            {
                var round = Round("clock-seven-beat-wait", bpm); var note = round.Notes.Single();
                var art = MonsterAttackCatalog.For(note); double spawn = art.SpawnSeconds(note, round.BeatSeconds);
                Near(7 * round.BeatSeconds, note.StartSeconds - spawn);
                Check.True(Sample(round, note, spawn).Visible);
                for (int i = 1; i < 7; i++)
                {
                    var frame = Sample(round, note, spawn + i * round.BeatSeconds);
                    Near(i / 7.0, frame.Progress); Near(0, frame.Lift);
                    Check.Equal(MonsterAttackPhase.Travel, frame.Phase);
                }
                Check.True(Sample(round, note, note.StartSeconds - .01).Progress < 1);
                round.Advance(note.StartSeconds); Near(1, Sample(round, note, round.ElapsedSeconds).Progress);
                Check.Equal(1, round.Calls.Count); Check.Equal(0, round.Results.Count);
                round.Press(note.StartSeconds, 0, 0); Check.Equal(1, round.PerfectCount);
            }
        }

        [Test]
        public void ThrownDollReachesThePunchThenLandsAndAttacksOnlyAfterAMiss()
        {
            foreach (double bpm in new[] { 60.0, 120.0, 168.0, 240.0 })
            foreach (bool early in new[] { false, true })
            {
                var round = Round("clock-quick-tap", bpm); var note = round.Notes.Single();
                var art = MonsterAttackCatalog.For(note); double target = note.StartSeconds;
                Check.False(art.Grounded); Check.True(art.LandsAfterMiss);
                if (early) round.Press(target - round.BeatSeconds * .5, 0, 0);
                round.Advance(target); var contact = Sample(round, note, target);
                Check.Equal(MonsterAttackPhase.Contact, contact.Phase); Near(0, contact.MissApproach);
                double deadline = target + round.HalfMissWindow;
                round.Advance(deadline + .001); Check.Equal(1, round.MissCount);
                double landing = deadline + MonsterAttackTimeline.MissApproachSeconds(round.BeatSeconds);
                var falling = Sample(round, note, (deadline + landing) * .5);
                Check.Equal(MonsterAttackPhase.Travel, falling.Phase); Near(.5, falling.MissApproach);
                Check.False(falling.IsReaction);
                round.Suspend(); var frozen = Sample(round, note, round.ElapsedSeconds);
                round.Advance(100); Near(frozen.MissApproach, Sample(round, note, round.ElapsedSeconds).MissApproach);
                round.Resume(false); round.Advance(landing);
                var hit = Sample(round, note, round.ElapsedSeconds);
                Check.Equal(MonsterAttackPhase.Miss, hit.Phase); Near(1, hit.MissApproach);
                round.Advance(landing + MonsterAttackTimeline.ReactionSeconds + .01);
                Check.False(Sample(round, note, round.ElapsedSeconds).Visible);
                Check.Equal(1, round.Results.Count); Check.Equal(4m, round.TotalDamageTaken);
            }
            foreach (double error in new[] { 0.0, .10 })
            {
                var round = Round("clock-quick-tap"); var note = round.Notes.Single();
                round.Press(note.StartSeconds + error, 0, 0);
                var reaction = Sample(round, note, round.ElapsedSeconds + PlayerMotionTimeline.TapPreparationDuration(round.BeatSeconds));
                Check.Equal(error == 0 ? MonsterAttackPhase.Perfect : MonsterAttackPhase.HalfMiss, reaction.Phase);
                Near(0, reaction.MissApproach);
                Check.Equal(1, round.Results.Count);
            }
        }

        [Test]
        public void LinearRushLobAndMaterializationKeepTheirOwnApproachTiming()
        {
            var linear = Round("march-three"); var a = linear.Notes.First();
            double launch = MonsterAttackCatalog.For(a).SpawnSeconds(a, linear.BeatSeconds);
            Near(.25, Sample(linear, a, launch + (a.StartSeconds - launch) * .25).Progress);
            Near(.75, Sample(linear, a, launch + (a.StartSeconds - launch) * .75).Progress);
            var rush = Round("offbeat-pair"); var b = rush.Notes.First();
            var waiting = Sample(rush, b, b.StartSeconds - rush.BeatSeconds * .5);
            Check.Equal(MonsterAttackPhase.Wait, waiting.Phase); Near(0, waiting.Progress);
            Near(.5, Sample(rush, b, b.StartSeconds - rush.BeatSeconds * .125).Progress);
            var lob = Round("clock-quick-tap"); var c = lob.Notes.Single();
            Check.True(Sample(lob, c, c.StartSeconds - lob.BeatSeconds * .5).Lift > .7);
            var dream = Round("drowsy-four-beat-wait"); var d = dream.Notes.Single();
            Check.False(Sample(dream, d, d.StartSeconds - .001).Visible);
            Check.True(Sample(dream, d, d.StartSeconds).Visible);
        }

        [Test]
        public void MultipleResponsesUseTheirAuthoredCallAndEmissionSchedules()
        {
            var march = Round("march-three");
            var spawns = march.Notes.Select(n => MonsterAttackCatalog.For(n).SpawnSeconds(n, march.BeatSeconds)).ToArray();
            Near(march.BeatSeconds, spawns[1] - spawns[0]); Near(march.BeatSeconds, spawns[2] - spawns[1]);
            var feathers = Round("tresillo-taps");
            Check.Equal(3, feathers.Notes.Select(n => MonsterAttackCatalog.For(n).SpawnSeconds(n, feathers.BeatSeconds)).Distinct().Count());
            foreach (var round in new[] { march, feathers })
            {
                foreach (var note in round.Notes)
                { round.Press(note.StartSeconds, 0, 0); round.Release(note.StartSeconds + .001, 0, 0); }
                Check.Equal(round == march ? 1 : 3, round.Calls.Count); Check.Equal(3, round.PerfectCount);
                Check.Equal(3, round.Results.Count);
            }
        }

        [Test]
        public void HarpyWaitsAfterEachCallThenRushesForTheSameFinalHalfBeat()
        {
            foreach (double bpm in new[] { 60.0, 120.0, 200.0 })
            {
                var round = Round("tresillo-taps", bpm); var attack = round.Plan.Attacks.Single();
                var calls = attack.Call.Select(c => c.Tick * round.BeatSeconds / 4).ToArray();
                Near(1.5 * round.BeatSeconds, calls[1] - calls[0]);
                Near(1.5 * round.BeatSeconds, calls[2] - calls[1]);
                Near(round.BeatSeconds, round.Notes[0].StartSeconds - calls[2]);
                for (int i = 0; i < round.Notes.Count; i++)
                {
                    var note = round.Notes[i]; var art = MonsterAttackCatalog.For(note);
                    Near(calls[i], art.SpawnSeconds(note, round.BeatSeconds));
                    Near(4 * round.BeatSeconds, note.StartSeconds - calls[i]);
                    for (int tick = 0; tick < 16; tick++)
                    {
                        var frame = Sample(round, note, calls[i] + tick * round.BeatSeconds / 4);
                        Near(Math.Max(0, (tick - 14) / 2.0), frame.Progress); Near(0, frame.Lift);
                        if (tick > 0 && tick < 14) Check.Equal(MonsterAttackPhase.Wait, frame.Phase);
                    }
                    Near(.5, Sample(round, note, note.StartSeconds - round.BeatSeconds * .25).Progress);
                    Check.Equal(MonsterAttackPhase.Contact, Sample(round, note, note.StartSeconds).Phase);
                }
                var quick = Round("rotated-tresillo", bpm);
                Check.Equal(1, quick.Notes.Count); Check.Equal(1, quick.Plan.Calls.Count);
                Near(quick.BeatSeconds, quick.Notes[0].StartSeconds -
                    MonsterAttackCatalog.For(quick.Notes[0]).SpawnSeconds(quick.Notes[0], quick.BeatSeconds));
            }
        }

        [Test]
        public void ActualGradesChooseImagesAndAnEarlyMissCannotDestroyTheIncomingAttack()
        {
            foreach (int variant in new[] { 0, 1, 2, 3 })
            {
                var round = Round("count-four-tap"); var note = round.Notes.Single(); double target = note.StartSeconds;
                if (variant == 0) round.Press(target, 0, 0);
                if (variant == 1) round.Press(target + .10, 0, 0);
                if (variant == 2) round.Advance(target + .20);
                if (variant == 3)
                {
                    round.Press(target - .25, 0, 0);
                    Check.Equal(MissReason.TooEarly, note.Result.Reason);
                    var incoming = Sample(round, note, target - .20);
                    Check.True(incoming.Visible); Check.False(incoming.IsReaction); Check.True(incoming.Progress < 1);
                }
                var phase = variant == 0 ? MonsterAttackPhase.Perfect : variant == 1 ? MonsterAttackPhase.HalfMiss : MonsterAttackPhase.Miss;
                double at = Math.Max(target, note.Result.JudgedAtSeconds);
                if (variant < 2) at = Math.Max(at, note.Result.JudgedAtSeconds + PlayerMotionTimeline.TapPreparationDuration(round.BeatSeconds));
                Check.Equal(phase, Sample(round, note, at).Phase);
                Check.False(Sample(round, note, at + MonsterAttackTimeline.ReactionSeconds + .01).Visible);
                Check.Equal(1, round.Results.Count); Check.Equal(variant == 0 ? 0m : variant == 1 ? 2m : 4m, round.TotalDamageTaken);
            }
        }

        [Test]
        public void SustainedHazardsUseOneSlotThroughContactAndOneFinalResult()
        {
            foreach (var id in new[] { "turtle-long-hold", "ray-short-dive" })
            {
                var round = Round(id); var note = round.Notes.Single();
                round.Press(note.StartSeconds, 0, 0);
                double middle = (note.StartSeconds + note.EndSeconds) * .5;
                round.Advance(middle); Check.Equal(MonsterAttackPhase.Contact, Sample(round, note, middle).Phase);
                round.Release(note.EndSeconds, 0, 0);
                Check.Equal(1, round.PerfectCount); Check.Equal(1, round.Notes.Count); Check.Equal(1, round.Results.Count);
                Check.Equal(MonsterAttackPhase.Perfect, Sample(round, note, note.EndSeconds).Phase);
            }
            var early = Round("ray-short-dive"); var dive = early.Notes.Single();
            early.Press(dive.StartSeconds, 0, 0); early.Release(dive.StartSeconds + .2, 0, 0);
            Check.Equal(RhythmGrade.Miss, dive.Result.Grade);
            Check.Equal(MonsterAttackPhase.Contact, Sample(early, dive, dive.EndSeconds - .1).Phase);
            Check.Equal(MonsterAttackPhase.Miss, Sample(early, dive, dive.EndSeconds).Phase);
        }

        [Test]
        public void PauseAndClockJumpsCannotRestartAttackFramesOrApplyExtraDamage()
        {
            var round = Round("clock-seven-beat-wait"); var note = round.Notes.Single();
            round.Advance(note.StartSeconds - round.BeatSeconds * 2.3);
            var a = Sample(round, note, round.ElapsedSeconds);
            round.Suspend(); round.Advance(100);
            var b = Sample(round, note, round.ElapsedSeconds);
            Near(a.Progress, b.Progress); Near(a.Lift, b.Lift); Near(a.AnimationBeat, b.AnimationBeat);
            round.Resume(false); round.Advance(note.EndSeconds + 1);
            for (int i = 0; i < 100; i++) Check.False(Sample(round, note, round.ElapsedSeconds).Visible);
            Check.Equal(1, round.Results.Count); Check.Equal(1, round.Calls.Count); Check.Equal(4m, round.TotalDamageTaken);
        }

        [Test]
        public void SustainsExposeContinuousProgressAndAnExactEndCueEvenWithoutInput()
        {
            foreach (double bpm in new[] { 60.0, 120.0, 200.0, 240.0 })
            foreach (var id in new[] { "turtle-long-hold", "bat-hold", "ray-short-dive", "ray-deep-dive" })
            {
                var round = Round(id, bpm); var note = round.Notes.Single();
                MonsterContactFeedback Cue(double at) => MonsterAttackTimeline.ContactFeedback(note, at, round.BeatSeconds, round.HalfMissWindow);
                Check.False(Cue(note.StartSeconds - .001).Active);
                var first = Cue(note.StartSeconds); Check.True(first.Active); Near(0, first.Progress); Near(1, first.Pulse);
                var middle = Cue((note.StartSeconds + note.EndSeconds) / 2); Near(.5, middle.Progress); Check.False(middle.IsEnding);
                var last = Cue(note.EndSeconds - .001); Check.True(last.Progress < 1); Check.False(last.IsEnding);
                var end = Cue(note.EndSeconds); Check.True(end.IsEnding); Near(1, end.Progress); Near(0, end.EndProgress);
                Check.False(Cue(note.EndSeconds + .23).Active);
                round.Press(note.StartSeconds, 0, 0);
                round.Release(note.StartSeconds + .01, 0, 0); Check.Equal(RhythmGrade.Miss, note.Result.Grade);
                Check.True(Cue(note.EndSeconds - .001).Active); Check.True(Cue(note.EndSeconds).IsEnding);
            }
        }

        [Test]
        public void ShakeFeedbackTracksRealOutAndBackMovementInsteadOfAHoldTimer()
        {
            var round = Round("one-beat-shake"); var note = round.Notes.Single(); double at = note.StartSeconds;
            MonsterContactFeedback Cue(double time) => MonsterAttackTimeline.ContactFeedback(note, time, round.BeatSeconds, round.HalfMissWindow);
            round.Press(at - round.HalfMissWindow, 0, 0);
            round.Move(at - .04, .1, 0); var outward = Cue(round.ElapsedSeconds);
            Check.True(outward.Active); Check.False(outward.IsEnding); Check.True(outward.Progress >= .5 && outward.Progress < 1);
            round.Move(at, 0, 0); Check.Equal(1, round.PerfectCount);
            var done = Cue(round.ElapsedSeconds); Check.True(done.IsEnding); Near(1, done.Progress);
            Check.False(Cue(at + .3).Active); Check.Equal(1, round.Results.Count);
        }

        [Test]
        public void NekomataTailsRushLateAndWithdrawBeforeTheNextHalfBeatForEveryGrade()
        {
            foreach (double bpm in new[] { 60.0, 120.0, 200.0, 240.0 })
            foreach (int grade in new[] { 0, 1, 2 })
            {
                var round = Round("seesaw-early-finish", bpm); var first = round.Notes[0]; var second = round.Notes[1];
                var art = MonsterAttackCatalog.For(first); var other = MonsterAttackCatalog.For(second);
                Near(.16, art.LaneHeight(first)); Near(-.16, other.LaneHeight(second));
                Near(0, Sample(round, first, first.StartSeconds - round.BeatSeconds * .5).Progress);
                Near(.5, Sample(round, first, first.StartSeconds - round.BeatSeconds * .125).Progress);
                if (grade < 2)
                {
                    double error = grade == 0 ? 0 : round.HalfMissWindow - .0001;
                    round.Press(first.StartSeconds + error, 0, 0); round.Release(round.ElapsedSeconds + .001, 0, 0);
                }
                else round.Advance(first.StartSeconds + round.HalfMissWindow + .001);
                Check.Equal(1, round.Results.Count);
                var cleared = Sample(round, first, second.StartSeconds - .001);
                Check.True(!cleared.Visible, $"Tail still {cleared.Phase} at {bpm} BPM, grade {first.Result.Grade}, age {cleared.PhaseAge}, window {round.HalfMissWindow}");
                Check.Equal(MonsterAttackPhase.Contact, Sample(round, second, second.StartSeconds).Phase);
            }
        }

        private static MonsterAttackFrame Sample(RhythmRound round, ResponseNote note, double seconds) =>
            MonsterAttackTimeline.Evaluate(note, MonsterAttackCatalog.For(note), seconds, round.BeatSeconds, round.HalfMissWindow);
        private static void Near(double a, double b) => Check.True(Math.Abs(a - b) < 1e-7, a + " != " + b);

        private static RhythmRound Round(string patternId, double bpm = 120)
        {
            var monster = MonsterCatalog.All.Single(m => m.Patterns.Any(p => p.Id == patternId));
            var pattern = monster.Patterns.Single(p => p.Id == patternId);
            var slots = new List<SlotTemplate>();
            for (int tick = 0; tick < 16; tick += 2)
            {
                slots.Add(new SlotTemplate(GestureKind.Tap, tick)); slots.Add(new SlotTemplate(GestureKind.Flick, tick));
                slots.Add(new SlotTemplate(GestureKind.Shake, tick));
                foreach (var kind in new[] { GestureKind.Hold, GestureKind.Dive })
                    foreach (int duration in new[] { 4, 8, 16 }) slots.Add(new SlotTemplate(kind, tick, duration));
            }
            var stage = MusicStage.Generate(new MusicDefinition("attack-tests", "Attack tests", bpm, 8,
                new[] { new MusicSection("INTRO", 0, 1, 1, false), new MusicSection("BODY", 1, 7, 1) }, new[] { slots }));
            var placement = stage.FindPlacements(pattern.Pattern).First(x => x.StartTick >= 64 && x.CueStartTick % pattern.CueAlignmentTicks == 0);
            var proposal = new MonsterProposal(monster.Id, monster, new[] { placement });
            return new RhythmRound(BattlePlanner.Resolve(stage, new[] { proposal }, 1));
        }
    }
}
