using System;
using System.Linq;
using BBSB.Core;
using BBSB.Runtime.UI;
#if !BBSB_STANDALONE
using NUnit.Framework;
#endif

namespace BBSB.Tests
{
    public sealed class MonsterAuthoringCoreTests
    {
        private static MonsterDefinition Monster(string id = "test-authored") => new MonsterDefinition(id, "테스트", "",
            new RhythmPattern("custom-hold", 4, new[] { new PatternStep(GestureKind.Hold, 0, 8) }),
            new[] { new CallSignal(0, "통!") }, 12, 4, 1);
        [Test]
        public void CustomCatalogIsDeterministicRejectsCollisionsAndKeepsBuiltIns()
        {
            var previous = MonsterCatalog.All.Skip(MonsterCatalog.BuiltIn.Count).ToArray();
            try
            {
                MonsterCatalog.SetCustom(new[] { Monster("z-custom"), Monster("a-custom") });
                Check.Equal(MonsterCatalog.BuiltIn.Count + 2, MonsterCatalog.All.Count);
                Check.Equal("a-custom", MonsterCatalog.All[MonsterCatalog.BuiltIn.Count].Id);
                var before = MonsterCatalog.All;
                bool rejected = false;
                try { MonsterCatalog.SetCustom(new[] { Monster(MonsterCatalog.BuiltIn[0].Id) }); }
                catch (ArgumentException) { rejected = true; }
                Check.True(rejected); Check.True(ReferenceEquals(before, MonsterCatalog.All));
                rejected = false;
                try { MonsterCatalog.SetCustom(new[] { Monster(), Monster() }); }
                catch (ArgumentException) { rejected = true; }
                Check.True(rejected); Check.True(ReferenceEquals(before, MonsterCatalog.All));
            }
            finally { MonsterCatalog.SetCustom(previous); }
        }
        [Test]
        public void NewPatternHasAnAttackVisualWithoutHardCodedImageSlots()
        {
            var monster = Monster(); var preview = new MonsterPreview(monster, monster.Patterns[0]);
            var visual = MonsterAttackCatalog.For(preview.Round.Notes[0]);
            Check.Equal(monster.Id, visual.MonsterId); Check.Equal("custom-hold", visual.PatternId);
            Check.Equal(MonsterAttackMotion.Linear, visual.Motion);
        }
        [Test]
        public void AnimatorTimelineUsesCallResponseSustainRecoveryAndRealJudgments()
        {
            var monster = Monster(); var preview = new MonsterPreview(monster, monster.Patterns[0]);
            var round = preview.Round; var plan = round.Plan.Monsters[0]; var note = round.Notes[0];
            Check.Equal(MonsterAnimationSituation.Idle, MonsterAnimationTimeline.Evaluate(round, plan, 0).Situation);
            var call = MonsterAnimationTimeline.Evaluate(round, plan, preview.CallSeconds + .1);
            Check.Equal(MonsterAnimationSituation.Call, call.Situation); Check.Equal(0, call.CallOffsetTick);
            Check.Equal(MonsterAnimationSituation.Attack, MonsterAnimationTimeline.Evaluate(round, plan, note.StartSeconds + .1).Situation);
            Check.Equal(MonsterAnimationSituation.Attack, MonsterAnimationTimeline.Evaluate(round, plan, note.EndSeconds - .1).Situation);
            round.Press(note.StartSeconds + .1, 0, 0); round.Advance(note.EndSeconds);
            Check.Equal(MonsterAnimationSituation.HalfMiss, MonsterAnimationTimeline.Evaluate(round, plan, round.ElapsedSeconds).Situation);
            Check.Equal(MonsterAnimationSituation.Idle, MonsterAnimationTimeline.Evaluate(round, plan, note.EndSeconds + .3).Situation);
            preview.Restart(); round = preview.Round; plan = round.Plan.Monsters[0];
            Check.Equal(MonsterAnimationSituation.Recover, MonsterAnimationTimeline.Evaluate(round, plan, note.EndSeconds + .05).Situation);
            round.Advance(note.StartSeconds + round.HalfMissWindow + .01);
            Check.Equal(MonsterAnimationSituation.Miss, MonsterAnimationTimeline.Evaluate(round, plan, round.ElapsedSeconds).Situation);
        }
        [Test]
        public void AnimatorTimelineResetsCallAgeForEachSignalAndRestart()
        {
            var monster = new MonsterDefinition("two-call", "테스트", "",
                new RhythmPattern("two-call-tap", 8, new[] { new PatternStep(GestureKind.Tap, 0) }),
                new[] { new CallSignal(0, "통"), new CallSignal(4, "탕") }, 4, 4, 1);
            var preview = new MonsterPreview(monster, monster.Patterns[0]);
            var round = preview.Round; var plan = round.Plan.Monsters[0];
            var first = MonsterAnimationTimeline.Evaluate(round, plan, preview.CallSeconds + .1);
            var second = MonsterAnimationTimeline.Evaluate(round, plan, preview.CallSeconds + round.BeatSeconds + .1);
            Check.Equal(0, first.CallOffsetTick); Check.Equal(4, second.CallOffsetTick);
            Check.True(Math.Abs(first.AgeBeats - second.AgeBeats) < 1e-8);
            round.Press(round.Notes[0].StartSeconds, 0, 0);
            Check.Equal(MonsterAnimationSituation.Perfect, MonsterAnimationTimeline.Evaluate(round, plan, round.ElapsedSeconds).Situation);
            preview.Restart(); Check.Equal(MonsterAnimationSituation.Idle,
                MonsterAnimationTimeline.Evaluate(preview.Round, plan, 0).Situation);
        }
    }
}
