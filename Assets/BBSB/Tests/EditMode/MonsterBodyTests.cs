using System;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class MonsterBodyTests
    {
        [Test]
        public void EveryCallCompletesItsWholeBodyMotionBeforeTheNextCallOrResponse()
        {
            foreach (double bpm in new[] { 60.0, 120.0, 200.0 })
            foreach (var monster in MonsterCatalog.All)
            foreach (var pattern in monster.Patterns)
            {
                var demo = new MonsterPreview(monster, pattern, bpm); var attack = demo.Attack;
                var actor = demo.Round.Plan.Monsters[0]; double beat = demo.BeatSeconds;
                for (int i = 0; i < attack.Call.Count; i++)
                {
                    double start = attack.Call[i].Tick * beat / 4;
                    double duration = MonsterBodyTimeline.CallDuration(attack, i, beat);
                    double next = i + 1 < attack.Call.Count ? attack.Call[i + 1].Tick * beat / 4 : attack.ResponseStartTick * beat / 4;
                    Check.True(duration > 0 && start + duration < next);
                    var from = new[] { MonsterBodyPose.Idle, MonsterBodyPose.Call, MonsterBodyPose.Attack, MonsterBodyPose.Recover };
                    var to = new[] { MonsterBodyPose.Call, MonsterBodyPose.Attack, MonsterBodyPose.Recover, MonsterBodyPose.Idle };
                    for (int part = 0; part < 4; part++)
                    {
                        var frame = MonsterBodyTimeline.Evaluate(actor, start + duration * (part + .5) / 4, beat);
                        Check.True(frame.Active); Check.Equal(i, frame.CallIndex);
                        Check.Equal(from[part], frame.From); Check.Equal(to[part], frame.To);
                        Check.True(Math.Abs(frame.Blend - .5) < .000001);
                    }
                    var beginning = MonsterBodyTimeline.Evaluate(actor, start, beat);
                    Check.Equal(MonsterBodyPose.Idle, beginning.From); Check.Equal(0.0, beginning.Blend);
                    var ending = MonsterBodyTimeline.Evaluate(actor, start + duration * .999999, beat);
                    Check.Equal(MonsterBodyPose.Idle, ending.To); Check.True(ending.Blend > .99999);
                    Check.False(MonsterBodyTimeline.Evaluate(actor, start + duration + .000001, beat).Active);
                }
                foreach (var note in demo.Round.Notes)
                {
                    Check.False(MonsterBodyTimeline.Evaluate(actor, note.StartSeconds, beat).Active);
                    Check.False(MonsterBodyTimeline.Evaluate(actor, (note.StartSeconds + note.EndSeconds) / 2, beat).Active);
                    Check.False(MonsterBodyTimeline.Evaluate(actor, note.EndSeconds, beat).Active);
                }
            }
        }
    }
}
