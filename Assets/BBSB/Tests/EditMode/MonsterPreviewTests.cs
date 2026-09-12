using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class MonsterPreviewTests
    {
        [Test]
        public void EveryPatternKeepsItsCallsAndResponseTimingAtEveryPreviewTempo()
        {
            foreach (double bpm in new[] { 60.0, 120.0, 200.0 })
            foreach (var monster in MonsterCatalog.All)
            foreach (var pattern in monster.Patterns)
            {
                var demo = new MonsterPreview(monster, pattern, bpm);
                Check.Equal(1, demo.Round.Plan.Monsters.Count);
                Check.Equal(1, demo.Round.Plan.Attacks.Count);
                Check.Equal(pattern.Call.Count, demo.Attack.Call.Count);
                for (int i = 0; i < pattern.Call.Count; i++)
                    Check.Equal(pattern.Call[i].OffsetTick, demo.Attack.Call[i].Tick - demo.Attack.CallStartTick);
                foreach (var note in demo.Round.Notes)
                {
                    Check.Equal(pattern.Pattern.CueLeadTicks + note.Step.OffsetTick, note.StartTick - demo.Attack.CallStartTick);
                    Check.Equal(PreviewCueKind.Respond, demo.CueAt(note.StartSeconds).Kind);
                    var arrival = MonsterAttackTimeline.Evaluate(note, MonsterAttackCatalog.For(note), note.StartSeconds, demo.BeatSeconds, demo.Round.HalfMissWindow);
                    Check.Equal(MonsterAttackPhase.Contact, arrival.Phase);
                    Check.True(Math.Abs(1 - arrival.Progress) < .000001);
                    Check.True(demo.DurationSeconds > note.EndSeconds + MonsterAttackTimeline.ReactionSeconds, pattern.Id + " needs recovery time at " + bpm);
                }
                Check.True(Math.Abs(demo.LoopSeconds(demo.DurationSeconds * 3 + .1) - .1) < .000001);
                Check.Equal(PreviewCueKind.Ready, demo.CueAt(demo.LoopSeconds(demo.DurationSeconds)).Kind);
                Check.Equal(0, demo.Round.Results.Count);
                Check.True(demo.Round.Combat == null);
                Check.True(Math.Abs(demo.Round.ElapsedSeconds) < .000001);
            }
        }

        [Test]
        public void SevenBeatWaitAndOffbeatHaveUnambiguousFirstCallRelativeCounts()
        {
            var doll = Demo("clock-seven-beat-wait"); var shot = doll.Round.Notes[0];
            Check.True(Math.Abs(doll.DisplayBeat(shot.StartSeconds) - 8) < .000001);
            Check.Equal(PreviewCueKind.Wait, doll.CueAt(shot.StartSeconds - doll.BeatSeconds).Kind);
            Check.True(Math.Abs(doll.CueAt(shot.StartSeconds - doll.BeatSeconds).BeatsUntilResponse - 1) < .000001);
            var fox = Demo("offbeat-single-tap");
            Check.True(Math.Abs(fox.DisplayBeat(fox.Round.Notes[0].StartSeconds) - 1.5) < .000001);
        }

        [Test]
        public void DiveHasAnEndReleaseCueWhileHoldAndShakeKeepTheirOwnInstructions()
        {
            foreach (var id in new[] { "ray-deep-dive", "ray-short-dive", "turtle-long-hold", "one-beat-shake" })
            {
                var demo = Demo(id); var note = demo.Round.Notes[0];
                Check.Equal(PreviewCueKind.Sustain, demo.CueAt((note.StartSeconds + note.EndSeconds) * .5).Kind);
                var end = demo.CueAt(note.EndSeconds);
                Check.Equal(note.Step.Kind == GestureKind.Dive ? PreviewCueKind.Release : PreviewCueKind.Rest, end.Kind);
            }
        }

        [Test]
        public void BrowsingAndRestartingDoNotResolveOrShareLiveNotes()
        {
            var first = Demo("march-three"); var second = Demo("march-three");
            Check.False(ReferenceEquals(first.Round.Notes[0], second.Round.Notes[0]));
            first.Round.Press(first.Round.Notes[0].StartSeconds, 0, 0);
            Check.True(first.Round.Results.Count > 0);
            for (double time = 0; time < second.DurationSeconds * 4; time += .013)
                second.CueAt(second.LoopSeconds(time));
            Check.Equal(0, second.Round.Results.Count);
            Check.True(second.Round.Notes.All(note => note.Result == null && note.State == ResponseState.Pending));
            Check.Equal(0.0, second.Round.ElapsedSeconds);
        }

        private static MonsterPreview Demo(string patternId)
        {
            var monster = MonsterCatalog.All.First(m => m.Patterns.Any(p => p.Id == patternId));
            return new MonsterPreview(monster, monster.Patterns.First(p => p.Id == patternId));
        }
    }
}
